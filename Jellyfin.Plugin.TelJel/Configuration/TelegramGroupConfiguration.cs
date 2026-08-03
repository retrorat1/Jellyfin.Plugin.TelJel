using System;

namespace Jellyfin.Plugin.TelJel.Configuration;

/// <summary>
/// A Telegram destination linked to one or more Jellyfin libraries.
/// </summary>
public class TelegramGroupConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelegramGroupConfiguration"/> class.
    /// </summary>
    public TelegramGroupConfiguration()
    {
        Id = Guid.NewGuid().ToString("N");
        Name = string.Empty;
        ChatId = string.Empty;
        ThreadId = string.Empty;
        LibraryIds = Array.Empty<string>();
        Enabled = true;
        SilentNotification = false;
    }

    /// <summary>
    /// Gets or sets the unique id for this group entry.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name (e.g. Movies, Kids TV).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the Telegram chat / group / channel id.
    /// </summary>
    public string ChatId { get; set; }

    /// <summary>
    /// Gets or sets an optional forum topic / message thread id.
    /// </summary>
    public string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets Jellyfin library (collection folder) ids this group should receive.
    /// Empty means all libraries.
    /// </summary>
    public string[] LibraryIds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this group is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether notifications are sent silently.
    /// </summary>
    public bool SilentNotification { get; set; }
}
