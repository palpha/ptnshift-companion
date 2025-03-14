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
    public CaptureConfiguration GetNormalized(IReadOnlyCollection<DisplayInfo> availableDisplays)
    {
        var display = availableDisplays.FirstOrDefault(x => x.Id == DisplayId)
                      ?? availableDisplays.FirstOrDefault(x => x.IsPrimary)
                      ?? availableDisplays.FirstOrDefault();

        if (display == null)
        {
            return this with
            {
                DisplayId = null
            };
        }

        var maxX = display.Width - Width;
        var maxY = display.Height - Height;

        return this with
        {
            CaptureX = Math.Clamp(CaptureX, 0, maxX),
            CaptureY = Math.Clamp(CaptureY, 0, maxY),
            Width = Math.Clamp(Width, 1, display.Width),
            Height = Math.Clamp(Height, 1, display.Height),
            FrameRate = Math.Clamp(FrameRate, 1, 100)
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