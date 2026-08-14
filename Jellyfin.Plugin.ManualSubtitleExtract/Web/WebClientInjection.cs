namespace Jellyfin.Plugin.ManualSubtitleExtract.Web;

public static class WebClientInjection
{
    public const string Marker = "manual-subtitle-extract-client";

    public static string? WebPath { get; set; }

    public static string? Problem { get; set; }

    public static bool IsWebIndexPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return true;
        return path.EndsWith("/", StringComparison.Ordinal) || path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryReadIndex(string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath)) return null;
        var path = Path.Combine(webPath, "index.html");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public static string GetClientScriptUrl(string? pathBase)
    {
        var basePath = pathBase ?? string.Empty;
        if (basePath.Length == 0)
        {
            return "/ManualSubtitleExtract/client.js";
        }

        if (!basePath.StartsWith('/'))
        {
            basePath = "/" + basePath;
        }

        if (basePath.EndsWith('/'))
        {
            basePath = basePath.TrimEnd('/');
        }

        return $"{basePath}/ManualSubtitleExtract/client.js";
    }

    public static string? Inject(string html, string basePath)
    {
        if (html.Contains(Marker, StringComparison.Ordinal)) return null;
        var marker = "</body>";
        var index = html.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;

        var script = $"<script id=\"{Marker}\" src=\"{basePath}\"></script>";
        return html.Insert(index, script);
    }
}
