using System.Runtime.InteropServices;
using Jellyfin.Plugin.ManualSubtitleExtract.Configuration;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Services;

public sealed class ExecutableLocator
{
    private static readonly string[] LinuxFfmpegCandidates =
    {
        "/usr/lib/jellyfin-ffmpeg/ffmpeg",
        "/usr/local/bin/ffmpeg",
        "/usr/bin/ffmpeg"
    };

    private static readonly string[] LinuxFfprobeCandidates =
    {
        "/usr/lib/jellyfin-ffmpeg/ffprobe",
        "/usr/local/bin/ffprobe",
        "/usr/bin/ffprobe"
    };

    public string FindFfmpeg()
    {
        var configured = Plugin.Instance?.Configuration.FfmpegPath;
        return Find(configured, "ffmpeg", LinuxFfmpegCandidates);
    }

    public string FindFfprobe()
    {
        var configured = Plugin.Instance?.Configuration.FfprobePath;
        return Find(configured, "ffprobe", LinuxFfprobeCandidates);
    }

    private static string Find(string? configured, string command, IEnumerable<string> linuxCandidates)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured))
            {
                return configured;
            }

            throw new FileNotFoundException($"Configured {command} path does not exist: {configured}");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            foreach (var candidate in linuxCandidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var found = FindOnPath(command + ".exe");
            if (found is not null)
            {
                return found;
            }
        }
        else
        {
            var found = FindOnPath(command);
            if (found is not null)
            {
                return found;
            }
        }

        throw new FileNotFoundException($"Could not find {command}. Configure its full path in Dashboard > Plugins > Manual Subtitle Extract.");
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }
}
