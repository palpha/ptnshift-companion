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
    void OnRegionCapture(int posX, int posY, ReadOnlySpan<byte> buffer);
}

public class PtnshiftFinder : IPtnshiftFinder
{
    // abaabbbaaaabbbbb, where a = 1c1c1cff and b = 2c2c2cff

    private static readonly byte[] PixelA = [0x1C, 0x1C, 0x1C, 0xFF];
    private static readonly byte[] PixelB = [0x2C, 0x2C, 0x2C, 0xFF];

    private static readonly byte[] ExpectedBytes = new[]
        {
            PixelA, PixelB, PixelA, PixelA,
            PixelB, PixelB, PixelB, PixelA,
            PixelA, PixelA, PixelA, PixelB,
            PixelB, PixelB, PixelB, PixelB
        }
        .SelectMany(x => x).ToArray();

    private IDebugWriter DebugWriter { get; }
    private TimeProvider TimeProvider { get; }
    private ITimer LocationCheckTimer { get; }

    private bool IsLocationLost { get; set; }
    private long LastRegionFrameTimestamp { get; set; }
    private byte[]? LastFullScreenBuffer { get; set; }
    private int LastFullScreenWidth { get; set; }

    // Internal for testing purposes
    internal long LastLocationCheckTimestamp { get; set; }

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

    public void OnFullScreenCapture(int width, ReadOnlySpan<byte> buffer)
    {
        LastFullScreenBuffer = buffer.ToArray();
        LastFullScreenWidth = width;
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

    public void OnRegionCapture(int posX, int posY, ReadOnlySpan<byte> buffer)
    {
        LastRegionFrameTimestamp = TimeProvider.GetTimestamp();

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
        FindInFullScreen();
    }

    private void FindInFullScreen()
    {
        var locationPotentiallyLost =
            LastRegionFrameTimestamp > 0
            && TimeProvider.GetElapsedTime(LastRegionFrameTimestamp) > TimeSpan.FromMilliseconds(500);

        if ((locationPotentiallyLost == false && IsLocationLost == false) || LastFullScreenBuffer == null)
        {
            return;
        }

        try
        {
            if (FindInBuffer(
                LastFullScreenBuffer,
                LastFullScreenWidth,
                posX: 0, posY: 0,
                out var location))
            {
                SetLocationFound(location ?? new(0, 0));
            }
        }
        catch
        {
            //
        }
    }

    private static bool FindInBuffer(
        ReadOnlySpan<byte> buffer,
        int width, int posX, int posY,
        out IPtnshiftFinder.Location? location)
    {
        var index = buffer.IndexOf(ExpectedBytes);

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

        var pixelIndex = index / 4;
        var x = posX + pixelIndex % width;
        var y = posY + pixelIndex / width;
        location = new(x, y);
        return true;
    }
}