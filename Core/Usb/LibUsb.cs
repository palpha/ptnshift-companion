using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Core.Usb;

[SuppressMessage(
    "Interoperability",
    "SYSLIB1054:Use \'LibraryImportAttribute\' instead of \'DllImportAttribute\' to generate P/Invoke marshalling code at compile time")]
public static class LibUsb
{
    private const string LibUsbLibrary = "libusb.dylib";

    [DllImport(LibUsbLibrary, EntryPoint = "libusb_init")]
    internal static extern int Init(out IntPtr context);

    [DllImport(LibUsbLibrary, EntryPoint = "libusb_exit")]
    internal static extern void Exit(IntPtr context);

    [DllImport(LibUsbLibrary, EntryPoint = "libusb_open_device_with_vid_pid")]
    internal static extern IntPtr OpenDeviceWithVidPid(IntPtr context, ushort vendorId, ushort productId);

    [DllImport(LibUsbLibrary, EntryPoint = "libusb_close")]
    internal static extern void Close(IntPtr handle);

    [DllImport(LibUsbLibrary, EntryPoint = "libusb_claim_interface")]
    internal static extern int ClaimInterface(IntPtr handle, int interfaceNumber);

    [DllImport(LibUsbLibrary, EntryPoint = "libusb_release_interface")]
    internal static extern int ReleaseInterface(IntPtr handle, int interfaceNumber);

    [DllImport(LibUsbLibrary, EntryPoint = "libusb_bulk_transfer")]
    internal static extern int BulkTransfer(
        IntPtr handle,
        byte endpoint,
        ref byte data,
        int length,
        out int transferred,
        int timeout);

    [DllImport(LibUsbLibrary, EntryPoint = "libusb_error_name")]
    internal static extern IntPtr LibUsbErrorName(int errorCode);
}