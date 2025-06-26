using System.Collections.Concurrent;
using SkiaSharp;

namespace Core.Diagnostics;

public enum DiagnosticTextPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public record DiagnosticText(string Value, bool AlwaysDisplay);

public class DiagnosticOutputRenderer : IDiagnosticOutputRenderer
{
    private const int Width = 960;
    private const int Height = 160;

    private ConcurrentDictionary<Subsystem, DiagnosticText> Texts { get; } = new();
    private SKFont Font { get; } = new(SKTypeface.Default, 16);

    private SKPaint Paint { get; } = new()
    {
        Color = SKColors.White.WithAlpha(170),
        IsAntialias = true
    };

    private static IReadOnlyDictionary<Subsystem, DiagnosticTextPosition> SubsystemPositions { get; } =
        new Dictionary<Subsystem, DiagnosticTextPosition>
        {
            [Subsystem.Unused] = DiagnosticTextPosition.TopLeft,
            [Subsystem.PixelCapture] = DiagnosticTextPosition.TopRight,
            [Subsystem.PixelCaptureIssues] = DiagnosticTextPosition.BottomLeft,
            [Subsystem.FrameTransmission] = DiagnosticTextPosition.BottomRight
        };

    private static IReadOnlyDictionary<
        DiagnosticTextPosition,
        (Func<float, float, (float x, float y)> Position, SKTextAlign Align)> PositionMappings { get; } =
        new Dictionary<DiagnosticTextPosition, (Func<float, float, (float, float)>, SKTextAlign)>
        {
            [DiagnosticTextPosition.TopLeft] = ((w, h) => (170, 60), SKTextAlign.Left),
            [DiagnosticTextPosition.TopRight] = ((w, h) => (w - 5, 60), SKTextAlign.Right),
            [DiagnosticTextPosition.BottomLeft] = ((w, h) => (170, h - 36), SKTextAlign.Left),
            [DiagnosticTextPosition.BottomRight] = ((w, h) => (w - 5, h - 36), SKTextAlign.Right)
        };

    public event EventHandler? OverlayChanged;

    public SKBitmap DiagnosticOverlayBitmap { get; } =
        new(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);

    public DiagnosticOutputMode Mode { private get; set; }

    public bool SetText(Subsystem subsystem, string text, bool? alwaysDisplay)
    {
        Texts[subsystem] = new(text, alwaysDisplay == true);
        if (Redraw())
        {
            OverlayChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    private bool Redraw()
    {
        using var canvas = new SKCanvas(DiagnosticOverlayBitmap);
        canvas.Clear(SKColors.Transparent);
        var changed = false;

        foreach (var (subsystem, text) in Texts)
        {
            if (SubsystemPositions.TryGetValue(subsystem, out var posEnum) == false)
            {
                continue;
            }

            if (PositionMappings.TryGetValue(posEnum, out var posConf) == false)
            {
                continue;
            }

            if (Mode == DiagnosticOutputMode.Normal && text.AlwaysDisplay == false)
            {
                continue;
            }

            var (posFunc, align) = posConf;
            var (x, y) = posFunc(Width, Height);
            var effectiveText = text.Value.ToUpper();

            canvas.DrawText(effectiveText, x, y, align, Font, Paint);
            changed = true;
        }

        return changed;
    }
}
