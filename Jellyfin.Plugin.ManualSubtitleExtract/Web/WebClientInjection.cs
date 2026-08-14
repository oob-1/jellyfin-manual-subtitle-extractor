namespace Jellyfin.Plugin.ManualSubtitleExtract.Web;

public static class WebClientInjection
{
    public const string Marker = "manual-subtitle-extract-client";
    private const string ClientScriptVersion = "2026.08.14.3";

    public static string? WebPath { get; set; }

    public static string? Problem { get; set; }

    public static bool IsWebIndexPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web", StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryReadIndex(string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath)) return null;
        var path = Path.Combine(webPath, "index.html");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public static string GetWebBasePath(string? pathBase, string requestPath)
    {
        if (requestPath.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
        {
            requestPath = requestPath[..^"/index.html".Length];
        }

        var webBasePath = NormalizePath(requestPath);
        var normalizedPathBase = NormalizePath(pathBase);
        if (normalizedPathBase.Length > 0
            && !webBasePath.Equals(normalizedPathBase, StringComparison.OrdinalIgnoreCase)
            && !webBasePath.StartsWith(normalizedPathBase + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPathBase + webBasePath;
        }

        return webBasePath;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return string.Empty;
        }

        var normalized = path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
        return normalized.TrimEnd('/');
    }

    public static string BuildScriptTag(string webBasePath)
    {
        var prefix = webBasePath.TrimEnd('/');
        return $"<script id=\"{Marker}\" src=\"{prefix}/configurationpage?name=manual-subtitle-extract.js&amp;v={ClientScriptVersion}\" defer></script>";
    }

    public static string? Inject(string html, string webBasePath)
    {
        if (html.Contains(Marker, StringComparison.Ordinal)) return null;
        var script = BuildScriptTag(webBasePath);
        var headIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headIndex >= 0) return html.Insert(headIndex, script);

        var bodyIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyIndex < 0 ? html + script : html.Insert(bodyIndex, script);
    }
}
