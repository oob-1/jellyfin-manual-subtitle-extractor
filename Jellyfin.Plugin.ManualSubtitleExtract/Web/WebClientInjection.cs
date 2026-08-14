namespace Jellyfin.Plugin.ManualSubtitleExtract.Web;

public static class WebClientInjection
{
    public const string Marker = "manual-subtitle-extract-client";
    private const string ClientScriptVersion = "2026.08.14.2";

    public static string? WebPath { get; set; }

    public static string? Problem { get; set; }

    public static bool IsWebIndexPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return true;
        return path.EndsWith("/", StringComparison.Ordinal)
            || path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web", StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryReadIndex(string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath)) return null;
        var path = Path.Combine(webPath, "index.html");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public static string GetClientScriptUrl(string? pathBase, string? requestPath)
    {
        var basePath = NormalizeBasePath(pathBase);
        if (basePath.Length == 0)
        {
            basePath = InferBasePath(requestPath);
        }

        return $"{basePath}/ManualSubtitleExtract/client.js?v={ClientScriptVersion}";
    }

    private static string InferBasePath(string? requestPath)
    {
        var rawPath = requestPath ?? string.Empty;
        var path = NormalizeBasePath(requestPath);
        if (path.Length == 0)
        {
            return string.Empty;
        }

        foreach (var suffix in new[] { "/web/index.html", "/web/", "/web", "/index.html" })
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeBasePath(path[..^suffix.Length]);
            }
        }

        return rawPath.EndsWith('/', StringComparison.Ordinal) ? NormalizeBasePath(rawPath) : string.Empty;
    }

    private static string NormalizeBasePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return string.Empty;
        }

        var normalized = path;
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        return normalized.TrimEnd('/');
    }

    public static string? Inject(string html, string clientScriptUrl)
    {
        if (html.Contains(Marker, StringComparison.Ordinal)) return null;
        var marker = "</body>";
        var index = html.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        var script = $"<script id=\"{Marker}\" src=\"{clientScriptUrl}\"></script>";
        return index < 0 ? html + script : html.Insert(index, script);
    }
}
