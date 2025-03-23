namespace Core.Usb;

public interface IPush2Usb : IDisposable
{
    bool IsConnected { get; }
    bool Connect();
    void SendFrame(ReadOnlySpan<byte> rgbFrame);
    void Disconnect(bool? force = null);
}