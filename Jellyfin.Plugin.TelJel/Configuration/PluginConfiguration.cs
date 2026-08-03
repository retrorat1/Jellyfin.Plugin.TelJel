using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TelJel.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        EnablePlugin = true;
        BotToken = string.Empty;
        ServerUrl = string.Empty;
        TvBatchDelaySeconds = 150;
        Groups = [];
    }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin is enabled.
    /// </summary>
    public bool EnablePlugin { get; set; }

    /// <summary>
    /// Gets or sets the Telegram bot token.
    /// </summary>
    public string BotToken { get; set; }

    /// <summary>
    /// Gets or sets the public Jellyfin base URL used for poster links
    /// (e.g. https://jellyfin.example.com or http://192.168.1.10:8096).
    /// </summary>
    public string ServerUrl { get; set; }

    /// <summary>
    /// Gets or sets how long to wait before flushing a TV episode batch (seconds).
    /// </summary>
    public int TvBatchDelaySeconds { get; set; }

    /// <summary>
    /// Gets or sets Telegram groups linked to libraries.
    /// </summary>
    public TelegramGroupConfiguration[] Groups { get; set; }
}
