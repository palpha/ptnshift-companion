using Microsoft.Extensions.Logging;

namespace Core.Capturing;

// ReSharper disable once UnusedType.Global
public class WindowsCaptureService(
    IStreamer streamer,
    IDisplayService displayService,
    IPtnshiftFinder ptnshiftFinder,
    ILogger<WindowsCaptureService> logger)
    : CaptureServiceBase(streamer, displayService, ptnshiftFinder, logger)
{
    public override Task<bool> CheckCapturePermissionAsync() =>
        // On Windows, usually no special permission needed for direct duplication.
        Task.FromResult(true);

    public override int GetConfigurationChangeDelayMs(CaptureConfiguration configuration) =>
        CurrentConfiguration?.DisplayId != configuration.DisplayId ? 500 : 0;

    protected override void UpdateStreamerConfiguration(CaptureConfiguration previousConfiguration)
    {
        if (CurrentConfiguration == null)
        {
            throw new InvalidOperationException("Configuration not set.");
        }

        if (previousConfiguration.DisplayId != CurrentConfiguration.DisplayId)
        {
            Streamer.Stop();
            Task.Run(async () =>
            {
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
            });
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