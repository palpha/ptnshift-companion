using System.Runtime.InteropServices;

namespace Core.Capturing;

public static class LibScreenStream
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ScreenStreamError
    {
        public int code;
        public IntPtr domain; // char* (C string)
        public IntPtr description; // char* (C string)
    }

    private const string LibraryName = "libscreenstream.dylib";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void CaptureCallback(nint data, int length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ErrorCallback(IntPtr errorPtr);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CheckCapturePermission")]
    internal static extern void CheckCapturePermission();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IsCapturePermissionGranted")]
    internal static extern bool IsCapturePermissionGranted();

    [DllImport(LibraryName, EntryPoint = "StartCapture")]
    internal static extern int StartCapture(
        int displayId,
        int x, int y,
        int width, int height,
        int frameRate,
        int fullScreenFrameRate,
        CaptureCallback regionCallback,
        CaptureCallback fullScreenCallback,
        ErrorCallback regionStoppedCallback,
        ErrorCallback fullScreenStoppedCallback
    );

    [DllImport(LibraryName, EntryPoint = "StopCapture")]
    internal static extern int StopCapture();

    [DllImport(LibraryName, EntryPoint = "GetRegionBufferStats")]
    public static extern int GetRegionBufferStats();

    [DllImport(LibraryName, EntryPoint = "GetFullScreenBufferStats")]
    public static extern int GetFullScreenBufferStats();

    [DllImport(LibraryName, EntryPoint = "GetRegionFrameDropStats")]
    public static extern int GetRegionFrameDropStats();

    [DllImport(LibraryName, EntryPoint = "GetFullScreenFrameDropStats")]
    public static extern int GetFullScreenFrameDropStats();

    [DllImport(LibraryName, EntryPoint = "ResetPerformanceStats")]
    public static extern void ResetPerformanceStats();
}
