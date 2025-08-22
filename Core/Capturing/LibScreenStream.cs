using System.Runtime.InteropServices;

namespace Core.Capturing;

public static partial class LibScreenStream
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

    [LibraryImport(LibraryName, EntryPoint = nameof(CheckCapturePermission))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static partial void CheckCapturePermission();

    [LibraryImport(LibraryName, EntryPoint = nameof(IsCapturePermissionGranted))]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsCapturePermissionGranted();

    [LibraryImport(LibraryName, EntryPoint = nameof(StartCapture))]
    internal static partial int StartCapture(
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

    [LibraryImport(LibraryName, EntryPoint = nameof(StopCapture))]
    internal static partial int StopCapture();

    [LibraryImport(LibraryName, EntryPoint = nameof(GetRegionBufferStats))]
    internal static partial int GetRegionBufferStats();

    [LibraryImport(LibraryName, EntryPoint = nameof(GetFullScreenBufferStats))]
    internal static partial int GetFullScreenBufferStats();

    [LibraryImport(LibraryName, EntryPoint = nameof(GetRegionFrameDropStats))]
    internal static partial int GetRegionFrameDropStats();

    [LibraryImport(LibraryName, EntryPoint = nameof(GetFullScreenFrameDropStats))]
    internal static partial int GetFullScreenFrameDropStats();

    [LibraryImport(LibraryName, EntryPoint = nameof(ResetPerformanceStats))]
    internal static partial void ResetPerformanceStats();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void WindowListCallback(IntPtr windows, int count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ApplicationListCallback(IntPtr apps, int count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ThumbnailCallback(IntPtr data, int length);

    [LibraryImport(LibraryName, EntryPoint = nameof(GetAvailableWindows))]
    internal static partial void GetAvailableWindows(WindowListCallback callback);

    [LibraryImport(LibraryName, EntryPoint = nameof(GetAvailableApplications))]
    internal static partial void GetAvailableApplications(ApplicationListCallback callback);

    [LibraryImport(LibraryName, EntryPoint = nameof(GetWindowThumbnail))]
    internal static partial void GetWindowThumbnail(int windowId, ThumbnailCallback callback);
}
