using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Core.Image;
using Core.Usb;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace GUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private bool isCapturePermitted;
    [ObservableProperty] private bool isCapturing;
    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private bool isPreviewEnabled = true;
    [ObservableProperty] private bool isDevMode;
    [ObservableProperty] private double measuredFrameRate;
    [ObservableProperty] private string lastFrameDumpFilename = "";
    [ObservableProperty] private string debugOutput = "";
    [ObservableProperty] private ObservableCollection<ScreenHelper.DisplayInfo>? displayInfos;
    [ObservableProperty] private ScreenHelper.DisplayInfo? selectedDisplayInfo;
    [ObservableProperty] private string? captureX = "533";
    [ObservableProperty] private string? captureY = "794";
    [ObservableProperty] private string? captureFrameRate = "30";

    [ObservableProperty] private int captureXParsed = 533;
    [ObservableProperty] private int captureYParsed = 794;
    [ObservableProperty] private int captureFrameRateParsed = 30;

    [ObservableProperty] private Bitmap? imageSource;

    private Random Rnd { get; } = new();

    private int FrameCount { get; set; }
    private DateTime LastFrameTime { get; set; }
    private byte[]? LastFrameData { get; set; }

    private ILogger<MainWindowViewModel> Logger { get; }
    private IStreamer Streamer { get; }
    private IImageConverter ImageConverter { get; }
    private IPush2Usb Push2Usb { get; }

    private AppSettings? AppSettings { get; set; }

    public bool CanCapture => IsCapturePermitted && SelectedDisplayInfo is not null;

    [MemberNotNullWhen(true, nameof(DisplayInfos))]
    public bool HasDisplayInfos => DisplayInfos is { Count: > 0 };

    public MainWindowViewModel(
        IStreamer streamer,
        IImageConverter imageConverter,
        IPush2Usb push2Usb,
        ILogger<MainWindowViewModel> logger)
    {
        Streamer = streamer;
        ImageConverter = imageConverter;
        Push2Usb = push2Usb;
        Logger = logger;

        Streamer.EventSource.FrameCaptured += OnFrameReceived;

        LastFrameTime = DateTime.UtcNow;

        PropertyChanged += (_, e) =>
        {
            void MaybeChangeCaptureRegion()
            {
                if (IsCapturing)
                {
                    ExecuteToggleCapture(skipPermissionCheck: true);
                    ExecuteToggleCapture(skipPermissionCheck: true);
                }
            }

            switch (e.PropertyName)
            {
                case nameof(IsCapturePermitted) or nameof(SelectedDisplayInfo):
                    OnPropertyChanged(nameof(CanCapture));
                    MaybeChangeCaptureRegion();
                    break;

                case nameof(DisplayInfos):
                    SetSelectedDisplayInfo();
                    OnPropertyChanged(nameof(HasDisplayInfos));
                    break;

                case nameof(CaptureX):
                {
                    if (int.TryParse(captureX, out var x))
                    {
                        CaptureXParsed = x;
                    }

                    break;
                }
                case nameof(CaptureY):
                {
                    if (int.TryParse(captureY, out var x))
                    {
                        CaptureXParsed = x;
                    }

                    break;
                }
                case nameof(CaptureFrameRate):
                {
                    if (int.TryParse(captureFrameRate, out var x))
                    {
                        CaptureXParsed = x;
                    }

                    break;
                }

                case nameof(CaptureXParsed):
                case nameof(CaptureYParsed):
                case nameof(CaptureFrameRateParsed):
                    MaybeChangeCaptureRegion();
                    break;
            }
        };

        ExecuteIdentifyDisplays();
        _ = ExecuteCheckPermission(delay: true);
        Task.Run(ExecuteConnect);

        _ = LoadSettings();
    }

    private void SetSelectedDisplayInfo(bool? useFallback = null)
    {
        if (HasDisplayInfos && SelectedDisplayInfo is null)
        {
            SelectedDisplayInfo =
                DisplayInfos.FirstOrDefault(x => x.Id == AppSettings?.SelectedDisplayId)
                ?? (useFallback == true ? DisplayInfos.First() : null);
        }
    }

    private async Task LoadSettings()
    {
        DebugOutput += $"Settings: {SettingsManager.SettingsPath}\n";
        AppSettings = await SettingsManager.LoadAsync();
        Dispatcher.UIThread.Invoke(() =>
        {
            SetSelectedDisplayInfo(useFallback: true);
            CaptureX = AppSettings.CaptureX.ToString();
            CaptureY = AppSettings.CaptureY.ToString();
            CaptureFrameRate = AppSettings.CaptureFrameRate.ToString();
            IsPreviewEnabled = AppSettings.IsPreviewEnabled;
        });
    }

    public async Task SaveSettings() =>
        await SettingsManager.SaveAsync(new(
            SelectedDisplayInfo?.Id,
            int.TryParse(CaptureX, out var x) ? x : 0,
            int.TryParse(CaptureY, out var y) ? y : 0,
            int.TryParse(CaptureFrameRate, out var frameRate) ? frameRate : 0,
            IsPreviewEnabled));

    //TODO: refactor MacStreamer
    //TODO: refactor this bowl of spaghetti

    private void OnFrameReceived(ReadOnlySpan<byte> frame)
    {
        FrameCount++;
        var now = DateTime.UtcNow;
        var elapsed = (now - LastFrameTime).TotalSeconds;
        if (elapsed >= 1)
        {
            MeasuredFrameRate = FrameCount / elapsed;
            FrameCount = 0;
            LastFrameTime = now;
        }

        LastFrameData = frame.ToArray();

        if (IsPreviewEnabled)
        {
            var image = ConvertRawBytesToPng(frame);
            Dispatcher.UIThread.Invoke(() => ImageSource = image);
        }
    }

    private Bitmap ConvertRawBytesToPng(ReadOnlySpan<byte> frame)
    {
        var bitmap = ImageConverter.ConvertBgra24BytesToBitmap(frame, SKColorType.Bgra8888);
        using var skImage = SKImage.FromBitmap(bitmap);
        var data = skImage.Encode(SKEncodedImageFormat.Png, 100);

        if (Logger.IsEnabled(LogLevel.Trace) && Rnd.Next(0, 1000) == 0)
        {
            var timestamp = DateTime.UtcNow.ToString("HHmmss_fff");
            var tempDir = Path.GetTempPath();
            var tempPath = Path.Combine(tempDir, $"{timestamp}.png");
            Logger.LogInformation("Saving frame to disk: {Filename}", tempPath);
            File.WriteAllBytes(tempPath, data.ToArray());
        }

        return Bitmap.DecodeToWidth(data.AsStream(), 960);
    }

    public async Task ExecuteCheckPermission(bool? delay = null)
    {
        if (delay == true)
        {
            await Task.Delay(500);
        }

        IsCapturePermitted = await Streamer.CheckPermissionAsync();
    }

    public void ExecuteToggleCapture(bool? skipPermissionCheck = null)
    {
        if (skipPermissionCheck != true)
        {
            _ = ExecuteCheckPermission();
        }

        if (IsCapturePermitted == false || SelectedDisplayInfo is null)
        {
            return;
        }

        if (Streamer.IsCapturing)
        {
            Streamer.Stop();
        }
        else
        {
            var capX = int.Parse(CaptureX ?? "400");
            var capY = int.Parse(CaptureY ?? "1000");
            var capFrameRate = int.Parse(CaptureFrameRate ?? "30");

            try
            {
                Streamer.Start(SelectedDisplayInfo.Id, capX, capY, 960, 160, capFrameRate);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unable to start capture");
                DebugOutput += $"Unable to start capture: {ex.Message}\n{ex.StackTrace}\n";
            }
        }

        IsCapturing = Streamer.IsCapturing;
    }

    public async Task ExecuteToggleConnection()
    {
        await Task.CompletedTask;

        if (IsConnected)
        {
            ExecuteDisconnect();
        }
        else
        {
            ExecuteConnect();
        }
    }

    private void ExecuteConnect()
    {
        IsConnected = Push2Usb.Connect();
        Logger.LogInformation("Connected to server");
    }

    private void ExecuteDisconnect()
    {
        if (IsConnected == false)
        {
            return;
        }

        Push2Usb.Disconnect();
        IsConnected = Push2Usb.IsConnected;
        Logger.LogInformation("Disconnected from server");
    }

    public void ExecuteInspectLastFrame()
    {
        if (LastFrameData == null)
        {
            return;
        }

        var tmpDir = Path.GetTempPath();
        var tmpFilename = Path.Combine(tmpDir, "last_frame.txt");
        using var writer = new StreamWriter(tmpFilename);

        for (var i = 0; i < LastFrameData.Length; i += 4)
        {
            var r = LastFrameData[i + 2];
            var g = LastFrameData[i + 1];
            var b = LastFrameData[i];
            writer.Write($"0x{r:X2}{g:X2}{b:X2} ");

            if ((i / 4 + 1) % 960 == 0)
            {
                writer.WriteLine();
            }
        }

        if (LastFrameDumpFilename == "")
        {
            DebugOutput += $"Frame dump: {tmpFilename}\n";
        }

        LastFrameDumpFilename = tmpFilename;
    }

    public void ExecuteOpenLastFrameDump()
    {
        if (string.IsNullOrWhiteSpace(LastFrameDumpFilename))
        {
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", $"-R \"{LastFrameDumpFilename}\"");
        }
    }

    private void ExecuteIdentifyDisplays()
    {
        DisplayInfos = new(ScreenHelper.ListDisplays().OrderBy(x => x.Id).ToList());
    }
}