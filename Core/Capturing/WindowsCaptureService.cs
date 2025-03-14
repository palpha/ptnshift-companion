using Microsoft.Extensions.Logging;

namespace Core.Capturing;

public class WindowsCaptureService(
    IStreamer streamer,
    IDisplayService displayService,
    ILogger<WindowsCaptureService> logger)
    : CaptureServiceBase(streamer, displayService, logger)
{
    public override Task<bool> CheckCapturePermissionAsync() =>
        // On Windows, usually no special permission needed for direct duplication.
        Task.FromResult(true);

    protected override void UpdateStreamerConfiguration(CaptureConfiguration previousConfiguration)
    {
        if (CurrentConfiguration == null)
        {
            throw new InvalidOperationException("Configuration not set.");
        }

        if (previousConfiguration.CaptureX != CurrentConfiguration.CaptureX
            || previousConfiguration.CaptureY != CurrentConfiguration.CaptureY
            || previousConfiguration.DisplayId != CurrentConfiguration.DisplayId)
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
        else if (previousConfiguration.FrameRate != CurrentConfiguration.FrameRate)
        {
            (Streamer as IFrameRateUpdater)?.SetFrameRate(CurrentConfiguration.FrameRate);
        }
    }
}