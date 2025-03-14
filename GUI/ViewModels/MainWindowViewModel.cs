using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Core.Capturing;
using Core.Image;
using Core.Settings;
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
    [ObservableProperty] private ObservableCollection<DisplayInfo>? availableDisplays;
    [ObservableProperty] private DisplayInfo? selectedDisplayInfo;
    [ObservableProperty] private string? captureX = "533";
    [ObservableProperty] private string? captureY = "794";
    [ObservableProperty] private string? captureFrameRate = "30";
    [ObservableProperty] private int captureXParsed = 533;
    [ObservableProperty] private int captureYParsed = 794;
    [ObservableProperty] private int captureFrameRateParsed = 30;
    [ObservableProperty] private CaptureConfiguration captureConfiguration = new(0, 533, 794, 960, 161, 25);
    [ObservableProperty] private Bitmap? imageSource;

    private CancellationTokenSource propertyUpdateCancellationTokenSource = new();
    private CancellationTokenSource cfgUpdateCancellationTokenSource = new();

    private Random Rnd { get; } = new();

    private int FrameCount { get; set; }
    private DateTime LastFrameTime { get; set; }
    private byte[]? LastFrameData { get; set; }

    private ILogger<MainWindowViewModel> Logger { get; }
    private IDisplayService DisplayService { get; }
    private ICaptureService CaptureService { get; }
    private ISettingsManager SettingsManager { get; }
    private IImageConverter ImageConverter { get; }
    private IPush2Usb Push2Usb { get; }

    private AppSettings? AppSettings { get; set; }

    public MainWindowViewModel(
        IDisplayService displayService,
        ICaptureService captureService,
        ISettingsManager settingsManager,
        IImageConverter imageConverter,
        IPush2Usb push2Usb,
        ILogger<MainWindowViewModel> logger)
    {
        DisplayService = displayService;
        CaptureService = captureService;
        SettingsManager = settingsManager;
        ImageConverter = imageConverter;
        Push2Usb = push2Usb;
        Logger = logger;

        CaptureService.FrameCaptured += OnFrameReceived;

        LastFrameTime = DateTime.UtcNow;

        PropertyChanged += (_, e) =>
        {
            void UpdateCaptureConfiguration(CaptureConfiguration configuration)
            {
                CaptureConfiguration = configuration.GetNormalized(DisplayService.AvailableDisplays);

                DelayOperation(
                    () => Dispatcher.UIThread.Invoke(() =>
                    {
                        CaptureX = CaptureConfiguration.CaptureX.ToString();
                        CaptureY = CaptureConfiguration.CaptureY.ToString();
                        CaptureFrameRate = CaptureConfiguration.FrameRate.ToString();
                    }),
                    100, ref propertyUpdateCancellationTokenSource);

                DelayOperation(
                    () => CaptureService.SetConfiguration(CaptureConfiguration),
                    500, ref cfgUpdateCancellationTokenSource);
            }


            switch (e.PropertyName)
            {
                case nameof(SelectedDisplayInfo):
                {
                    if (SelectedDisplayInfo == null)
                    {
                        return;
                    }

                    UpdateCaptureConfiguration(CaptureConfiguration with
                    {
                        DisplayId = SelectedDisplayInfo!.Id
                    });

                    break;
                }
                case nameof(CaptureX):
                {
                    if (CaptureX == CaptureConfiguration.CaptureX.ToString())
                    {
                        return;
                    }

                    if (int.TryParse(CaptureX, out var x))
                    {
                        UpdateCaptureConfiguration(CaptureConfiguration with
                        {
                            CaptureX = x
                        });
                    }

                    break;
                }
                case nameof(CaptureY):
                {
                    if (CaptureY == CaptureConfiguration.CaptureY.ToString())
                    {
                        return;
                    }

                    if (int.TryParse(CaptureY, out var x))
                    {
                        UpdateCaptureConfiguration(CaptureConfiguration with
                        {
                            CaptureY = x
                        });
                    }

                    break;
                }
                case nameof(CaptureFrameRate):
                {
                    if (CaptureFrameRate == CaptureConfiguration.FrameRate.ToString())
                    {
                        return;
                    }

                    if (int.TryParse(CaptureFrameRate, out var x))
                    {
                        UpdateCaptureConfiguration(CaptureConfiguration with
                        {
                            FrameRate = x
                        });
                    }

                    break;
                }
            }
        };

        AvailableDisplays = DisplayService.AvailableDisplays;
        _ = ExecuteCheckPermission(delay: true);
        _ = ExecuteConnectAsync();
        _ = LoadSettingsAsync();
    }

    private void SetSelectedDisplayInfo(bool? useFallback = null)
    {
        SelectedDisplayInfo ??=
            DisplayService.GetDefaultDisplay(AppSettings)
            ?? (useFallback == true ? DisplayService.AvailableDisplays.First() : null);
    }

    private async Task LoadSettingsAsync()
    {
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

    public async Task SaveSettingsAsync() =>
        await SettingsManager.SaveAsync(new(
            SelectedDisplayInfo?.Id,
            CaptureConfiguration.CaptureX,
            CaptureConfiguration.CaptureY,
            CaptureConfiguration.FrameRate,
            IsPreviewEnabled));

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

        IsCapturePermitted = await CaptureService.CheckCapturePermissionAsync();
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

        if (CaptureService.IsCapturing)
        {
            CaptureService.StopCapture();
        }
        else
        {
            CaptureService.SetConfiguration(CaptureConfiguration);
            CaptureService.StartCapture();
        }

#if MACOS
        IsCapturing = CaptureService.IsCapturing;
#elif WINDOWS
        Task.Run(async () =>
        {
            await Task.Delay(100);
            Dispatcher.UIThread.Invoke(() => IsCapturing = CaptureService.IsCapturing);
        });
#endif
    }

    private void DelayOperation(
        Action action,
        int delayMs,
        ref CancellationTokenSource cts)
    {
        cts.Cancel();
        cts = new CancellationTokenSource();
        var token = cts.Token;
        Task.Run(async () =>
        {
            await Task.Delay(delayMs, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Exception in delayed action");
            }
        }, token);
    }

    public async Task ExecuteToggleConnectionAsync()
    {
        if (IsConnected)
        {
            await ExecuteDisconnectAsync();
        }
        else
        {
            await ExecuteConnectAsync();
        }
    }

    private async Task ExecuteConnectAsync()
    {
        await Task.CompletedTask;

        try
        {
            Push2Usb.Connect();

            Dispatcher.UIThread.Invoke(() => IsConnected = Push2Usb.IsConnected);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to connect to Push");
        }
    }

    private async Task ExecuteDisconnectAsync()
    {
        if (IsConnected == false)
        {
            return;
        }

        await Task.CompletedTask;

        try
        {
            Push2Usb.Disconnect();

            Dispatcher.UIThread.Invoke(() => IsConnected = Push2Usb.IsConnected);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to disconnect from Push");
        }
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
}