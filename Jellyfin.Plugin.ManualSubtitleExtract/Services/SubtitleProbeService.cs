using System.Text.Json;
using Jellyfin.Plugin.ManualSubtitleExtract.Models;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Services;

public sealed class SubtitleProbeService
{
    private static readonly HashSet<string> TextCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "subrip", "srt", "ass", "ssa", "webvtt", "mov_text", "text", "microdvd", "mpl2", "jacosub", "sami", "realtext", "pjs", "subviewer", "subviewer1"
    };

    private readonly ExecutableLocator _locator;
    private readonly ProcessRunner _runner;

    public SubtitleProbeService(ExecutableLocator locator, ProcessRunner runner)
    {
        _locator = locator;
        _runner = runner;
    }

    public async Task<IReadOnlyList<SubtitleTrackDto>> GetTracksAsync(string mediaPath, CancellationToken cancellationToken)
    {
        var ffprobe = _locator.FindFfprobe();
        var result = await _runner.RunAsync(
            ffprobe,
            new[]
            {
                "-v", "error",
                "-print_format", "json",
                "-show_streams",
                mediaPath
            },
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffprobe failed: {result.Stderr.Trim()}");
        }

        var parsed = JsonSerializer.Deserialize<FfprobeResult>(result.Stdout)
            ?? throw new InvalidOperationException("ffprobe returned invalid JSON.");

        var subtitleStreams = parsed.Streams
            .Where(s => string.Equals(s.CodecType, "subtitle", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var tracks = new List<SubtitleTrackDto>(subtitleStreams.Count);
        for (var i = 0; i < subtitleStreams.Count; i++)
        {
            var stream = subtitleStreams[i];
            var language = GetTag(stream.Tags, "language", "und");
            var title = GetTag(stream.Tags, "title", string.Empty);
            var textBased = TextCodecs.Contains(stream.CodecName);
            var flags = new List<string>();
            if (stream.Disposition?.Default == 1) flags.Add("Default");
            if (stream.Disposition?.Forced == 1) flags.Add("Forced");
            if (stream.Disposition?.HearingImpaired == 1) flags.Add("SDH");

            var display = $"{language.ToUpperInvariant()} · {stream.CodecName}";
            if (!string.IsNullOrWhiteSpace(title)) display += $" · {title}";
            if (flags.Count > 0) display += $" · {string.Join(", ", flags)}";
            if (!textBased) display += " · image-based";

            tracks.Add(new SubtitleTrackDto
            {
                StreamIndex = stream.Index,
                SubtitleIndex = i,
                Codec = stream.CodecName,
                Language = language,
                Title = title,
                Default = stream.Disposition?.Default == 1,
                Forced = stream.Disposition?.Forced == 1,
                HearingImpaired = stream.Disposition?.HearingImpaired == 1,
                TextBased = textBased,
                DisplayName = display
            });
        }

        return tracks;
    }

    private static string GetTag(Dictionary<string, string>? tags, string key, string fallback)
    {
        if (tags is null)
        {
            return fallback;
        }

        foreach (var pair in tags)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pair.Value))
            {
                return pair.Value.Trim();
            }
        }

        return fallback;
    }
}
