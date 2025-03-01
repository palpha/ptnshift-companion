using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using Core.Image;
using Core.Usb;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace LiveshiftCompanion.PageModels;

public partial class MainPageModel : ObservableObject
{
    [ObservableProperty] private bool isCapturePermitted;
    [ObservableProperty] private bool isCapturing;
    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private bool isPreviewEnabled = true;
    [ObservableProperty] private ImageSource? imageSource;
    [ObservableProperty] private double frameRate;
    [ObservableProperty] private string colorUShortString = "0xFF0000";
    [ObservableProperty] private string lastFrameDumpFilename = "";
    [ObservableProperty] private string debugOutput = "";
    [ObservableProperty] private ObservableCollection<ScreenHelper.DisplayInfo>? displayInfos;
    [ObservableProperty] private ScreenHelper.DisplayInfo? selectedDisplayInfo;
    [ObservableProperty] private string? captureX = "400";
    [ObservableProperty] private string? captureY = "400";
    [ObservableProperty] private string? captureFrameRate = "30";

    private Random Rnd { get; } = new();

    private int FrameCount { get; set; }
    private DateTime LastFrameTime { get; set; }
    private byte[]? LastFrameData { get; set; }

    private ILogger<MainPageModel> Logger { get; }
    private IStreamer Streamer { get; }
    private IImageConverter ImageConverter { get; }
    private IPush2Usb Push2Usb { get; }

    public bool CanCapture => IsCapturePermitted && SelectedDisplayInfo is not null;

    [MemberNotNullWhen(true, nameof(DisplayInfos))]
    public bool HasDisplayInfos => DisplayInfos is { Count: > 0 };

    public ICommand ToggleCaptureCommand { get; }
    public ICommand TogglePreviewCommand { get; }
    public ICommand ToggleConnectionCommand { get; }
    public ICommand InspectLastFrameCommand { get; }
    public ICommand OpenFrameDumpCommand { get; }
    public ICommand CheckPermissionCommand { get; }

    public MainPageModel(
        IStreamer streamer,
        IImageConverter imageConverter,
        IPush2Usb push2Usb,
        ILogger<MainPageModel> logger)
    {
        Streamer = streamer;
        ImageConverter = imageConverter;
        Push2Usb = push2Usb;
        Logger = logger;

        ToggleCaptureCommand = new Command(ExecuteToggleCapture);
        TogglePreviewCommand = new Command(ExecuteTogglePreview);
        ToggleConnectionCommand = new AsyncCommand(ExecuteToggleConnection);
        InspectLastFrameCommand = new Command(ExecuteInspectLastFrame);
        OpenFrameDumpCommand = new Command(ExecuteOpenLastFrameDump);
        CheckPermissionCommand = new AsyncCommand(ExecuteCheckPermission);

        Streamer.EventSource.FrameCaptured += OnFrameReceived;
        LastFrameTime = DateTime.UtcNow;

        PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(IsCapturePermitted) or nameof(SelectedDisplayInfo):
                    OnPropertyChanged(nameof(CanCapture));
                    break;

                case nameof(DisplayInfos):
                    if (HasDisplayInfos && SelectedDisplayInfo is null)
                    {
                        SelectedDisplayInfo = DisplayInfos.First();
                    }

                    OnPropertyChanged(nameof(HasDisplayInfos));
                    break;
            }
        };

        ExecuteIdentifyDisplays();
        _ = ExecuteCheckPermission();
        Task.Run(ExecuteConnect);
    }

    //TODO: refactor Streamer
    //TODO: refactor this bowl of spaghetti

    private void OnFrameReceived(ReadOnlySpan<byte> frame)
    {
        FrameCount++;
        var now = DateTime.UtcNow;
        var elapsed = (now - LastFrameTime).TotalSeconds;
        if (elapsed >= 1)
        {
            FrameRate = FrameCount / elapsed;
            FrameCount = 0;
            LastFrameTime = now;
        }

        LastFrameData = frame.ToArray();

        if (IsPreviewEnabled)
        {
            var image = ConvertRawBytesToPng(frame);
            MainThread.BeginInvokeOnMainThread(() => ImageSource = image);
        }
    }

    private ImageSource ConvertRawBytesToPng(ReadOnlySpan<byte> frame)
    {
        var bitmap = ImageConverter.ConvertBgra24BytesToBitmap(frame, SKColorType.Bgra8888);
        using var skImage = SKImage.FromBitmap(bitmap);
        var data = skImage.Encode(SKEncodedImageFormat.Png, 100);

        if (Logger.IsEnabled(LogLevel.Trace) && Rnd.Next(0, 1000) == 0)
        {
            var timestamp = DateTime.UtcNow.ToString("HHmmss_fff");
            var tempPath = Path.Combine(FileSystem.CacheDirectory, $"{timestamp}.png");
            Logger.LogInformation("Saving frame to disk: {Filename}", tempPath);
            File.WriteAllBytes(tempPath, data.ToArray());
        }

        return ImageSource.FromStream(() => data.AsStream(true));
    }

    private async Task ExecuteCheckPermission()
    {
        IsCapturePermitted = await Streamer.CheckPermissionAsync();
    }

    private void ExecuteToggleCapture()
    {
        _ = ExecuteCheckPermission();

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
            var capY = int.Parse(CaptureY ?? "400");
            var capFrameRate = int.Parse(CaptureFrameRate ?? "30");
            Streamer.Start(SelectedDisplayInfo.Id, capX, capY, 960, 160, capFrameRate);
        }

        IsCapturing = Streamer.IsCapturing;
    }

    private void ExecuteTogglePreview()
    {
        IsPreviewEnabled = IsPreviewEnabled == false;
    }

    private async Task ExecuteToggleConnection()
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

    private void ExecuteInspectLastFrame()
    {
        if (LastFrameData == null)
        {
            return;
        }

        var tmpFilename = Path.Combine(FileSystem.CacheDirectory, "last_frame.txt");
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

        LastFrameDumpFilename = tmpFilename;
    }

    private void ExecuteOpenLastFrameDump()
    {
        if (string.IsNullOrWhiteSpace(LastFrameDumpFilename))
        {
            return;
        }

        if (OperatingSystem.IsIOS())
        {
            Process.Start("open", $"-R \"{LastFrameDumpFilename}\"");
        }
    }

    private void ExecuteIdentifyDisplays()
    {
        DisplayInfos = new(ScreenHelper.ListDisplays());
    }
}