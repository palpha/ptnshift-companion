namespace Core.Capturing;

public interface ICaptureService : IDisposable
{
    event Action<ReadOnlySpan<byte>> FrameCaptured;
    
    bool IsCapturing { get; }

    Task<bool> CheckCapturePermissionAsync();
    void SetConfiguration(CaptureConfiguration configuration);
    void StartCapture();
    void StopCapture();
}