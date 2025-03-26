using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Core.Diagnostics;

namespace Core.Capturing;

public interface IPtnshiftFinder
{
    public record Location(int X, int Y);

    event Action LocationLost;
    event Action<Location> LocationFound;
    void OnFullScreenCapture(int width, ReadOnlySpan<byte> buffer);
    void OnRegionCapture(ReadOnlySpan<byte> buffer);
}

public class PtnshiftFinder : IPtnshiftFinder
{
    // abaabbbaaaabbbbb, where a = 1c1c1cff and b = 2c2c2cff

    private static readonly byte[] PixelA = [0x1C, 0x1C, 0x1C];
    private static readonly byte[] PixelB = [0x2C, 0x2C, 0x2C];
    private static readonly byte[] PixelC = [0x2B, 0x2B, 0x2B];

    private static readonly byte[] ExpectedBytes = new[]
        {
            PixelA, PixelB, PixelA, PixelA,
            PixelB, PixelB, PixelB, PixelA,
            PixelA, PixelA, PixelA, PixelB,
            PixelB, PixelB, PixelB, PixelB
        }
        .SelectMany(x => x).ToArray();

    private static readonly byte[] UnexpectedBytes = new[]
        {
            PixelA, PixelB, PixelA, PixelA,
            PixelB, PixelB, PixelB, PixelA,
            PixelA, PixelA, PixelA, PixelB,
            PixelB, PixelB, PixelB, PixelC
        }
        .SelectMany(x => x).ToArray();


    private IDebugWriter DebugWriter { get; }
    private TimeProvider TimeProvider { get; }
    private ITimer LocationCheckTimer { get; }

    private bool IsLocationLost { get; set; }

    // Internal for testing purposes
    internal long LastLocationCheckTimestamp { get; private set; }

    public PtnshiftFinder(
        IDebugWriter debugWriter,
        TimeProvider timeProvider)
    {
        DebugWriter = debugWriter;
        TimeProvider = timeProvider;
        LocationCheckTimer =
            TimeProvider.CreateTimer(
                OnLocationCheckTick,
                null,
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromMilliseconds(500));
    }

    public event Action LocationLost = delegate { };
    public event Action<IPtnshiftFinder.Location> LocationFound = delegate { };

    private IPtnshiftFinder.Location? FoundLocation { get; set; }

    public void OnFullScreenCapture(int width, ReadOnlySpan<byte> buffer)
    {
        if (FindInBuffer(buffer, width, out var location))
        {
            FoundLocation = location;
        }
    }

    private void SetLocationLost()
    {
        if (IsLocationLost)
        {
            return;
        }

        IsLocationLost = true;
        LocationLost.Invoke();
    }

    private void SetLocationFound(IPtnshiftFinder.Location location)
    {
        IsLocationLost = false;
        LocationFound.Invoke(location);
    }

    public void OnRegionCapture(ReadOnlySpan<byte> buffer)
    {
        if (IsLocationLost)
        {
            // We're already lost, let the timer-based check handle it
            return;
        }

        if (buffer.IndexOf(ExpectedBytes) == 0)
        {
            // We are where we should be
            return;
        }

        // We're offset, leave it to the timer-based check to avoid race conditions
        SetLocationLost();
    }

    private void OnLocationCheckTick(object? state)
    {
        LastLocationCheckTimestamp = TimeProvider.GetTimestamp();
        if (FoundLocation != null)
        {
            SetLocationFound(FoundLocation);
        }
    }

    private static bool FindInBuffer(
        ReadOnlySpan<byte> buffer,
        int width,
        out IPtnshiftFinder.Location? location)
    {
        var index = buffer.IndexOf(ExpectedBytes);

        if (index == -1)
        {
            // Windows seems to render the pixels weirdly, maybe anti-aliasing?
            index = buffer.IndexOf(UnexpectedBytes);
        }

        if (index == -1)
        {
            // Left frame
            location = null;
            return false;
        }

        if (index == 0)
        {
            location = null;
            return true;
        }

        var pixelIndex = index / 3;
        var x = pixelIndex % width;
        var y = pixelIndex / width;
        location = new(x, y);
        return true;
    }
}
