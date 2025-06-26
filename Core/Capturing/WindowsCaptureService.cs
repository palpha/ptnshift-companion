using Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Core.Capturing;

// ReSharper disable once UnusedType.Global
public class WindowsCaptureService(
    IStreamer streamer,
    IDisplayService displayService,
    IPtnshiftFinder ptnshiftFinder,
    IDiagnosticOutputRenderer diagnosticOutputRenderer,
    ILogger<WindowsCaptureService> logger,
    TimeProvider timeProvider)
    : CaptureServiceBase(
        streamer,
        displayService,
        ptnshiftFinder,
        diagnosticOutputRenderer,
        logger,
        timeProvider)
{
    public override Task<bool> CheckCapturePermissionAsync() =>
        // On Windows, usually no special permission needed for direct duplication.
        Task.FromResult(true);

    public override int GetConfigurationChangeDelayMs(CaptureConfiguration configuration) =>
        CurrentConfiguration?.DisplayId != configuration.DisplayId ? 500 : 0;

    protected override async Task UpdateStreamerConfigurationAsync(CaptureConfiguration previousConfiguration)
    {
        if (CurrentConfiguration == null)
        {
            throw new InvalidOperationException("Configuration not set.");
        }

        if (previousConfiguration.DisplayId != CurrentConfiguration.DisplayId)
        {
            await StopStreamerAsync();
            await Task.Delay(200);
            try
            {
                if (Streamer.IsCapturing)
                {
                    return;
                }

                StartStreamer();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to restart capture");
            }
        }
        else
        {
            if (previousConfiguration.CaptureX != CurrentConfiguration.CaptureX
                || previousConfiguration.CaptureY != CurrentConfiguration.CaptureY)
            {
                (Streamer as IRegionUpdater)?.SetRegion(
                    CurrentConfiguration.CaptureX,
                    CurrentConfiguration.CaptureY,
                    CurrentConfiguration.Width,
                    CurrentConfiguration.Height);
            }

            if (previousConfiguration.FrameRate != CurrentConfiguration.FrameRate)
            {
                (Streamer as IFrameRateUpdater)?.SetFrameRate(CurrentConfiguration.FrameRate);
            }
        }
    }
}
