using System.Runtime.InteropServices;

namespace Core.Capturing;

public static class WinScreenStreamLib
{
    private const string LibraryName = "WinScreenStream.dll";

    [DllImport(LibraryName, CharSet = CharSet.Ansi)]
    internal static extern int GetActiveDisplays([Out] DisplayInfo[] infos, int maxCount);

    [DllImport(LibraryName, CharSet = CharSet.Ansi)]
    internal static extern int StartCapture(int displayId, int frameRate, CaptureFrameCallback cb, nint userContext);

    [DllImport(LibraryName, CharSet = CharSet.Ansi)]
    internal static extern void SetFrameRate(int frameRate);

    [DllImport(LibraryName, CharSet = CharSet.Ansi)]
    internal static extern void StopCapture();

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

        public float dpiX;
        public float dpiY;

        public int left;
        public int top;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CaptureFrameCallback(
        nint pixels,
        int width,
        int height,
        nint userContext
    );
}
