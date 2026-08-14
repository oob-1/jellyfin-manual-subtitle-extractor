using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Web;

public sealed class FileTransformationRegistrationService : BackgroundService
{
    private static readonly Guid TransformationId = Guid.Parse("31504013-3c56-4af8-a28d-dc5696f71864");

    private readonly ILogger<FileTransformationRegistrationService> _logger;

    public FileTransformationRegistrationService(ILogger<FileTransformationRegistrationService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= 6 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            if (TryRegister(_logger))
            {
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        _logger.LogInformation("File Transformation plugin was not available; Manual Subtitle Extract will use middleware injection only");
    }

    public static bool TryRegister(ILogger logger)
    {
        try
        {
            var assembly = AssemblyLoadContext.All
                .SelectMany(context => context.Assemblies)
                .FirstOrDefault(candidate => candidate.FullName?.Contains(".FileTransformation", StringComparison.OrdinalIgnoreCase) == true);

            var interfaceType = assembly?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
            var registerMethod = interfaceType?.GetMethod("RegisterTransformation", BindingFlags.Public | BindingFlags.Static);
            var payloadType = registerMethod?.GetParameters().FirstOrDefault()?.ParameterType;
            if (registerMethod is null || payloadType is null)
            {
                return false;
            }

            var payload = CreateJObjectPayload(payloadType);
            if (payload is null)
            {
                return false;
            }

            registerMethod.Invoke(null, new[] { payload });
            logger.LogInformation("Registered Manual Subtitle Extract index.html transform with File Transformation");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not register Manual Subtitle Extract with File Transformation yet");
            return false;
        }
    }

    private static object? CreateJObjectPayload(Type jObjectType)
    {
        var payload = Activator.CreateInstance(jObjectType);
        var jTokenType = jObjectType.Assembly.GetType("Newtonsoft.Json.Linq.JToken");
        if (payload is null || jTokenType is null)
        {
            return null;
        }

        var addMethod = jObjectType.GetMethod("Add", new[] { typeof(string), jTokenType });
        var fromObjectMethod = jTokenType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method =>
            {
                var parameters = method.GetParameters();
                return method.Name == "FromObject"
                    && parameters.Length == 1
                    && parameters[0].ParameterType == typeof(object);
            });

        if (addMethod is null || fromObjectMethod is null)
        {
            return null;
        }

        AddPayloadValue(payload, addMethod, fromObjectMethod, "id", TransformationId);
        AddPayloadValue(payload, addMethod, fromObjectMethod, "fileNamePattern", "index.html");
        AddPayloadValue(payload, addMethod, fromObjectMethod, "callbackAssembly", typeof(WebClientInjection).Assembly.FullName ?? string.Empty);
        AddPayloadValue(payload, addMethod, fromObjectMethod, "callbackClass", typeof(WebClientInjection).FullName ?? string.Empty);
        AddPayloadValue(payload, addMethod, fromObjectMethod, "callbackMethod", nameof(WebClientInjection.TransformIndexHtml));

        return payload;
    }

    private static void AddPayloadValue(object payload, MethodInfo addMethod, MethodInfo fromObjectMethod, string key, object value)
    {
        var token = fromObjectMethod.Invoke(null, new[] { value });
        addMethod.Invoke(payload, new[] { key, token });
    }
}
