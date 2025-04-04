using Core.Capturing;

namespace Core.Diagnostics;

public interface IFrameRateReporter
{
    event Action<double> FrameRateChanged;
}

public class FrameRateCounter : IFrameRateReporter
{
    private ICaptureService CaptureService { get; }
    private TimeProvider TimeProvider { get; }

    private int FrameCount { get; set; }
    private long LastFrameRateReport { get; set; }

    public event Action<double> FrameRateChanged = delegate { };

    public FrameRateCounter(
        ICaptureService captureService,
        TimeProvider timeProvider)
    {
        CaptureService = captureService;
        TimeProvider = timeProvider;

        LastFrameRateReport = TimeProvider.GetTimestamp();
        CaptureService.FrameCaptured += OnFrame;
    }

    private void OnFrame(ReadOnlySpan<byte> _)
    {
        FrameCount++;

        var elapsedSeconds = TimeProvider.GetElapsedTime(LastFrameRateReport).TotalSeconds;
        if (elapsedSeconds < 1)
        {
            return;
        }

        var measuredFrameRate = FrameCount / elapsedSeconds;
        FrameRateChanged.Invoke(measuredFrameRate);

        FrameCount = 0;
        LastFrameRateReport = TimeProvider.GetTimestamp();
    }
}
