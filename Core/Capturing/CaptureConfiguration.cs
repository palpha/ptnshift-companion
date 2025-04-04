using System.Diagnostics.CodeAnalysis;

namespace Core.Capturing;

public record CaptureConfiguration(
    int? DisplayId,
    int CaptureX,
    int CaptureY,
    int Width,
    int Height,
    int FrameRate)
{
    private bool TryGetFirstValid(
        IReadOnlyCollection<DisplayInfo> availableDisplays,
        [NotNullWhen(true)] out CaptureConfiguration? configuration)
    {
        configuration = null;

        foreach (var display in availableDisplays)
        {
            if (TryNormalize(display, out var normalized) == false)
            {
                continue;
            }

            configuration = normalized;
            return true;
        }

        return false;
    }

    private bool TryNormalize(
        DisplayInfo? display,
        [NotNullWhen(true)] out CaptureConfiguration? configuration)
    {
        configuration = null;

        if (display == null)
        {
            return false;
        }

        var scalingFactor = display.ScalingFactor;
        var effectiveWidth = (int) (960 * scalingFactor + 0.5);
        var effectiveHeight = (int) (161 * scalingFactor + 0.5);
        var maxX = Math.Max(0, display.Width - effectiveWidth);
        var maxY = Math.Max(0, display.Height - effectiveHeight);

        configuration = this with
        {
            CaptureX = Math.Clamp(CaptureX, 0, maxX),
            CaptureY = Math.Clamp(CaptureY, 0, maxY),
            Width = effectiveWidth,
            Height = effectiveHeight,
            FrameRate = Math.Clamp(FrameRate, 1, 100)
        };

        return true;
    }


    public CaptureConfiguration GetNormalized(IReadOnlyCollection<DisplayInfo> availableDisplays)
    {
        var display = availableDisplays.FirstOrDefault(x => x.Id == DisplayId)
            ?? availableDisplays.FirstOrDefault(x => x.IsPrimary);

        if (TryNormalize(display, out var normalized))
        {
            return normalized;
        }

        if (TryGetFirstValid(availableDisplays, out var configuration))
        {
            return configuration;
        }

        return this with
        {
            DisplayId = null
        };
    }

    [MemberNotNullWhen(true, nameof(DisplayId))]
    public bool IsValid(IReadOnlyCollection<DisplayInfo> availableDisplays) =>
        DisplayId.HasValue
        && availableDisplays.Any(x => x.Id == DisplayId)
        && CaptureX >= 0
        && CaptureY >= 0
        && Width > 0
        && Height > 0
        && FrameRate > 0;
}
