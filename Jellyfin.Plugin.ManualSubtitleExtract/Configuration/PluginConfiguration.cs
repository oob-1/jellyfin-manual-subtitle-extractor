using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string FfmpegPath { get; set; } = string.Empty;

    public string FfprobePath { get; set; } = string.Empty;

    public bool AllowOverwrite { get; set; }
}
