using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Core.Diagnostics;

namespace Core.Capturing;

public interface IPtnshiftFinder
{
    public record Location(int X, int Y);

    bool IsEnabled { get; set; }
    event Action LocationLost;
    event Action<Location> LocationFound;
    void OnFullScreenCapture(int width, ReadOnlySpan<byte> buffer);
    void OnRegionCapture(ReadOnlySpan<byte> buffer);
}

// Pattern: abaabbbaaaabbbbb, where a = 1c1c1cff and b = 2c2c2cff

public class PtnshiftFinder : IPtnshiftFinder
{
    private const int Tolerance = 5;

    private static byte PixelA => 0x1C;
    private static byte PixelB => 0x2C;

    private static byte[] ColorPattern { get; } =
    [
        PixelA, PixelB, PixelA, PixelA,
        PixelB, PixelB, PixelB, PixelA,
        PixelA, PixelA, PixelA, PixelB,
        PixelB, PixelB, PixelB, PixelB
    ];

    private static byte[] ExpectedBytes { get; } =
        ColorPattern.SelectMany(x => new[] { x, x, x }).ToArray();

    // 374 black pixels * 3 channels
    private static readonly byte[] BlackLine = new byte[374 * 3];

    static PtnshiftFinder()
    {
        // Fill all bytes in the black block with 0x00
        for (var i = 0; i < BlackLine.Length; i++)
        {
            BlackLine[i] = 0x00;
        }
    }

    private TimeProvider TimeProvider { get; }

    // ReSharper disable once UnusedAutoPropertyAccessor.Local
    private ITimer LocationCheckTimer { get; }

    private bool IsLocationLost { get; set; }

    // Internal for testing purposes
    internal long LastLocationCheckTimestamp { get; private set; }

    public PtnshiftFinder(TimeProvider timeProvider)
    {
        TimeProvider = timeProvider;
        LocationCheckTimer =
            TimeProvider.CreateTimer(
                OnLocationCheckTick,
                null,
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromMilliseconds(500));
    }

    public bool IsEnabled { get; set; }

    public event Action LocationLost = delegate { };
    public event Action<IPtnshiftFinder.Location> LocationFound = delegate { };

    private IPtnshiftFinder.Location? FoundLocation { get; set; }

    public void OnFullScreenCapture(int width, ReadOnlySpan<byte> buffer)
    {
        if (IsEnabled == false)
        {
            return;
        }

        if (FindInBuffer(buffer, width, out var perfectLocation))
        {
            FoundLocation = perfectLocation;
        }
        else if (FindSignatureByBlackLine(buffer, width, out var location))
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
            index = buffer.IndexOf(ExpectedBytes.AsSpan()[..14]);
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

    private static bool FindSignatureByBlackLine(
        ReadOnlySpan<byte> buffer,
        int width,
        [NotNullWhen(true)] out IPtnshiftFinder.Location? location)
    {
        // We know the signature is 16 pixels (48 bytes) and then 374 black pixels
        // So the black block starts right after the 16-pixel signature.

        location = null;
        var searchStart = 0;
        while (true)
        {
            // Look for the black block in the portion of the buffer starting at searchStart
            var idx = buffer[searchStart..].IndexOf(BlackLine);
            if (idx == -1)
            {
                break; // no more matches
            }

            // Convert idx in the sliced buffer to an index in the full buffer.
            idx += searchStart;

            // The signature would end at idx - 1, so the start of the signature is:
            // idx - (16 * 3) = idx - 48
            var signatureStart = idx - 48;
            if (signatureStart >= 0)
            {
                // Check if the 48 bytes preceding idx match our pattern with approx.
                if (IsSignatureMatch(buffer, signatureStart))
                {
                    // Each pixel is 3 bytes
                    var pixelIndex = signatureStart / 3;
                    var x = pixelIndex % width;
                    var y = pixelIndex / width;
                    location = new(x, y);
                    return true;
                }
            }

            // Move searchStart forward so we can look for another occurrence
            searchStart = idx + 1;
        }

        return false;
    }

    private static bool IsNearColor(
        ReadOnlySpan<byte> buffer,
        int startIndex,
        byte expected)
    {
        var r = buffer[startIndex + 0];
        var g = buffer[startIndex + 1];
        var b = buffer[startIndex + 2];

        return Math.Abs(r - expected) <= Tolerance
            && Math.Abs(g - expected) <= Tolerance
            && Math.Abs(b - expected) <= Tolerance;
    }

    private static bool IsSignatureMatch(
        ReadOnlySpan<byte> buffer,
        int offset)
    {
        // We have 16 "slots", each is 3 bytes => 48 bytes in total
        // So for slot i, the byte offset is offset + i*3
        for (var i = 0; i < ColorPattern.Length; i++)
        {
            // Each slot is (R,G,B)
            var slotColor = ColorPattern[i];
            var slotByteOffset = offset + i * 3;

            // If any pixel fails the approximate check, bail out immediately
            if (IsNearColor(buffer, slotByteOffset, slotColor) == false)
            {
                return false;
            }
        }

        return true;
    }
}