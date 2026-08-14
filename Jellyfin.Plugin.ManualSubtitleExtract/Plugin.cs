using System;
using System.Collections.Generic;
using Jellyfin.Plugin.ManualSubtitleExtract.Configuration;
using Jellyfin.Plugin.ManualSubtitleExtract.Web;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ManualSubtitleExtract;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        logger.LogInformation("Manual Subtitle Extract plugin loaded");

        try
        {
            WebClientInjection.WebPath = applicationPaths.WebPath;
        }
        catch (NotSupportedException ex)
        {
            WebClientInjection.Problem = "This server does not expose a Jellyfin Web folder; the item menu cannot be extended.";
            logger.LogWarning(ex, "Could not locate Jellyfin Web path");
        }
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Manual Subtitle Extract";

    public override Guid Id => Guid.Parse("2f5b1b4c-1788-4f8a-a087-c6a4f68b5aa7");

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var prefix = GetType().Namespace;
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{prefix}.Configuration.configPage.html"
            },
            new PluginPageInfo
            {
                Name = "manual-subtitle-extract.js",
                EmbeddedResourcePath = $"{prefix}.Configuration.manual-subtitle-extract.js"
            }
        };
    }
}
