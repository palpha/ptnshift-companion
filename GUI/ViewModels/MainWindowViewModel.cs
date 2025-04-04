using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Core;
using Core.Capturing;
using Core.Diagnostics;
using Core.Image;
using Core.Settings;
using Core.Usb;
using Microsoft.Extensions.Logging;

namespace GUI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private bool isCapturePermitted = OperatingSystem.IsWindows();
    [ObservableProperty] private bool isCapturing;
    [ObservableProperty] private bool isAutoLocateEnabled = true;
    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private bool isPreviewEnabled = true;
    [ObservableProperty] private bool isDevMode;
    [ObservableProperty] private bool isDebug;
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
    [ObservableProperty] private WriteableBitmap? imageSource;

    private CancellationTokenSource propertyUpdateCts = new();
    private CancellationTokenSource cfgUpdateCts = new();
    private CancellationTokenSource connectionCts = new();

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private CancellationTokenSource permissionCheckCts = new();

    private ILogger<MainWindowViewModel> Logger { get; }
    private IDebugWriter DebugWriter { get; }
    private IDisplayService DisplayService { get; }
    private ICaptureService CaptureService { get; }
    private IPtnshiftFinder PtnshiftFinder { get; }
    private ISettingsManager SettingsManager { get; }
    private IPush2Usb Push2Usb { get; }
    private IPreviewRenderer PreviewRenderer { get; }
    private IFrameDebugger FrameDebugger { get; }

    private AppSettings? AppSettings { get; set; }

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        IDebugWriter debugWriter,
        IDisplayService displayService,
        ICaptureService captureService,
        IPtnshiftFinder ptnshiftFinder,
        ISettingsManager settingsManager,
        IPush2Usb push2Usb,
        IPreviewRenderer previewRenderer,
        IFrameDebugger frameDebugger,
        IFrameRateReporter frameRateReporter)
    {
        Logger = logger;
        DebugWriter = debugWriter;
        DisplayService = displayService;
        CaptureService = captureService;
        PtnshiftFinder = ptnshiftFinder;
        SettingsManager = settingsManager;
        Push2Usb = push2Usb;
        PreviewRenderer = previewRenderer;
        FrameDebugger = frameDebugger;

        DebugWriter.DebugWritten += WriteDebug;
        PtnshiftFinder.LocationLost += OnLocationLost;
        PtnshiftFinder.LocationFound += OnLocationFound;
        PreviewRenderer.PreviewRendered += OnPreviewRendered;
        FrameDebugger.FrameDumpWritten += OnFrameDumpWritten;
        frameRateReporter.FrameRateChanged += OnFrameRateChanged;

        PropertyChanged += OnPropertyChanged;

        AvailableDisplays = DisplayService.AvailableDisplays;
        IsDebug = AppConstants.IsDebug;

        PreviewRenderer.IsPreviewEnabled = IsPreviewEnabled;

        DelayOperation(
            () => _ = ExecuteCheckPermissionAsync(),
            1000, ref permissionCheckCts);
        _ = ExecuteConnectAsync();
        _ = LoadSettingsAsync();
    }

    private void OnPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SelectedDisplayInfo):
            {
                if (SelectedDisplayInfo == null)
                {
                    return;
                }

                UpdateCaptureConfiguration(CaptureConfiguration with { DisplayId = SelectedDisplayInfo!.Id });

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
                    UpdateCaptureConfiguration(CaptureConfiguration with { CaptureX = x });
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
                    UpdateCaptureConfiguration(CaptureConfiguration with { CaptureY = x });
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
                    UpdateCaptureConfiguration(CaptureConfiguration with { FrameRate = x });
                }

                break;
            }
            case nameof(IsPreviewEnabled):
            {
                PreviewRenderer.IsPreviewEnabled = IsPreviewEnabled;
                break;
            }
        }
    }

    private void OnFrameDumpWritten(string filename)
    {
        if (LastFrameDumpFilename != filename)
        {
            WriteDebug($"Frame dump: {filename}");
        }

        LastFrameDumpFilename = filename;
    }

    private void OnLocationLost()
    {
        WriteDebug("Lost location");
    }

    private void OnLocationFound(IPtnshiftFinder.Location x)
    {
        if (IsAutoLocateEnabled == false)
        {
            return;
        }

        UpdateCaptureConfiguration(CaptureConfiguration with { CaptureX = x.X, CaptureY = x.Y }, 0);
    }

    private void OnPreviewRendered(WriteableBitmap previewBitmap)
    {
        Dispatcher.UIThread.Post(() => ImageSource = previewBitmap);
    }

    public void UpdateCaptureConfiguration(CaptureConfiguration configuration, int? applicationDelayMs = null)
    {
        var newConfiguration = configuration.GetNormalized(DisplayService.AvailableDisplays);
        if (newConfiguration == CaptureConfiguration)
        {
            return;
        }

        CaptureConfiguration = newConfiguration;

        DelayOperation(
            () => Dispatcher.UIThread.Invoke(() =>
            {
                CaptureX = CaptureConfiguration.CaptureX.ToString();
                CaptureY = CaptureConfiguration.CaptureY.ToString();
                CaptureFrameRate = CaptureConfiguration.FrameRate.ToString();
            }),
            100, ref propertyUpdateCts);

        DelayOperation(
            () => CaptureService.SetConfiguration(CaptureConfiguration),
            applicationDelayMs ?? CaptureService.GetConfigurationChangeDelayMs(CaptureConfiguration), ref cfgUpdateCts);
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
            IsAutoLocateEnabled = AppSettings.IsAutoLocateEnabled;
        });
    }

    public async Task SaveSettingsAsync() =>
        await SettingsManager.SaveAsync(new(
            SelectedDisplayInfo?.Id,
            CaptureConfiguration.CaptureX,
            CaptureConfiguration.CaptureY,
            CaptureConfiguration.FrameRate,
            IsPreviewEnabled,
            IsAutoLocateEnabled));

    private void OnFrameRateChanged(double frameRate)
    {
        MeasuredFrameRate = frameRate;
    }

    public async Task ExecuteCheckPermissionAsync()
    {
        IsCapturePermitted = await CaptureService.CheckCapturePermissionAsync();
    }

    public async Task ExecuteToggleCaptureAsync()
    {
        await ExecuteCheckPermissionAsync();

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

        if (OperatingSystem.IsMacOS())
        {
            IsCapturing = CaptureService.IsCapturing;
        }
        else if (OperatingSystem.IsWindows())
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                Dispatcher.UIThread.Invoke(() => IsCapturing = CaptureService.IsCapturing);
            });
        }
    }

    public async Task ExecuteToggleConnectionAsync()
    {
        if (IsConnected == false) // reactive
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

            DelayOperation(
                () => Dispatcher.UIThread.Invoke(() => IsConnected = Push2Usb.IsConnected),
                100, ref connectionCts);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unable to connect to Push");
        }
    }

    private async Task ExecuteDisconnectAsync()
    {
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

    private void DelayOperation(Action action, int delayMs, ref CancellationTokenSource cts)
    {
        cts.Cancel();
        cts = new();
        var token = cts.Token;
        Task.Run(async () =>
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, token);
            }

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

    private void WriteDebug(string message) => DebugOutput += $"{message}\n";

    public async Task ExecuteInspectLastFrameAsync()
    {
        await FrameDebugger.DumpLastFrameAsync();
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
        else if (OperatingSystem.IsWindows())
        {
            Process.Start("explorer.exe", $"/select,\"{LastFrameDumpFilename}\"");
        }
    }
}
