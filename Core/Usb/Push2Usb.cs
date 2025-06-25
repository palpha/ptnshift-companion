using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Core.Capturing;
using Core.Diagnostics;
using Core.Image;
using Microsoft.Extensions.Logging;

namespace Core.Usb;

public sealed class Push2Usb : IPush2Usb
{
    private const int ChunkSize = 512 * 128; // 40960; // 512 * 64;
    private const int OverlayWidth = 960;
    private const int OverlayHeight = 160;

    internal static ReadOnlyMemory<byte> FrameHeader { get; } = new([
        0xFF, 0xCC, 0xAA, 0x88,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00
    ]);

    private PushIdentity Identity { get; set; } = PushIdentity.Push2;

    private ILogger<Push2Usb> Logger { get; }
    private IStreamer Streamer { get; }
    private IImageConverter ImageConverter { get; }
    private ILibUsbWrapper LibUsbWrapper { get; }
    private TimeProvider TimeProvider { get; }

    private Lock SyncRoot { get; } = new();
    private Lock BufferLock { get; } = new();
    private byte[] CroppedFrameBuffer { get; } = new byte[960 * 160 * 3];
    private byte[] LastCroppedRgbFrame { get; } = new byte[960 * 160 * 3];
    private volatile bool isFrameBeingSent;

    // Send buffer and conversion buffer are swapped to prevent buffer overrun
    private byte[] SendBuffer { get; set; } = new byte[2048 * 160];
    private byte[] ConversionBuffer { get; set; } = new byte[2048 * 160];

    private IntPtr UsbContext { get; set; }
    private IntPtr PushDevice { get; set; }
    private bool IsDisposed { get; set; }
    private int ConsecutiveErrors { get; set; }
    private long LastFrameTimestamp { get; set; }
    private ITimer FrameCheckTimer { get; set; }

    private IDiagnosticOutputRenderer DiagnosticOutputRenderer { get; }

    public bool IsConnected { get; private set; }

    public Push2Usb(
        ILogger<Push2Usb> logger,
        IStreamer streamer,
        IImageConverter imageConverter,
        ILibUsbWrapper libUsbWrapper,
        TimeProvider timeProvider,
        IDiagnosticOutputRenderer diagnosticOutputRenderer)
    {
        Logger = logger;
        Streamer = streamer;
        ImageConverter = imageConverter;
        LibUsbWrapper = libUsbWrapper;
        TimeProvider = timeProvider;
        DiagnosticOutputRenderer = diagnosticOutputRenderer;

        DiagnosticOutputRenderer.OverlayChanged += OnOverlayChanged;

        DoMeasure = Logger.IsEnabled(LogLevel.Trace);
        if (DoMeasure)
        {
            Logger.LogInformation("Will output measurements");
        }
        else
        {
            Logger.LogInformation("No measurements");
        }

        StartFrameCheck();
    }

    public bool Connect()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (IsConnected)
        {
            return true;
        }

        // Initialize libusb
        var (initResult, ctx) = LibUsbWrapper.Init();
        UsbContext = ctx;
        if (initResult < 0)
        {
            Logger.LogError(
                "Failed to initialize libusb. Error: {Message}",
                GetLibUsbErrorMessage(initResult));
            return false;
        }

        // Open Push 2
        PushDevice = LibUsbWrapper.OpenDeviceWithVidPid(UsbContext, Identity.VendorId, Identity.ProductId);
        if (PushDevice == IntPtr.Zero)
        {
            // Try Push 3
            Identity = PushIdentity.Push3;
            PushDevice = LibUsbWrapper.OpenDeviceWithVidPid(UsbContext, Identity.VendorId, Identity.ProductId);
            if (PushDevice == IntPtr.Zero)
            {
                Identity = PushIdentity.Push2;
                Logger.LogError("Unable to find Push 2/3. Is it connected?");
                return false;
            }

            Logger.LogInformation("Push 3 connected");
        }
        else
        {
            Logger.LogInformation("Push 2 connected");
        }

        // Claim the interface for display communication
        var claimResult = LibUsbWrapper.ClaimInterface(PushDevice, Identity.DisplayInterface);
        if (claimResult < 0)
        {
            Logger.LogError(
                "Claim interface failed: {Message}",
                GetLibUsbErrorMessage(claimResult));
            Disconnect(force: true);
            return false;
        }

        Streamer.EventSource.RegionFrameCaptured += OnRegionFrameReceived;

        DiagnosticOutputRenderer.SetText(Subsystem.FrameTransmission, "Connected", alwaysDisplay: true);
        return IsConnected = true;
    }

    private void OnRegionFrameReceived(ReadOnlySpan<byte> bgraBytes)
    {
        LastFrameTimestamp = TimeProvider.GetTimestamp();
        SendFrame(bgraBytes);
    }

    private bool HasReceivedRecentFrame() => TimeProvider.GetElapsedTime(LastFrameTimestamp).TotalSeconds <= 1;

    [MemberNotNull(nameof(FrameCheckTimer))]
    private void StartFrameCheck()
    {
        FrameCheckTimer = TimeProvider.CreateTimer(_ =>
        {
            try
            {
                if (IsConnected == false)
                {
                    return;
                }

                var now = TimeProvider.GetLocalNow();
                var nowStr = $" ({now.ToString("u").Replace("Z", "")})";

                if (HasReceivedRecentFrame())
                {
                    DiagnosticOutputRenderer.SetText(Subsystem.FrameTransmission, $"Streaming{nowStr}");
                    return;
                }

                if (SeenFrames > 0)
                {
                    if (DiagnosticOutputRenderer.SetText(Subsystem.FrameTransmission, $"Resending{nowStr}") == false)
                    {
                        // If the text was not updated, it means the overlay is not visible
                        SendSendBuffer();
                    }

                    return;
                }

                DiagnosticOutputRenderer.SetText(Subsystem.FrameTransmission, "Connected", true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in frame check timer");
                DiagnosticOutputRenderer.SetText(Subsystem.FrameTransmission, "Error");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    public void Disconnect(bool? force = null)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        Streamer.EventSource.RegionFrameCaptured -= OnRegionFrameReceived;

        if (force != true && IsConnected == false)
        {
            throw new InvalidOperationException("Cannot disconnect, not connected.");
        }

        if (PushDevice != IntPtr.Zero)
        {
            lock (SyncRoot)
            {
                var releaseResult = LibUsbWrapper.ReleaseInterface(PushDevice, Identity.DisplayInterface);
                if (releaseResult != 0)
                {
                    Logger.LogError(
                        "Failed to release interface: {Message}",
                        GetLibUsbErrorMessage(releaseResult));
                }

                LibUsbWrapper.Close(PushDevice);
                PushDevice = IntPtr.Zero;
            }
        }

        LibUsbWrapper.Exit(UsbContext);
        Logger.LogInformation("Disconnected from Push 2");
        IsConnected = false;
    }

    private int SkippedFrames { get; set; }
    private int SeenFrames { get; set; }

    public void SendFrame(ReadOnlySpan<byte> rgbFrame)
    {
        SeenFrames++;

        if (SkippedFrames % 1000 == 1)
        {
            Logger.LogInformation("Skipped {SkippedFrames} frames out of {SeenFrames}", SkippedFrames, SeenFrames);
        }

        if (isFrameBeingSent)
        {
            SkippedFrames++;
            Logger.LogDebug("Skipping frame to prevent buffer overrun");
            return;
        }

        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (ConsecutiveErrors >= 3)
        {
            Logger.LogError("Too many errors! Stopping transmission.");
            return;
        }

        try
        {
            lock (BufferLock)
            {
                Debug.Assert(rgbFrame.Length >= 960 * 161 * 3);
                var croppedFrame = rgbFrame[(960 * 3)..];
                croppedFrame.CopyTo(LastCroppedRgbFrame);
                ConvertBuffer(croppedFrame);
            }

            SendSendBuffer();

            ConsecutiveErrors = 0; // Reset on success
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not send frame");
            ConsecutiveErrors++;
        }
    }

    private void OnOverlayChanged(object? sender, EventArgs e)
    {
        lock (BufferLock)
        {
            ConvertBuffer(LastCroppedRgbFrame.AsSpan());
        }

        SendSendBuffer();
    }

    private void ConvertBuffer(ReadOnlySpan<byte> rgbFrame)
    {
        rgbFrame.CopyTo(CroppedFrameBuffer);
        BlendOverlay(CroppedFrameBuffer);
        ImageConverter.ConvertRgb24ToRgb16(CroppedFrameBuffer, ConversionBuffer);
        (SendBuffer, ConversionBuffer) = (ConversionBuffer, SendBuffer);
    }

    private void BlendOverlay(byte[] rgbFrame)
    {
        var overlayBitmap = DiagnosticOutputRenderer.DiagnosticOverlayBitmap;
        if (overlayBitmap == null || overlayBitmap.Bytes == null)
        {
            return;
        }

        var diagnosticOverlayBgra = overlayBitmap.Bytes;
        for (var y = 0; y < OverlayHeight; y++)
        {
            for (var x = 0; x < OverlayWidth; x++)
            {
                var overlayIdx = (y * OverlayWidth + x) * 4;
                var olB = diagnosticOverlayBgra[overlayIdx + 0];
                var olG = diagnosticOverlayBgra[overlayIdx + 1];
                var olR = diagnosticOverlayBgra[overlayIdx + 2];
                var olA = diagnosticOverlayBgra[overlayIdx + 3];
                if (olA == 0)
                {
                    continue;
                }

                var rgbIdx = (y * OverlayWidth + x) * 3;
                var alpha = olA / 255f;
                rgbFrame[rgbIdx + 0] = (byte) (olR * alpha + rgbFrame[rgbIdx + 0] * (1 - alpha));
                rgbFrame[rgbIdx + 1] = (byte) (olG * alpha + rgbFrame[rgbIdx + 1] * (1 - alpha));
                rgbFrame[rgbIdx + 2] = (byte) (olB * alpha + rgbFrame[rgbIdx + 2] * (1 - alpha));
            }
        }
    }

    private void SendSendBuffer()
    {
        if (isFrameBeingSent)
        {
            return;
        }

        isFrameBeingSent = true;
        try
        {
            Task.Run(() =>
            {
                try
                {
                    lock (SyncRoot)
                    {
                        SendFrameInternal(SendBuffer);
                        isFrameBeingSent = false;
                    }
                }
                finally
                {
                    isFrameBeingSent = false;
                }
            });
        }
        finally
        {
            isFrameBeingSent = false;
        }
    }

    private Stopwatch TotalStopwatch { get; } = new();
    private Stopwatch ChunkStopwatch { get; } = new();
    private bool DoMeasure { get; }

    private void SendFrameInternal(byte[] frameData)
    {
        if (Identity is null)
        {
            Logger.LogError("Identity is null, cannot send frame data");
            throw new InvalidOperationException("Identity is null.");
        }

        if (frameData.Length % ChunkSize != 0)
        {
            Logger.LogError(
                "Frame data length ({Length}) is not a multiple of {ChunkSize}",
                frameData.Length,
                ChunkSize);
            throw new ArgumentException("Frame data must be a multiple of 512 bytes!");
        }

        if (PushDevice == IntPtr.Zero)
        {
            Logger.LogWarning("Push device is not connected");
            return;
        }

        if (DoMeasure)
        {
            TotalStopwatch.Restart();
        }

        // Step 1: Send header
        var headerResult = LibUsbWrapper.BulkTransfer(
            PushDevice,
            Identity.DisplayEndpoint,
            FrameHeader,
            FrameHeader.Length,
            out var transferredBytes,
            timeout: 1000);

        if (transferredBytes != FrameHeader.Length)
        {
            Logger.LogError(
                "Could not send full header, sent {Transferred}/{HeaderLength} bytes",
                transferredBytes,
                FrameHeader.Length);
            return;
        }

        if (headerResult != 0)
        {
            Logger.LogError(
                "Failed to send frame header: {Message}",
                GetLibUsbErrorMessage(headerResult));
            return;
        }

        // Step 2: Send pixel data in 512*n-byte chunks
        for (var i = 0; i < frameData.Length; i += ChunkSize)
        {
            if (DoMeasure)
            {
                ChunkStopwatch.Restart();
            }

            var frameSlice = frameData[i..(i + ChunkSize)];

            if (PushDevice == IntPtr.Zero)
            {
                Logger.LogWarning("Lost Push device while sending frame data");
                return;
            }

            var result = LibUsbWrapper.BulkTransfer(
                PushDevice,
                Identity.DisplayEndpoint,
                frameSlice,
                ChunkSize,
                out var transferred,
                timeout: 200);

            if (DoMeasure)
            {
                ChunkStopwatch.Stop();
                Logger.LogTrace("Chunk time: {Ms:0.000}", ChunkStopwatch.Elapsed.TotalMilliseconds);
            }

            if (result == 0 && transferred == ChunkSize)
            {
                continue;
            }

            Logger.LogError(
                "USB transfer failed at chunk {ChunkIndex}. "
                + "Sent {Transferred}/{ChunkSize} bytes. "
                + "Error: {Message}",
                i / ChunkSize,
                transferred,
                ChunkSize,
                GetLibUsbErrorMessage(result));

            return;
        }

        if (DoMeasure)
        {
            TotalStopwatch.Stop();
            Logger.LogTrace("Total time: {Ms:0.000}", TotalStopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private string GetLibUsbErrorMessage(int errorCode)
    {
        // Try libusb_error_name first
        var errorNamePtr = LibUsbWrapper.LibUsbErrorName(errorCode);
        var errorName = Marshal.PtrToStringAnsi(errorNamePtr);

        if (string.IsNullOrEmpty(errorName) == false)
        {
            return errorName; // Use the official libusb error name
        }

        // Fallback to custom error messages
        return errorCode switch
        {
            0 => "Success",
            -1 => "Input/output error",
            -2 => "Invalid parameter",
            -3 => "Access denied (insufficient permissions)",
            -4 => "No such device",
            -5 => "Interface not found",
            -6 => "Resource busy",
            -7 => "Timeout",
            _ => $"Unknown error (code: {errorCode})"
        };
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Disconnect();
        IsDisposed = true;
    }
}
