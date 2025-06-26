using Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Core.Capturing;

// ReSharper disable once UnusedType.Global
public class MacCaptureService(
    IStreamer streamer,
    IDisplayService displayService,
    IPtnshiftFinder ptnshiftFinder,
    IDiagnosticOutputRenderer diagnosticOutputRenderer,
    ILogger<CaptureServiceBase> logger,
    TimeProvider timeProvider)
    : CaptureServiceBase(
        streamer,
        displayService,
        ptnshiftFinder,
        diagnosticOutputRenderer,
        logger,
        timeProvider)
{
    public override async Task<bool> CheckCapturePermissionAsync()
    {
        return await Streamer.CheckPermissionAsync();
    }

    public override int GetConfigurationChangeDelayMs(CaptureConfiguration configuration) => 100;

    protected override async Task UpdateStreamerConfigurationAsync(CaptureConfiguration previousConfiguration)
    {
        await StopStreamerAsync();
        StartStreamer();
    }
}
