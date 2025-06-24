using Core.Capturing;
using Microsoft.Extensions.Logging;

namespace Core.Diagnostics;

public interface IFrameRateReporter
{
    event Action<double> FrameRateChanged;
}

public class FrameRateCounter : IFrameRateReporter
{
    private ICaptureService CaptureService { get; }
    private TimeProvider TimeProvider { get; }
    private ILogger<FrameRateCounter> Logger { get; }

    private int FrameCount { get; set; }
    private long LastFrameRateReport { get; set; }

    public event Action<double> FrameRateChanged = delegate { };

    public FrameRateCounter(
        ICaptureService captureService,
        TimeProvider timeProvider,
        ILogger<FrameRateCounter> logger)
    {
        CaptureService = captureService;
        TimeProvider = timeProvider;
        Logger = logger;

        LastFrameRateReport = TimeProvider.GetTimestamp();
        CaptureService.FrameCaptured += OnFrame;
    }

    private void OnFrame(ReadOnlySpan<byte> _)
    {
        try
        {
            FrameCount++;

            var elapsedSeconds = TimeProvider.GetElapsedTime(LastFrameRateReport).TotalSeconds;
            if (elapsedSeconds < 1)
            {
                return;
            }

            var measuredFrameRate = FrameCount / elapsedSeconds;
            Logger.LogDebug("Measured frame rate: {FrameRate:0.00}", measuredFrameRate);
            FrameRateChanged.Invoke(measuredFrameRate);

            FrameCount = 0;
            LastFrameRateReport = TimeProvider.GetTimestamp();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to measure frame rate");
        }
    }
}
