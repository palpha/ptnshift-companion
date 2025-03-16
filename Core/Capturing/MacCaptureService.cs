﻿using Microsoft.Extensions.Logging;

namespace Core.Capturing;

// ReSharper disable once UnusedType.Global
public class MacCaptureService(
    IStreamer streamer,
    IDisplayService displayService,
    IPtnshiftFinder ptnshiftFinder,
    ILogger<CaptureServiceBase> logger)
    : CaptureServiceBase(streamer, displayService, ptnshiftFinder, logger)
{
    public override async Task<bool> CheckCapturePermissionAsync()
    {
        return await Streamer.CheckPermissionAsync();
    }

    protected override void UpdateStreamerConfiguration(CaptureConfiguration previousConfiguration)
    {
        if (previousConfiguration.Equals(CurrentConfiguration) || IsCapturing == false)
        {
            return;
        }

        Streamer.Stop();
        StartStreamer();
    }
}