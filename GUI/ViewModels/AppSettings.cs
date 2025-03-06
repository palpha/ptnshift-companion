namespace GUI.ViewModels;

public record AppSettings(
    int? SelectedDisplayId = null,
    int CaptureX = 400,
    int CaptureY = 1000,
    int CaptureFrameRate = 30,
    bool IsPreviewEnabled = true);