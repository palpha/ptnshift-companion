using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Core.Capturing;

public class MacCapturableWindowLister(ILogger<MacCapturableWindowLister> logger) : ICapturableWindowLister
{
    private static readonly ConcurrentDictionary<int, object> ActiveCallbacks = new();
    private static int callbackIdCounter;

    [StructLayout(LayoutKind.Sequential)]
    private struct AppInfo
    {
        public int processId;
        public IntPtr name; // char* (C string)
        public IntPtr bundleIdentifier; // char* (C string)
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinInfo
    {
        public int windowId;
        public int processId;
        public IntPtr title; // char* (C string)
        public IntPtr applicationName; // char* (C string)
        public int width;
        public int height;
        public int layer;
        public float alpha;
        public int isOnScreen; // 0 = false, 1 = true
    }

    private ILogger<MacCapturableWindowLister> Logger { get; } = logger;

    public async Task<byte[]?> GetThumbnailAsync(int windowId) =>
        await ExecuteWithCallbackAsync<byte[], LibScreenStream.ThumbnailCallback>(
            CopyAndFreeThumbnail,
            callback => LibScreenStream.GetWindowThumbnail(windowId, callback));

    public async Task<IEnumerable<ApplicationInfo>> ListApplicationsAsync() =>
        await ExecuteWithCallbackAsync<ApplicationInfo[], LibScreenStream.ApplicationListCallback>(
            ConvertAndFreeApplications,
            LibScreenStream.GetAvailableApplications);

    public async Task<IEnumerable<WindowInfo>> ListWindowsAsync() =>
        await ExecuteWithCallbackAsync<WindowInfo[], LibScreenStream.WindowListCallback>(
            ConvertAndFreeWindows,
            LibScreenStream.GetAvailableWindows);

    private static TCallback CreateTypedCallback<TCallback>(Action<IntPtr, int> callback)
        where TCallback : Delegate =>
        (TCallback) Delegate.CreateDelegate(typeof(TCallback), callback.Target, callback.Method);

    private static TCallback CreateCallbackWithErrorHandling<TResult, TCallback>(
        TaskCompletionSource<TResult> tcs,
        int callbackId,
        Func<IntPtr, int, TResult> dataProcessor)
        where TCallback : Delegate
    {
        return CreateTypedCallback<TCallback>((data, length) =>
        {
            try
            {
                tcs.SetResult(dataProcessor(data, length));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            finally
            {
                RemoveCallback(callbackId);
            }
        });
    }

    private static async Task<T> RunCallbackAsync<T>(Action<TaskCompletionSource<T>, int> setupCallback)
    {
        var tcs = new TaskCompletionSource<T>();
        var callbackId = Interlocked.Increment(ref callbackIdCounter);
        setupCallback(tcs, callbackId);
        try
        {
            return await tcs.Task;
        }
        catch
        {
            RemoveCallback(callbackId);
            if (typeof(T).IsArray)
            {
                return (T) (object) Array.Empty<object>();
            }

            return default!;
        }
    }

    private static async Task<TResult> ExecuteWithCallbackAsync<TResult, TCallback>(
        Func<IntPtr, int, TResult> dataProcessor,
        Action<TCallback> nativeCall)
        where TCallback : Delegate
    {
        return await RunCallbackAsync<TResult>((tcs, callbackId) =>
        {
            var callback = CreateCallbackWithErrorHandling<TResult, TCallback>(
                tcs, callbackId, dataProcessor);
            RegisterCallback(callbackId, callback);
            nativeCall(callback);
        });
    }

    private static void RegisterCallback(int callbackId, object callback)
    {
        ActiveCallbacks[callbackId] = callback;
    }

    private static void RemoveCallback(int callbackId)
    {
        ActiveCallbacks.TryRemove(callbackId, out _);
    }

    private byte[] CopyAndFreeThumbnail(IntPtr data, int length)
    {
        if (data != IntPtr.Zero && length is > 0 and < 100_000_000)
        {
            Logger.LogDebug("[Copy] Thumbnail pointer: 0x{Data:X}, length: {Length}", data, length);
            var thumbnailData = new byte[length];
            Marshal.Copy(data, thumbnailData, 0, length);
            Logger.LogDebug("[Free] Thumbnail pointer: 0x{Data:X}", data);
            Marshal.FreeHGlobal(data);
            return thumbnailData;
        }

        if (data == IntPtr.Zero)
        {
            Logger.LogDebug("[Skip Copy/Free] Thumbnail pointer is null.");
        }
        else if (length <= 0)
        {
            Logger.LogDebug("[Skip Copy/Free] Thumbnail length is not positive: {Length}", length);
        }
        else
        {
            Logger.LogDebug("[Skip Copy/Free] Thumbnail length is suspiciously large: {Length}", length);
        }

        return [];
    }

    private ApplicationInfo[] ConvertAndFreeApplications(IntPtr appsPtr, int count)
    {
        var result = new ApplicationInfo[count];
        var size = Marshal.SizeOf<AppInfo>();
        var pointersToFree = new List<IntPtr>();

        for (var i = 0; i < count; i++)
        {
            var itemPtr = IntPtr.Add(appsPtr, i * size);
            var appInfo = Marshal.PtrToStructure<AppInfo>(itemPtr);
            var name = Marshal.PtrToStringUTF8(appInfo.name) ?? "";
            var bundleId = appInfo.bundleIdentifier != IntPtr.Zero
                ? Marshal.PtrToStringUTF8(appInfo.bundleIdentifier)
                : null;
            result[i] = new(appInfo.processId, name, bundleId);

            if (appInfo.name != IntPtr.Zero)
            {
                pointersToFree.Add(appInfo.name);
            }

            if (appInfo.bundleIdentifier != IntPtr.Zero)
            {
                pointersToFree.Add(appInfo.bundleIdentifier);
            }
        }

        FreePointers(pointersToFree);

        return result;
    }

    private WindowInfo[] ConvertAndFreeWindows(IntPtr windowsPtr, int count)
    {
        var result = new WindowInfo[count];
        var size = Marshal.SizeOf<WinInfo>();
        var pointersToFree = new List<IntPtr>();
        var freedPointers = new HashSet<IntPtr>();

        for (var i = 0; i < count; i++)
        {
            var itemPtr = IntPtr.Add(windowsPtr, i * size);
            var nativeWindow = Marshal.PtrToStructure<WinInfo>(itemPtr);
            var title = nativeWindow.title != IntPtr.Zero
                ? Marshal.PtrToStringUTF8(nativeWindow.title) ?? ""
                : "";
            var applicationName = nativeWindow.applicationName != IntPtr.Zero
                ? Marshal.PtrToStringUTF8(nativeWindow.applicationName) ?? ""
                : "";

            // Classify window kind based on layer, title, and other metadata
            var kind = nativeWindow.layer switch
            {
                0 when nativeWindow.isOnScreen != 0 && nativeWindow.alpha > 0.9f => WindowKind.Normal,
                > 0 when title == "Menubar" => WindowKind.Menubar,
                > 0 when applicationName == "Dock" => WindowKind.Dock,
                < 0 => WindowKind.Desktop,
                > 0 => WindowKind.System,
                _ => WindowKind.Unknown
            };

            result[i] = new(nativeWindow.windowId, nativeWindow.processId, title, applicationName,
                nativeWindow.width, nativeWindow.height, nativeWindow.layer, nativeWindow.alpha,
                nativeWindow.isOnScreen != 0, kind);

            AddPointerToFree(nativeWindow.title, "window title", pointersToFree, freedPointers);
            AddPointerToFree(nativeWindow.applicationName, "applicationName", pointersToFree, freedPointers);
        }

        FreePointers(pointersToFree);

        return result;
    }

    private void AddPointerToFree(IntPtr pointer, string pointerName, List<IntPtr> pointersToFree,
        HashSet<IntPtr> freedPointers)
    {
        if (pointer == IntPtr.Zero || freedPointers.Contains(pointer))
        {
            return;
        }

        Logger.LogDebug(
            "[Free] {PointerName} pointer: 0x{Pointer:X}",
            pointerName, pointer);
        pointersToFree.Add(pointer);
        freedPointers.Add(pointer);
    }

    private void FreePointers(List<IntPtr> pointersToFree)
    {
        foreach (var ptr in pointersToFree)
        {
            if (ptr == IntPtr.Zero)
            {
                Logger.LogDebug("[Skip Free] Attempted to free null pointer.");
                continue;
            }

            Logger.LogDebug("[Free] Calling Marshal.FreeHGlobal on pointer: 0x{Ptr:X}", ptr);
            Marshal.FreeHGlobal(ptr);
        }
    }
}
