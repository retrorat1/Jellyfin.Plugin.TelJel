using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TelJel.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TelJel.Notifiers;

/// <summary>
/// Listens for newly added movies/episodes and sends Telegram notifications.
/// </summary>
public sealed class ItemAddedNotifier : IHostedService, IDisposable
{
    private const int MaxMetadataRetries = 8;

    private readonly ILibraryManager _libraryManager;
    private readonly TelegramSender _sender;
    private readonly ILogger<ItemAddedNotifier> _logger;
    private readonly ConcurrentDictionary<Guid, int> _pendingRetries = new();
    private readonly ConcurrentDictionary<string, TvBatch> _tvBatches = new();
    private readonly ConcurrentDictionary<Guid, byte> _recentlyNotified = new();
    private readonly object _batchLock = new();
    private Timer? _retryTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemAddedNotifier"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="sender">Telegram sender.</param>
    /// <param name="logger">Logger.</param>
    public ItemAddedNotifier(
        ILibraryManager libraryManager,
        TelegramSender sender,
        ILogger<ItemAddedNotifier> logger)
    {
        _libraryManager = libraryManager;
        _sender = sender;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemAdded;
        _retryTimer = new Timer(OnRetryTick, null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
        _logger.LogInformation("TelJel: ItemAdded notifier started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _retryTimer?.Change(Timeout.Infinite, 0);
        FlushAllTvBatches();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _retryTimer?.Dispose();
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.EnablePlugin)
        {
            return;
        }

        var item = e.Item;
        if (item.IsVirtualItem)
        {
            return;
        }

        if (item is not Movie and not Episode)
        {
            return;
        }

        if (!_recentlyNotified.TryAdd(item.Id, 0))
        {
            return;
        }

        _ = Task.Run(() => ProcessItemAsync(item.Id));
    }

    private async Task ProcessItemAsync(Guid itemId)
    {
        try
        {
            var item = _libraryManager.GetItemById(itemId);
            if (item == null)
            {
                _pendingRetries.TryRemove(itemId, out _);
                return;
            }

            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.EnablePlugin)
            {
                return;
            }

            // Wait briefly for metadata providers to populate overview / images.
            if (NeedsMetadataWait(item))
            {
                var retries = _pendingRetries.AddOrUpdate(itemId, 1, (_, current) => current + 1);
                if (retries <= MaxMetadataRetries)
                {
                    _logger.LogDebug("TelJel: waiting for metadata on {Name} (try {Try})", item.Name, retries);
                    return;
                }

                _logger.LogInformation("TelJel: notifying for {Name} without full metadata", item.Name);
            }

            _pendingRetries.TryRemove(itemId, out _);

            var groups = ResolveGroups(item, config);
            if (groups.Count == 0)
            {
                _logger.LogDebug("TelJel: no matching Telegram groups for {Name}", item.Name);
                return;
            }

            if (item is Movie movie)
            {
                await NotifyMovieAsync(movie, groups, config).ConfigureAwait(false);
                return;
            }

            if (item is Episode episode)
            {
                QueueTvEpisode(episode, groups, config);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TelJel: error processing item {ItemId}", itemId);
        }
    }

    private void OnRetryTick(object? state)
    {
        foreach (var itemId in _pendingRetries.Keys.ToArray())
        {
            _ = Task.Run(() => ProcessItemAsync(itemId));
        }

        // Expire dedup keys after a while so remounts can notify again much later if needed.
        if (_recentlyNotified.Count > 5000)
        {
            _recentlyNotified.Clear();
        }
    }

    private static bool NeedsMetadataWait(BaseItem item)
    {
        return string.IsNullOrWhiteSpace(item.Overview) && item.ProviderIds.Count == 0;
    }

    private async Task NotifyMovieAsync(Movie movie, IReadOnlyList<TelegramGroupConfiguration> groups, PluginConfiguration config)
    {
        var libraryName = GetLibraryName(movie);
        var message = MessageBuilder.BuildMovieMessage(movie, libraryName);
        var imageUrl = MessageBuilder.BuildPrimaryImageUrl(config.ServerUrl, movie.Id);

        foreach (var group in groups)
        {
            await _sender.SendAsync(
                config.BotToken,
                group.ChatId,
                message,
                imageUrl,
                group.SilentNotification,
                string.IsNullOrWhiteSpace(group.ThreadId) ? null : group.ThreadId).ConfigureAwait(false);
        }
    }

    private void QueueTvEpisode(Episode episode, IReadOnlyList<TelegramGroupConfiguration> groups, PluginConfiguration config)
    {
        var seriesId = episode.SeriesId;
        var season = episode.ParentIndexNumber ?? 0;
        var key = $"{seriesId:N}|{season}";
        var delay = Math.Max(5, config.TvBatchDelaySeconds);

        lock (_batchLock)
        {
            if (!_tvBatches.TryGetValue(key, out var batch))
            {
                batch = new TvBatch(seriesId, season, groups.ToList());
                _tvBatches[key] = batch;
            }
            else
            {
                foreach (var group in groups)
                {
                    if (batch.Groups.All(g => g.Id != group.Id))
                    {
                        batch.Groups.Add(group);
                    }
                }
            }

            batch.EpisodeIds.Add(episode.Id);
            batch.Timer?.Dispose();
            batch.Timer = new Timer(
                _ => _ = Task.Run(() => FlushTvBatchAsync(key)),
                null,
                TimeSpan.FromSeconds(delay),
                Timeout.InfiniteTimeSpan);
        }
    }

    private async Task FlushTvBatchAsync(string key)
    {
        TvBatch? batch;
        lock (_batchLock)
        {
            if (!_tvBatches.TryRemove(key, out batch))
            {
                return;
            }

            batch.Timer?.Dispose();
        }

        try
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null || !config.EnablePlugin || batch.Groups.Count == 0)
            {
                return;
            }

            var episodes = batch.EpisodeIds
                .Select(id => _libraryManager.GetItemById(id) as Episode)
                .Where(e => e != null)
                .Cast<Episode>()
                .OrderBy(e => e.IndexNumber ?? 0)
                .ToList();

            if (episodes.Count == 0)
            {
                return;
            }

            var series = _libraryManager.GetItemById(batch.SeriesId) as Series;
            var seriesName = series?.Name ?? episodes[0].SeriesName ?? "Unknown";
            var seasonNumber = batch.SeasonNumber;
            var epNumbers = episodes.Select(e => e.IndexNumber ?? 0).Where(n => n > 0).ToList();
            if (epNumbers.Count == 0)
            {
                epNumbers.Add(0);
            }

            var epStart = epNumbers.Min();
            var epEnd = epNumbers.Max();
            var libraryName = GetLibraryName(episodes[0]);
            var metaSource = (BaseItem?)series ?? episodes[0];
            var (rating, cert, genres) = MessageBuilder.FormatMeta(metaSource);
            var overview = MessageBuilder.PickTvOverview(episodes, series);
            var message = MessageBuilder.BuildTvMessage(
                seriesName,
                seasonNumber,
                epStart,
                epEnd,
                libraryName,
                overview,
                rating,
                cert,
                genres);

            // Prefer series poster for batches / show identity.
            var posterItemId = series?.Id ?? episodes[0].Id;
            var imageUrl = MessageBuilder.BuildPrimaryImageUrl(config.ServerUrl, posterItemId);

            foreach (var group in batch.Groups)
            {
                await _sender.SendAsync(
                    config.BotToken,
                    group.ChatId,
                    message,
                    imageUrl,
                    group.SilentNotification,
                    string.IsNullOrWhiteSpace(group.ThreadId) ? null : group.ThreadId).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TelJel: failed flushing TV batch {Key}", key);
        }
    }

    private void FlushAllTvBatches()
    {
        string[] keys;
        lock (_batchLock)
        {
            keys = _tvBatches.Keys.ToArray();
        }

        foreach (var key in keys)
        {
            FlushTvBatchAsync(key).GetAwaiter().GetResult();
        }
    }

    private List<TelegramGroupConfiguration> ResolveGroups(BaseItem item, PluginConfiguration config)
    {
        var libraryIds = GetLibraryIds(item);
        var result = new List<TelegramGroupConfiguration>();

        foreach (var group in config.Groups ?? Array.Empty<TelegramGroupConfiguration>())
        {
            if (!group.Enabled || string.IsNullOrWhiteSpace(group.ChatId))
            {
                continue;
            }

            // Only notify when at least one library is explicitly selected (ticked).
            if (group.LibraryIds == null || group.LibraryIds.Length == 0)
            {
                continue;
            }

            if (libraryIds.Any(id => group.LibraryIds.Contains(id, StringComparer.OrdinalIgnoreCase)))
            {
                result.Add(group);
            }
        }

        return result;
    }

    private HashSet<string> GetLibraryIds(BaseItem item)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var folder in _libraryManager.GetCollectionFolders(item))
            {
                ids.Add(folder.Id.ToString("N"));
                ids.Add(folder.Id.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TelJel: could not resolve libraries for {Name}", item.Name);
        }

        return ids;
    }

    private string GetLibraryName(BaseItem item)
    {
        try
        {
            var folder = _libraryManager.GetCollectionFolders(item).FirstOrDefault();
            if (folder != null && !string.IsNullOrWhiteSpace(folder.Name))
            {
                return folder.Name;
            }
        }
        catch
        {
            // ignored
        }

        return "library";
    }

    private sealed class TvBatch
    {
        public TvBatch(Guid seriesId, int seasonNumber, List<TelegramGroupConfiguration> groups)
        {
            SeriesId = seriesId;
            SeasonNumber = seasonNumber;
            Groups = groups;
            EpisodeIds = [];
        }

        public Guid SeriesId { get; }

        public int SeasonNumber { get; }

        public List<TelegramGroupConfiguration> Groups { get; }

        public HashSet<Guid> EpisodeIds { get; }

        public Timer? Timer { get; set; }
    }
}
