using System.Runtime.InteropServices;

namespace Core.Image;

public static class WinScreenStreamLib
{
    private const string LibraryName = "WinScreenStream.dll";

    [DllImport(LibraryName, CharSet = CharSet.Ansi)]
    internal static extern int GetActiveDisplays([Out] DisplayInfo[] infos, int maxCount);

    [DllImport(LibraryName, CharSet = CharSet.Ansi)]
    internal static extern int StartCapture(int displayId, CaptureFrameCallback cb, IntPtr userContext);

    [DllImport(LibraryName, CharSet = CharSet.Ansi)]
    internal static extern void StopCapture();

    // Cleanup
    [DllImport(LibraryName, CharSet = CharSet.Ansi)]
    internal static extern void Cleanup();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DisplayInfo
    {
        public int id;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string name;

        public int width;
        public int height;

        [MarshalAs(UnmanagedType.U1)]
        public bool isPrimary;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CaptureFrameCallback(
        IntPtr pixels,
        int width,
        int height,
        IntPtr userContext
    );
}