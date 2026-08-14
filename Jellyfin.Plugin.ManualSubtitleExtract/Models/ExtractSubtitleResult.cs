namespace Jellyfin.Plugin.ManualSubtitleExtract.Models;

public sealed class ExtractSubtitleResult
{
    public string OutputPath { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;
}
