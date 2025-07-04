using SkiaSharp;

namespace Core.Diagnostics;

public enum Subsystem
{
    Unused,
    PixelCapture,
    PixelCaptureIssues,
    FrameTransmission
}

public enum DiagnosticOutputMode
{
    Normal,
    Verbose
}

public interface IDiagnosticOutputRenderer
{
    event EventHandler? OverlayChanged;
    SKBitmap? DiagnosticOverlayBitmap { get; }
    DiagnosticOutputMode Mode { set; }
    bool SetText(Subsystem part, string text, bool? alwaysDisplay = null);
}
