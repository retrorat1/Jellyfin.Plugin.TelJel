using Jellyfin.Plugin.TelJel.Notifiers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TelJel;

/// <summary>
/// Registers plugin services with the Jellyfin host.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<TelegramSender>();
        serviceCollection.AddHostedService<ItemAddedNotifier>();
    }
}
