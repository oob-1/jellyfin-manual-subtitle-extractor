using Jellyfin.Plugin.ManualSubtitleExtract.Web;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ManualSubtitleExtract.Services;

public sealed class StartupService : IScheduledTask
{
    private readonly ILogger<StartupService> _logger;

    public StartupService(ILogger<StartupService> logger)
    {
        _logger = logger;
    }

    public string Name => "Manual Subtitle Extract Startup";

    public string Key => "Jellyfin.Plugin.ManualSubtitleExtract.Startup";

    public string Description => "Registers Manual Subtitle Extract web client transformations.";

    public string Category => "Startup Services";

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual Subtitle Extract Startup. Registering file transformations.");

        for (var attempt = 1; attempt <= 10 && !cancellationToken.IsCancellationRequested; attempt++)
        {
            if (FileTransformationRegistrationService.TryRegister(_logger))
            {
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        _logger.LogInformation("Manual Subtitle Extract could not register with File Transformation during startup");
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.StartupTrigger
        };
    }
}
