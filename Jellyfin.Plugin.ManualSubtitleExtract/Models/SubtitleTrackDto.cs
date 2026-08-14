namespace Jellyfin.Plugin.ManualSubtitleExtract.Models;

public sealed class SubtitleTrackDto
{
    public int StreamIndex { get; init; }

    public int SubtitleIndex { get; init; }

    public string Codec { get; init; } = string.Empty;

    public string Language { get; init; } = "und";

    public string Title { get; init; } = string.Empty;

    public bool Default { get; init; }

    public bool Forced { get; init; }

    public bool HearingImpaired { get; init; }

    public bool TextBased { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}
