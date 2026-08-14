using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Web;

public sealed class ScriptInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ScriptInjectionMiddleware> _logger;

    public ScriptInjectionMiddleware(
        RequestDelegate next,
        ILogger<ScriptInjectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || !WebClientInjection.IsWebIndexPath(context.Request.Path.Value))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var originalBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context).ConfigureAwait(false);

            responseBuffer.Position = 0;
            context.Response.Body = originalBody;

            if (responseBuffer.Length == 0 || !CanInject(context.Response))
            {
                await responseBuffer.CopyToAsync(originalBody).ConfigureAwait(false);
                return;
            }

            using var reader = new StreamReader(responseBuffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var html = await reader.ReadToEndAsync().ConfigureAwait(false);
            var injected = WebClientInjection.Inject(
                html,
                WebClientInjection.GetWebBasePath(
                    context.Request.PathBase.Value,
                    context.Request.Path.Value ?? string.Empty));

            if (injected is null)
            {
                var originalBytes = Encoding.UTF8.GetBytes(html);
                context.Response.ContentLength = originalBytes.Length;
                await originalBody.WriteAsync(originalBytes).ConfigureAwait(false);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(injected);
            context.Response.ContentLength = bytes.Length;
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            await originalBody.WriteAsync(bytes).ConfigureAwait(false);
            _logger.LogInformation("Serving the Jellyfin web client with the Manual Subtitle Extract action menu script added");
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool CanInject(HttpResponse response)
        => response.StatusCode == StatusCodes.Status200OK
            && (string.IsNullOrWhiteSpace(response.ContentType)
                || response.ContentType.Contains("text/html", StringComparison.OrdinalIgnoreCase));
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
