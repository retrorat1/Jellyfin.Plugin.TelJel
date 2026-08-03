using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.TelJel.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TelJel;

/// <summary>
/// TelJel — Telegram notifications for newly added media.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer.</param>
    /// <param name="logger">Logger.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        Logger = logger;
    }

    /// <inheritdoc />
    public override string Name => "TelJel";

    /// <inheritdoc />
    public override string Description => "Rich Telegram notifications when movies and TV episodes are added";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("f662aa5a-4148-4c41-b8ff-0e1facacb5dd");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the plugin logger.
    /// </summary>
    public static ILogger Logger { get; private set; } = null!;

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }
}
