using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("Tests.GUI")]

namespace Core.Capturing;

/// <summary>
/// MacStreamer provides screen capture functionality using the native Swift library.
/// </summary>
public class MacStreamer(
    ICaptureEventSource eventSource,
    IDisplayService displayService,
    ILogger<MacStreamer> logger,
    TimeProvider timeProvider) : IStreamer, IDisposable
{
    private static LibScreenStream.CaptureCallback? RegionCaptureCallback { get; set; }
    private static LibScreenStream.CaptureCallback? FullScreenCaptureCallback { get; set; }
    private static LibScreenStream.ErrorCallback? RegionCaptureStoppedCallback { get; set; }
    private static LibScreenStream.ErrorCallback? FullScreenCaptureStoppedCallback { get; set; }

    private IDisplayService DisplayService { get; } = displayService;
    private ILogger<MacStreamer> Logger { get; } = logger;
    private TimeProvider TimeProvider { get; } = timeProvider;

    private Lock FrameLock { get; } = new();

    /// <summary>
    /// Synchronization object to prevent race conditions between manual and involuntary stops.
    /// </summary>
    private Lock StopLock { get; } = new();

    /// <summary>
    /// Flag to indicate if stop/cleanup is already in progress.
    /// </summary>
    private bool StopInProgress { get; set; }

    /// <summary>
    /// Completion source to track when the stop operation is fully completed.
    /// </summary>
    private TaskCompletionSource<bool>? StopCompletionSource { get; set; }

    /// <summary>
    /// Tracks which capture types have been stopped to ensure both region and full screen are stopped.
    /// </summary>
    private HashSet<FrameCaptureType> StoppedCaptureTypes { get; } = new();

    private int LoggedFrameFailures { get; set; }
    private ITimer? PerformanceMonitor { get; set; }

    public ICaptureEventSource EventSource { get; } = eventSource;

    private bool isCapturing;

    public bool IsCapturing
    {
        get => isCapturing;
        private set
        {
            if (isCapturing == value)
            {
                return;
            }

            isCapturing = value;
            EventSource.InvokeCaptureStateChanged(value);
        }
    }

    public async Task<bool> CheckPermissionAsync()
    {
        Logger.LogDebug("Checking capture permissions...");
        LibScreenStream.CheckCapturePermission();

        // Wait for the permission check to complete
        await Task.Delay(500);
        var hasPermission = LibScreenStream.IsCapturePermissionGranted();
        Logger.LogDebug("Permission check result: {HasPermission}", hasPermission);

        if (hasPermission == false)
        {
            Logger.LogWarning("Capture permission not granted");
        }

        return hasPermission;
    }

    public void Start(int displayId, int x, int y, int width, int height, int frameRate)
    {
        // Input validation to prevent crashes
        if (displayId < 0)
        {
            throw new ArgumentException("Display id must be non-negative", nameof(displayId));
        }

        if (x < 0 || y < 0)
        {
            throw new ArgumentException("Coordinates must be non-negative", nameof(x));
        }

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Width and height must be positive", nameof(width));
        }

        if (frameRate <= 0)
        {
            throw new ArgumentException("Frame rate must be positive", nameof(frameRate));
        }

        Logger.LogInformation(
            "Starting capture at {X}, {Y}, {Width}x{Height}, {FrameRate} FPS",
            x, y, width, height, frameRate);

        if (IsCapturing)
        {
            Logger.LogWarning("Capture is already running");
            throw new InvalidOperationException("Capture already in progress.");
        }

        // Check permissions before proceeding
        if (LibScreenStream.IsCapturePermissionGranted() == false)
        {
            Logger.LogError("Capture permission not granted - call CheckPermissionAsync first");
            throw new UnauthorizedAccessException("Screen capture permission not granted");
        }

        var display = DisplayService.GetDisplay(displayId);
        if (display == null)
        {
            Logger.LogWarning("Display {DisplayId} not found", displayId);
            throw new InvalidOperationException("Display could not be found.");
        }

        try
        {
            Logger.LogDebug("Starting capture setup...");

            // Clean up any existing buffers first - this ensures we start fresh
            // This is important for restart scenarios
            CleanupBuffers();

            // Reset frame failure counter for new capture session
            LoggedFrameFailures = 0;

            // Reset stop flag for new capture session
            lock (StopLock)
            {
                StopInProgress = false;
                StoppedCaptureTypes.Clear(); // Reset the stopped types tracking
            }

            Logger.LogDebug("Setting up callbacks...");
            var regionBufferSize = width * height * 3;
            RegionCaptureCallback = OnFrame(regionBufferSize, FrameCaptureType.Region);

            var fullScreenBufferSize = display.Width * display.Height * 3;
            FullScreenCaptureCallback = OnFrame(fullScreenBufferSize, FrameCaptureType.FullScreen);

            RegionCaptureStoppedCallback = OnStopped(FrameCaptureType.Region);
            FullScreenCaptureStoppedCallback = OnStopped(FrameCaptureType.FullScreen);

            Logger.LogDebug("Calling library StartCapture...");

            var result = LibScreenStream.StartCapture(
                displayId,
                x, y,
                width, height,
                frameRate, fullScreenFrameRate: 1,
                RegionCaptureCallback,
                FullScreenCaptureCallback,
                RegionCaptureStoppedCallback,
                FullScreenCaptureStoppedCallback);

            Logger.LogDebug("StartCapture returned with result: {Result}", result);

            if (result != 0)
            {
                CleanupBuffers(); // Clean up on failure
                Logger.LogError("Failed to start capture, error code {ErrorCode}", result);
                throw new InvalidOperationException($"Failed to start capture: {result}");
            }

            // Set capturing to true only after successful start
            IsCapturing = true;

            // Start performance monitoring
            StartPerformanceMonitoring();

            Logger.LogInformation("Started capture");
        }
        catch
        {
            CleanupBuffers(); // Ensure cleanup on any exception
            throw;
        }
    }

    public async Task StopAsync()
    {
        Logger.LogInformation("Stopping capture");

        TaskCompletionSource<bool>? completionSource = null;
        TaskCompletionSource<bool>? existingCompletionSource = null;

        lock (StopLock)
        {
            if (IsCapturing == false)
            {
                Logger.LogDebug("Was not capturing");
                return;
            }

            if (StopInProgress)
            {
                Logger.LogDebug("Stop already in progress, waiting for completion...");

                // Capture the existing completion source to wait for outside the lock
                existingCompletionSource = StopCompletionSource;
            }
            else
            {
                StopInProgress = true;
                IsCapturing = false; // Mark as not capturing immediately

                // Create completion source for this stop operation
                completionSource = new();
                StopCompletionSource = completionSource;
            }
        }

        // If another stop is in progress, wait for it
        if (existingCompletionSource != null)
        {
            await existingCompletionSource.Task;
            return;
        }

        // We are the ones doing the stop operation
        try
        {
            // Stop the native capture first, while callbacks are still valid
            // This ensures the native library can complete its shutdown process
            try
            {
                var result = LibScreenStream.StopCapture();
                if (result != 0)
                {
                    Logger.LogWarning("Non-zero result when stopping capture: {ErrorCode}", result);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception occurred while stopping native capture");
                // If native stop fails, complete the task with exception
                completionSource!.SetException(ex);
                throw;
            }

            // DON'T complete the task here - wait for the OnStopped callback
            // The task will be completed when the native library calls OnStopped callback
            Logger.LogDebug("Waiting for native stop callback confirmation...");

            // Add a timeout to prevent hanging if callback is never received
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);
            var completedTask = await Task.WhenAny(completionSource!.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                Logger.LogWarning("Timeout waiting for stop callback, proceeding with cleanup");

                // Perform cleanup and complete the task manually
                PerformInternalCleanup();

                lock (StopLock)
                {
                    StopInProgress = false;
                    StopCompletionSource = null;
                    StoppedCaptureTypes.Clear(); // Clear stopped types on timeout
                }

                Logger.LogInformation("Stop completed with timeout");
            }
            else
            {
                // Callback was received successfully
                await completionSource.Task; // Re-await to handle any exceptions
                Logger.LogInformation("Stop confirmed by native callback");
            }
        }
        catch (Exception ex) when (ex is not TaskCanceledException)
        {
            Logger.LogError(ex, "Exception during stop operation");

            // Ensure we don't leave the completion source hanging
            lock (StopLock)
            {
                if (StopCompletionSource == completionSource)
                {
                    StopInProgress = false;
                    StopCompletionSource = null;
                    StoppedCaptureTypes.Clear(); // Clear stopped types on exception
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Internal synchronous stop method for use in callbacks and disposal.
    /// This method should not be awaited as it may cause deadlocks in callback scenarios.
    /// </summary>
    private void StopInternal()
    {
        Logger.LogInformation("Stopping capture (internal)");

        lock (StopLock)
        {
            if (IsCapturing == false)
            {
                Logger.LogDebug("Was not capturing");
                return;
            }

            if (StopInProgress)
            {
                Logger.LogDebug("Stop already in progress");
                return;
            }

            StopInProgress = true;
            IsCapturing = false; // Mark as not capturing immediately
        }

        try
        {
            // Stop the native capture first, while callbacks are still valid
            // This ensures the native library can complete its shutdown process
            try
            {
                var result = LibScreenStream.StopCapture();
                if (result != 0)
                {
                    Logger.LogWarning("Non-zero result when stopping capture: {ErrorCode}", result);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Exception occurred while stopping native capture");
            }

            // Now it's safe to perform cleanup, but do NOT clear callbacks here
            PerformInternalCleanup();

            Logger.LogInformation("Stopped capture");
        }
        finally
        {
            lock (StopLock)
            {
                StopInProgress = false;
            }
        }
    }

    private Dictionary<FrameCaptureType, byte[]> Buffers { get; } = new();

    private LibScreenStream.CaptureCallback OnFrame(int bufferSize, FrameCaptureType type)
    {
        if (Buffers.ContainsKey(type))
        {
            throw new InvalidOperationException("Capture already in progress.");
        }

        var frameBuffer = Buffers[type] = ArrayPool<byte>.Shared.Rent(bufferSize);

        return (data, length) =>
        {
            // Early exit checks to avoid processing frames when not needed
            if (IsCapturing == false)
            {
                return; // Ignore frames if not capturing
            }

            if (length <= 0 || data == IntPtr.Zero)
            {
                return;
            }

            if (length > bufferSize)
            {
                Logger.LogWarning("Frame size {Length} exceeds buffer size {BufferSize}, dropping frame", length,
                    bufferSize);
                return;
            }

            // Validate that the frame buffer is still valid before proceeding
            if (Buffers.TryGetValue(type, out var currentBuffer) == false || currentBuffer != frameBuffer)
            {
                Logger.LogWarning("Frame buffer mismatch for {Type}, dropping frame", type);
                return;
            }

            lock (FrameLock)
            {
                try
                {
                    // Final safety check inside the lock - capture might have stopped
                    if (IsCapturing == false || Buffers.ContainsKey(type) == false)
                    {
                        return;
                    }

                    Marshal.Copy(data, frameBuffer, 0, length);
                    EventSource.InvokeFrameCaptured(type, frameBuffer.AsSpan(0, length));
                }
                catch (Exception ex)
                {
                    if (LoggedFrameFailures++ % 300 == 0)
                    {
                        Logger.LogError(
                            ex,
                            "Failed to capture frame ({Count} failures) for {Type}",
                            LoggedFrameFailures, type);
                    }
                }
            }
        };
    }

    public void TriggerCaptureFailure()
    {
        if (RegionCaptureStoppedCallback == null)
        {
            return; // Ignore if not initialized
        }

        // Trigger RegionCaptureStoppedCallback with IntPtr pointing to a simulated error
        // This is used to simulate a failure in the native library
        var error = new LibScreenStream.ScreenStreamError
        {
            code = -1,
            domain = Marshal.StringToHGlobalAnsi("TestDomain"),
            description = Marshal.StringToHGlobalAnsi("TestDescription")
        };
        var errorPtr = Marshal.AllocHGlobal(Marshal.SizeOf<LibScreenStream.ScreenStreamError>());
        Marshal.StructureToPtr(error, errorPtr, false);
        RegionCaptureStoppedCallback(errorPtr);
    }

    // Stop Handling Approach:
    // - Manual stops (via Stop() method) call native StopCapture first, then cleanup, preventing callbacks during cleanup
    // - Involuntary stops (from native error callbacks) are handled synchronously to avoid deadlocks
    // - stopInProgress flag prevents conflicts between manual and involuntary stops
    // - Only involuntary stops notify listeners (for restart capability)
    // - Listeners can safely restart capture from involuntary stop events
    private LibScreenStream.ErrorCallback OnStopped(FrameCaptureType type) => errorPtr =>
    {
        try
        {
            CaptureStoppedEvent? stoppedEvent;

            if (errorPtr == IntPtr.Zero)
            {
                Logger.LogInformation("Capture stopped normally for {Type}", type);
                stoppedEvent = new(0, "Normal stop", StopReason.Voluntary);
            }
            else
            {
                try
                {
                    var error = Marshal.PtrToStructure<LibScreenStream.ScreenStreamError>(errorPtr);
                    var domain = error.domain != IntPtr.Zero ? Marshal.PtrToStringAnsi(error.domain) : "Unknown";
                    var description = error.description != IntPtr.Zero
                        ? Marshal.PtrToStringAnsi(error.description)
                        : "No description";

                    Logger.LogWarning(
                        "Capture stopped with error for {Type}: Code={Code}, Domain={Domain}, Description={Description}",
                        type, error.code, domain, description);

                    var reason = error.code switch
                    {
                        -3817 => StopReason.Voluntary,
                        _ => StopReason.Unknown
                    };

                    stoppedEvent = new(error.code, description ?? "Unknown error", reason);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to parse error structure for {Type}", type);
                    stoppedEvent = new(-1, "Failed to parse error", StopReason.Unknown);
                }
            }

            // Handle stops differently based on whether this is a manual stop or involuntary
            // Manual stop = we called StopAsync() and are waiting for both streams to stop
            // Involuntary stop = external event (system menu, permissions, error) that we need to handle immediately
            bool isManualStop;
            lock (StopLock)
            {
                isManualStop = StopInProgress;
            }

            if (isManualStop)
            {
                Logger.LogInformation("Manual stop callback received for {Type}", type);

                // For manual stops, track which capture types have stopped
                TaskCompletionSource<bool>? pendingCompletion = null;
                var shouldComplete = false;

                lock (StopLock)
                {
                    if (StopInProgress && StopCompletionSource != null)
                    {
                        StoppedCaptureTypes.Add(type);

                        // Only complete when both Region and FullScreen have stopped
                        if (StoppedCaptureTypes.Contains(FrameCaptureType.Region) &&
                            StoppedCaptureTypes.Contains(FrameCaptureType.FullScreen))
                        {
                            pendingCompletion = StopCompletionSource;
                            StopCompletionSource = null;
                            StopInProgress = false;
                            shouldComplete = true;
                            StoppedCaptureTypes.Clear();
                        }
                    }
                }

                // Only perform cleanup and complete task when both streams have stopped
                if (shouldComplete)
                {
                    // Perform cleanup now that we've received both stop callbacks
                    PerformInternalCleanup();

                    // Complete the pending StopAsync task
                    if (pendingCompletion != null)
                    {
                        Logger.LogDebug("Completing pending stop task after both streams stopped");
                        pendingCompletion.SetResult(true);
                    }
                }
                else
                {
                    Logger.LogDebug("Waiting for other stream to stop before completing stop task");
                }

                return; // Don't process further for manual stops
            }

            // This is an involuntary stop (system menu, permission loss, error, etc.)
            // Handle it immediately regardless of the stop reason
            var shouldNotifyListeners = false;
            lock (StopLock)
            {
                if (IsCapturing)
                {
                    // This is an involuntary stop - mark it and proceed with cleanup and notification immediately
                    IsCapturing = false;
                    shouldNotifyListeners = true;
                }
            }

            if (shouldNotifyListeners)
            {
                // For involuntary stops, we handle them immediately for the specific stream
                // This includes system menu stops (-3817), permission losses, and other errors
                Logger.LogWarning("Involuntary stop detected for {Type} (reason: {Reason}), handling immediately",
                    type, stoppedEvent.Reason);

                try
                {
                    // For external voluntary stops (like system menu), we need to stop the entire capture
                    // to ensure both streams are stopped, not just the one that was stopped externally
                    if (stoppedEvent.Reason == StopReason.Voluntary)
                    {
                        Logger.LogInformation("External voluntary stop detected, stopping entire capture session");

                        // Stop the native capture to ensure both streams are stopped
                        try
                        {
                            var result = LibScreenStream.StopCapture();
                            if (result != 0)
                            {
                                Logger.LogWarning("Non-zero result when stopping capture after external stop: {ErrorCode}", result);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Exception occurred while stopping capture after external voluntary stop");
                        }
                    }

                    PerformInternalCleanup();

                    // Notify listeners on a background thread to avoid blocking the native callback
                    Task.Run(() =>
                    {
                        try
                        {
                            EventSource.InvokeCaptureStopped(type, stoppedEvent);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Failed to notify listeners of capture stopped for {Type}", type);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to handle involuntary capture stop for {Type}", type);
                }
            }
            else
            {
                Logger.LogDebug("Capture stop callback ignored - already stopped");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception in error callback for {Type}", type);
        }
    };

    private void CleanupBuffers()
    {
        if (Buffers.Count <= 0)
        {
            return;
        }

        Logger.LogDebug("Returning {Count} buffers to pool", Buffers.Count);

        foreach (var (type, buffer) in Buffers)
        {
            try
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to return buffer for {Type}", type);
            }
        }

        Buffers.Clear();
    }

    private void StartPerformanceMonitoring()
    {
        PerformanceMonitor?.Dispose();
        PerformanceMonitor =
            TimeProvider.CreateTimer(
                CheckPerformance, null,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private void CheckPerformance(object? state)
    {
        try
        {
            // Get performance metrics from the Swift library
            var regionBuffers = LibScreenStream.GetRegionBufferStats();
            var fullScreenBuffers = LibScreenStream.GetFullScreenBufferStats();
            var regionDropRate = LibScreenStream.GetRegionFrameDropStats() / 100.0;
            var fullScreenDropRate = LibScreenStream.GetFullScreenFrameDropStats() / 100.0;

            if (regionBuffers > 10 || regionDropRate > 5.0)
            {
                Logger.LogWarning(
                    "Region capture performance issue: {BufferCount} outstanding buffers, {DropRate:F1}% drop rate",
                    regionBuffers, regionDropRate);
            }

            if (fullScreenBuffers > 10 || fullScreenDropRate > 5.0)
            {
                Logger.LogWarning(
                    "Full screen capture performance issue: {BufferCount} outstanding buffers, {DropRate:F1}% drop rate",
                    fullScreenBuffers, fullScreenDropRate);
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Performance monitoring failed");
        }
    }

    /// <summary>
    /// Resets performance statistics in the Swift library
    /// </summary>
    private void ResetPerformanceStats()
    {
        try
        {
            LibScreenStream.ResetPerformanceStats();
            Logger.LogDebug("Performance statistics reset");
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to reset performance statistics");
        }
    }

    /// <summary>
    /// Performs internal cleanup without calling the public Stop() method.
    /// This is used when capture stops involuntarily to avoid conflicts with restart attempts.
    /// </summary>
    private void PerformInternalCleanup()
    {
        try
        {
            Logger.LogDebug("Performing internal cleanup");

            // Stop performance monitoring
            PerformanceMonitor?.Dispose();
            PerformanceMonitor = null;

            // Reset performance statistics
            ResetPerformanceStats();

            // Clean up buffers
            CleanupBuffers();

            // For singleton: never clear static callbacks, always keep them alive for native interop safety
            // (No-op: do not set RegionCaptureCallback, etc. to null)

            Logger.LogDebug("Internal cleanup completed");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception during internal cleanup");
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            StopInternal();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception during disposal");
        }

        PerformanceMonitor?.Dispose();
    }
}
