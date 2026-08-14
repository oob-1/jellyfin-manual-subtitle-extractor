namespace Jellyfin.Plugin.ManualSubtitleExtract.Models;

public sealed class ExtractSubtitleRequest
{
    public int StreamIndex { get; set; }

    public bool Overwrite { get; set; }
}
