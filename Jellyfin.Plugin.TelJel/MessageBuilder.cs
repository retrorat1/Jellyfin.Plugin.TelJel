using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;

namespace Jellyfin.Plugin.TelJel;

/// <summary>
/// Builds HTML Telegram captions from Jellyfin metadata.
/// </summary>
public static class MessageBuilder
{
    /// <summary>
    /// Builds a movie notification.
    /// </summary>
    /// <param name="movie">Movie item.</param>
    /// <param name="libraryName">Library display name.</param>
    /// <returns>HTML message.</returns>
    public static string BuildMovieMessage(Movie movie, string libraryName)
    {
        var year = movie.ProductionYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var title = string.IsNullOrWhiteSpace(year)
            ? Escape(movie.Name)
            : $"{Escape(movie.Name)} ({Escape(year)})";

        return string.Join(
            '\n',
            $"🎬 <b>{title}</b>",
            $"<i>has been added to <b>{Escape(libraryName)}</b>!</i>",
            "--------------------",
            $"⭐ <b>Rating:</b> <b>{Escape(FormatRating(movie))}</b>   🔞 <b>Certification:</b> <b>{Escape(movie.OfficialRating ?? "N/A")}</b>",
            $"🎭 <b>Genres:</b> {Escape(FormatGenres(movie))}",
            "--------------------",
            "📝 <b>About:</b>",
            Escape(string.IsNullOrWhiteSpace(movie.Overview) ? "No Plot Summary" : movie.Overview),
            string.Empty);
    }

    /// <summary>
    /// Builds a TV notification for one episode or an episode range.
    /// </summary>
    /// <param name="seriesName">Series name.</param>
    /// <param name="seasonNumber">Season number.</param>
    /// <param name="episodeStart">First episode number.</param>
    /// <param name="episodeEnd">Last episode number.</param>
    /// <param name="libraryName">Library display name.</param>
    /// <param name="overview">Overview text.</param>
    /// <param name="rating">Community rating text.</param>
    /// <param name="certification">Official rating.</param>
    /// <param name="genres">Genre list.</param>
    /// <returns>HTML message.</returns>
    public static string BuildTvMessage(
        string seriesName,
        int seasonNumber,
        int episodeStart,
        int episodeEnd,
        string libraryName,
        string? overview,
        string rating,
        string certification,
        string genres)
    {
        var epLabel = episodeStart == episodeEnd
            ? $"Episode {episodeStart:00}"
            : $"Episodes {episodeStart:00}-{episodeEnd:00}";

        return string.Join(
            '\n',
            $"📺 <b>{Escape(seriesName)} Season {seasonNumber:00} {epLabel}</b>",
            $"<i>has been added to <b>{Escape(libraryName)}</b> Library!</i>",
            "--------------------",
            $"⭐ <b>Rating:</b> <b>{Escape(rating)}</b>   🔞 <b>Certification:</b> <b>{Escape(certification)}</b>",
            $"🎭 <b>Genres:</b> {Escape(genres)}",
            "--------------------",
            "📝 <b>About:</b>",
            Escape(string.IsNullOrWhiteSpace(overview) ? "No Plot Summary" : overview),
            string.Empty);
    }

    /// <summary>
    /// Builds a simple test message.
    /// </summary>
    /// <param name="groupName">Group display name.</param>
    /// <returns>HTML message.</returns>
    public static string BuildTestMessage(string groupName)
    {
        return $"✅ <b>TelJel</b>\nTest notification for <b>{Escape(groupName)}</b> — configuration looks good.";
    }

    /// <summary>
    /// Builds a primary image URL for Telegram to fetch.
    /// </summary>
    /// <param name="serverUrl">Configured public server URL.</param>
    /// <param name="itemId">Item id.</param>
    /// <returns>Absolute image URL, or null when server URL is missing.</returns>
    public static string? BuildPrimaryImageUrl(string? serverUrl, Guid itemId)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            return null;
        }

        var baseUrl = serverUrl.TrimEnd('/');
        if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = "http://" + baseUrl;
        }

        return $"{baseUrl}/Items/{itemId}/Images/Primary";
    }

    private static string FormatRating(BaseItem item)
    {
        return item.CommunityRating?.ToString("0.0", CultureInfo.InvariantCulture) ?? "N/A";
    }

    private static string FormatGenres(BaseItem item)
    {
        if (item.Genres == null || item.Genres.Length == 0)
        {
            return "N/A";
        }

        return string.Join(", ", item.Genres);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return WebUtility.HtmlEncode(value);
    }

    /// <summary>
    /// Formats metadata fields from a series or episode for TV messages.
    /// </summary>
    /// <param name="item">Preferred metadata source.</param>
    /// <returns>Tuple of rating, certification, genres.</returns>
    public static (string Rating, string Certification, string Genres) FormatMeta(BaseItem? item)
    {
        if (item == null)
        {
            return ("N/A", "N/A", "N/A");
        }

        return (FormatRating(item), item.OfficialRating ?? "N/A", FormatGenres(item));
    }

    /// <summary>
    /// Picks the best overview for a TV batch.
    /// </summary>
    /// <param name="episodes">Episodes in the batch.</param>
    /// <param name="series">Parent series.</param>
    /// <returns>Overview text.</returns>
    public static string PickTvOverview(IReadOnlyList<Episode> episodes, Series? series)
    {
        if (episodes.Count == 1 && !string.IsNullOrWhiteSpace(episodes[0].Overview))
        {
            return episodes[0].Overview;
        }

        if (series != null && !string.IsNullOrWhiteSpace(series.Overview))
        {
            return series.Overview;
        }

        var firstWithOverview = episodes.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Overview));
        return firstWithOverview?.Overview ?? "No Plot Summary";
    }
}
