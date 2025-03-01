using System.Runtime.InteropServices;

namespace Core.Usb;

public class DefaultLibUsbWrapper : ILibUsbWrapper
{
    public (int, IntPtr) Init()
    {
        var result = LibUsb.Init(out var context);
        return (result, context);
    }

    public void Exit(IntPtr context) =>
        LibUsb.Exit(context);

    public IntPtr OpenDeviceWithVidPid(IntPtr context, ushort vendorId, ushort productId) =>
        LibUsb.OpenDeviceWithVidPid(context, vendorId, productId);

    public void Close(IntPtr handle) =>
        LibUsb.Close(handle);

    public int ClaimInterface(IntPtr handle, int interfaceNumber) =>
        LibUsb.ClaimInterface(handle, interfaceNumber);

    public int ReleaseInterface(IntPtr handle, int interfaceNumber) =>
        LibUsb.ReleaseInterface(handle, interfaceNumber);

    public int BulkTransfer(
        IntPtr handle,
        byte endpoint,
        ReadOnlyMemory<byte> data,
        int length,
        out int transferred,
        int timeout)
    {
        if (data.Length == 0)
        {
            transferred = 0;
            return 0;
        }

        return LibUsb.BulkTransfer(
            handle,
            endpoint,
            ref MemoryMarshal.GetReference(data.Span),
            length,
            out transferred,
            timeout);
    }

    public IntPtr LibUsbErrorName(int errorCode) =>
        LibUsb.LibUsbErrorName(errorCode);
}