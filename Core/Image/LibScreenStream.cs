using System.Runtime.InteropServices;

namespace Core.Image;

public static class LibScreenStream
{
    private const string LibraryName = "libscreenstream.dylib";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void DisplayInfoCallback(IntPtr displayInfos, int count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void CaptureCallback(IntPtr data, int length);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CheckCapturePermission")]
    internal static extern void CheckCapturePermission();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IsCapturePermissionGranted")]
    internal static extern bool IsCapturePermissionGranted();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetAvailableDisplays")]
    internal static extern int GetAvailableDisplays(DisplayInfoCallback callback);

    [DllImport(LibraryName, EntryPoint = "StartCapture")]
    internal static extern int StartCapture(
        int displayId,
        int x, int y,
        int width, int height,
        int frameRate,
        CaptureCallback callback
    );

    [DllImport(LibraryName, EntryPoint = "StopCapture")]
    internal static extern int StopCapture();

    [StructLayout(LayoutKind.Sequential)]
    public struct DisplayInfo
    {
        public int Id;
        public int Width;
        public int Height;
        public bool IsMain;
    }
}