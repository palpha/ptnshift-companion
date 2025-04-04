using Core.Capturing;

namespace Core.Diagnostics;

public interface IFrameDebugger
{
    event Action<string> FrameDumpWritten;
    Task DumpLastFrameAsync();
}

public class FrameDebugger : IFrameDebugger
{
    private Lock FrameDumpLock { get; } = new();
    private byte[] LastFrameData { get; } = new byte[960 * 161 * 3];

    public event Action<string> FrameDumpWritten = delegate { };

    public FrameDebugger(ICaptureService captureService)
    {
        if (AppConstants.IsDebug == false)
        {
            return;
        }

        captureService.FrameCaptured += OnFrameCaptured;
    }

    private void OnFrameCaptured(ReadOnlySpan<byte> frame)
    {
        lock (FrameDumpLock)
        {
            frame.CopyTo(LastFrameData);
        }
    }

    public async Task DumpLastFrameAsync()
    {
        var tmpDir = Path.GetTempPath();
        var tmpFilename = Path.Combine(tmpDir, "last_frame.txt");
        await using var writer = new StreamWriter(tmpFilename);

        lock (FrameDumpLock)
        {
            for (var i = 0; i < LastFrameData.Length; i += 4)
            {
                var r = LastFrameData[i + 2];
                var g = LastFrameData[i + 1];
                var b = LastFrameData[i];
                writer.Write($"0x{r:X2}{g:X2}{b:X2} ");

                if ((i / 4 + 1) % 960 == 0)
                {
                    writer.WriteLine();
                }
            }
        }

        FrameDumpWritten.Invoke(tmpFilename);
    }
}
