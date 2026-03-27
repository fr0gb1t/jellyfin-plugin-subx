using System.Net.Http.Headers;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Subtitles;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SubX;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient(nameof(SubXSubtitleProvider), client =>
        {
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                applicationHost.Name.Replace(' ', '_'),
                applicationHost.ApplicationVersionString));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                "Jellyfin-Plugin-SubX",
                typeof(PluginServiceRegistrator).Assembly.GetName().Version?.ToString() ?? "0.0.0.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        });

        serviceCollection.AddSingleton<ISubtitleProvider, SubXSubtitleProvider>();
    }
}
