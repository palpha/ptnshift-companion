namespace Core.Usb;

public interface ILibUsbWrapper
{
    (int Result, IntPtr Context) Init();
    void Exit(IntPtr context);
    IntPtr OpenDeviceWithVidPid(IntPtr context, ushort vendorId, ushort productId);
    void Close(IntPtr handle);
    int ClaimInterface(IntPtr handle, int interfaceNumber);
    int ReleaseInterface(IntPtr handle, int interfaceNumber);

    int BulkTransfer(
        IntPtr handle,
        byte endpoint,
        ReadOnlyMemory<byte> data,
        int length,
        out int transferred,
        int timeout);
    
    IntPtr LibUsbErrorName(int errorCode);
}