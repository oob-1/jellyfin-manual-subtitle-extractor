using System.Text;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Web;

public sealed class ScriptInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<ScriptInjectionMiddleware> _logger;

    public ScriptInjectionMiddleware(
        RequestDelegate next,
        IApplicationPaths applicationPaths,
        ILogger<ScriptInjectionMiddleware> logger)
    {
        _next = next;
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || !WebClientInjection.IsWebIndexPath(context.Request.Path.Value))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var webPath = WebClientInjection.WebPath;
        if (webPath is null)
        {
            try
            {
                webPath = _applicationPaths.WebPath;
                WebClientInjection.WebPath = webPath;
            }
            catch (NotSupportedException)
            {
                await _next(context).ConfigureAwait(false);
                return;
            }
        }

        var html = WebClientInjection.TryReadIndex(webPath);
        if (html is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var injected = WebClientInjection.Inject(
            html,
            WebClientInjection.GetClientScriptUrl(
                context.Request.PathBase.Value,
                context.Request.Path.Value));
        if (injected is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(injected);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        await context.Response.Body.WriteAsync(bytes).ConfigureAwait(false);
        _logger.LogDebug("Injected Manual Subtitle Extract client into Jellyfin Web");
    }
}

public sealed class ScriptInjectionStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            builder.UseMiddleware<ScriptInjectionMiddleware>();
            next(builder);
        };
    }
}
