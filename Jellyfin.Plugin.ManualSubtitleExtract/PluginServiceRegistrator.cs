using Jellyfin.Plugin.ManualSubtitleExtract.Services;
using Jellyfin.Plugin.ManualSubtitleExtract.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ManualSubtitleExtract;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<ExecutableLocator>();
        serviceCollection.AddSingleton<ProcessRunner>();
        serviceCollection.AddSingleton<SubtitleProbeService>();
        serviceCollection.AddSingleton<SubtitleExtractService>();
        serviceCollection.AddSingleton<IStartupFilter, ScriptInjectionStartupFilter>();
    }
}
