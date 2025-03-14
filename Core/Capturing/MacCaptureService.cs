using Microsoft.Extensions.Logging;

namespace Core.Capturing;

public class MacCaptureService(
    IStreamer streamer,
    IDisplayService displayService,
    ILogger<CaptureServiceBase> logger)
    : CaptureServiceBase(streamer, displayService, logger)
{
    public override async Task<bool> CheckCapturePermissionAsync()
    {
        return await Streamer.CheckPermissionAsync();
    }

    protected override void UpdateStreamerConfiguration(CaptureConfiguration previousConfiguration)
    {
        if (previousConfiguration.Equals(CurrentConfiguration))
        {
            return;
        }

        Streamer.Stop();
        StartStreamer();
    }
}