using System;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Plugin.TelJel.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TelJel.Api;

/// <summary>
/// TelJel configuration helpers.
/// </summary>
[ApiController]
[Route("TelJel")]
[Authorize(Policy = "RequiresElevation")]
[Produces(MediaTypeNames.Application.Json)]
public class TelJelController : ControllerBase
{
    private readonly TelegramSender _sender;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<TelJelController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelJelController"/> class.
    /// </summary>
    /// <param name="sender">Telegram sender.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="logger">Logger.</param>
    public TelJelController(
        TelegramSender sender,
        ILibraryManager libraryManager,
        ILogger<TelJelController> logger)
    {
        _sender = sender;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <summary>
    /// Lists Jellyfin libraries for the config UI.
    /// </summary>
    /// <returns>Library id/name pairs.</returns>
    [HttpGet("Libraries")]
    public ActionResult GetLibraries()
    {
        // Real libraries only — exclude virtual views like "All TV Shows" / "Movies".
        var root = _libraryManager.GetUserRootFolder();
        var libraries = root.Children
            .OfType<CollectionFolder>()
            .Select(c => new { Id = c.Id.ToString("N"), c.Name })
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(libraries);
    }

    /// <summary>
    /// Sends a test message to a configured group.
    /// </summary>
    /// <param name="groupId">Group configuration id.</param>
    /// <returns>Result.</returns>
    [HttpPost("Test/{groupId}")]
    public async Task<ActionResult> TestGroup([FromRoute] string groupId)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            return BadRequest(new { ok = false, error = "Plugin not loaded" });
        }

        if (string.IsNullOrWhiteSpace(config.BotToken))
        {
            return BadRequest(new { ok = false, error = "Bot token is not set" });
        }

        var group = (config.Groups ?? Array.Empty<TelegramGroupConfiguration>())
            .FirstOrDefault(g => string.Equals(g.Id, groupId, StringComparison.OrdinalIgnoreCase));

        if (group == null)
        {
            return NotFound(new { ok = false, error = "Group not found. Save configuration first." });
        }

        if (string.IsNullOrWhiteSpace(group.ChatId))
        {
            return BadRequest(new { ok = false, error = "Group chat id is empty" });
        }

        var message = MessageBuilder.BuildTestMessage(string.IsNullOrWhiteSpace(group.Name) ? group.ChatId : group.Name);
        var ok = await _sender.SendAsync(
            config.BotToken,
            group.ChatId,
            message,
            null,
            group.SilentNotification,
            string.IsNullOrWhiteSpace(group.ThreadId) ? null : group.ThreadId).ConfigureAwait(false);

        if (!ok)
        {
            _logger.LogWarning("TelJel: test message failed for group {GroupId}", groupId);
            return StatusCode(502, new { ok = false, error = "Telegram API rejected the message. Check bot token and chat id." });
        }

        return Ok(new { ok = true });
    }
}
