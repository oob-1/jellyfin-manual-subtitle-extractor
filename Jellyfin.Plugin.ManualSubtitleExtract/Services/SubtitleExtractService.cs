using System.Text.RegularExpressions;
using Jellyfin.Plugin.ManualSubtitleExtract.Models;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Services;

public sealed class SubtitleExtractService
{
    private static readonly Regex UnsafeFilePart = new("[^A-Za-z0-9_-]+", RegexOptions.Compiled);

    private readonly ExecutableLocator _locator;
    private readonly ProcessRunner _runner;
    private readonly SubtitleProbeService _probe;

    public SubtitleExtractService(ExecutableLocator locator, ProcessRunner runner, SubtitleProbeService probe)
    {
        _locator = locator;
        _runner = runner;
        _probe = probe;
    }

    public async Task<ExtractSubtitleResult> ExtractAsync(
        string mediaPath,
        int streamIndex,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var tracks = await _probe.GetTracksAsync(mediaPath, cancellationToken).ConfigureAwait(false);
        var track = tracks.FirstOrDefault(t => t.StreamIndex == streamIndex)
            ?? throw new ArgumentException("The selected subtitle stream no longer exists.", nameof(streamIndex));

        if (!track.TextBased)
        {
            throw new InvalidOperationException($"{track.Codec} is image-based. This plugin intentionally extracts text subtitles only; OCR is not performed.");
        }

        var outputPath = BuildOutputPath(mediaPath, track);
        var allowOverwrite = overwrite && (Plugin.Instance?.Configuration.AllowOverwrite ?? false);
        if (File.Exists(outputPath) && !allowOverwrite)
        {
            throw new IOException($"Subtitle already exists: {Path.GetFileName(outputPath)}. Enable overwrite in plugin settings and confirm overwrite in the dialog if you really want to replace it.");
        }

        var directory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("Could not determine the media directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = outputPath + ".manualextract.tmp.srt";
        try
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);

            var ffmpeg = _locator.FindFfmpeg();
            var args = new List<string>
            {
                "-hide_banner",
                "-loglevel", "error",
                "-i", mediaPath,
                "-map", $"0:{streamIndex}",
                "-c:s", "srt",
                "-y",
                temporaryPath
            };

            var result = await _runner.RunAsync(ffmpeg, args, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0 || !File.Exists(temporaryPath))
            {
                throw new InvalidOperationException($"ffmpeg failed: {result.Stderr.Trim()}");
            }

            File.Move(temporaryPath, outputPath, overwrite: allowOverwrite);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch
            {
                // Best effort cleanup.
            }
        }

        return new ExtractSubtitleResult
        {
            OutputPath = outputPath,
            FileName = Path.GetFileName(outputPath)
        };
    }

    private static string BuildOutputPath(string mediaPath, SubtitleTrackDto track)
    {
        var directory = Path.GetDirectoryName(mediaPath)
            ?? throw new InvalidOperationException("Could not determine the media directory.");
        var baseName = Path.GetFileNameWithoutExtension(mediaPath);
        var language = Sanitize(track.Language, "und").ToLowerInvariant();

        var suffixes = new List<string> { language };
        if (track.Forced) suffixes.Add("forced");
        if (track.HearingImpaired) suffixes.Add("sdh");

        return Path.Combine(directory, $"{baseName}.{string.Join('.', suffixes)}.srt");
    }

    private static string Sanitize(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var safe = UnsafeFilePart.Replace(value.Trim(), string.Empty);
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
}
