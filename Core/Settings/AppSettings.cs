namespace Core.Settings;

public record AppSettings(
    int? SelectedDisplayId = null,
    int CaptureX = 400,
    int CaptureY = 1000,
    int CaptureFrameRate = 24,
    int PreviewFrameRate = 12,
    bool IsPreviewEnabled = true,
    bool IsAutoLocateEnabled = true,
    bool IsVerboseOutput = false);
