using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Core.Capturing;
using GUI.ViewModels;

namespace GUI.Views;

public partial class MainWindow : Window
{
    private ICaptureService CaptureService { get; }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private Point dragStartPosition;

    private DragOutlineWindow? DragOutline { get; set; }
    private bool IsDragging { get; set; }
    private double CaptureStartX { get; set; }
    private double CaptureStartY { get; set; }
    private CancellationTokenSource? DebounceCts { get; set; }

    public MainWindow(ICaptureService captureService)
    {
        CaptureService = captureService;
        InitializeComponent();

        ArrowKeyEnabler.AddHandler(PointerPressedEvent, ArrowKeyEnabler_PointerPressed, handledEventsToo: true);
        ArrowKeyEnabler.AddHandler(PointerMovedEvent, ArrowKeyEnabler_PointerMoved, handledEventsToo: true);
        ArrowKeyEnabler.AddHandler(PointerReleasedEvent, ArrowKeyEnabler_PointerReleased, handledEventsToo: true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var focused = FocusManager?.GetFocusedElement();

        if (e.Key == Key.Escape)
        {
            ArrowKeyEnabler.Focus();
            return;
        }

        if ((e.KeyModifiers.HasFlag(KeyModifiers.Control) == false
             && (focused is not Button b || b != ArrowKeyEnabler))
            || focused is TextBox)
        {
            return;
        }

        void Update(int xChange, int yChange)
        {
            ViewModel.IsAutoLocateEnabled = false;
            var cfg = ViewModel.CaptureConfiguration;
            ViewModel.UpdateCaptureConfiguration(cfg with
            {
                CaptureX = cfg.CaptureX + xChange,
                CaptureY = cfg.CaptureY + yChange
            });
        }

        var delta =
            e.KeyModifiers.HasFlag(KeyModifiers.Shift) == false
                ? 1
                : e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Alt)
                    ? 100
                    : 10;

        switch (e.Key)
        {
            case Key.Left:
                Update(delta, 0);
                break;
            case Key.Right:
                Update(-delta, 0);
                break;
            case Key.Up:
                Update(0, delta);
                break;
            case Key.Down:
                Update(0, -delta);
                break;
            default:
                base.OnKeyDown(e);
                break;
        }
    }

    private void ArrowKeyEnabler_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        IsDragging = true;
        ViewModel.IsAutoLocateEnabled = false;
        dragStartPosition = e.GetPosition(this);

        // Remember the config's starting coords
        var cfg = ViewModel.CaptureConfiguration;
        CaptureStartX = cfg.CaptureX;
        CaptureStartY = cfg.CaptureY;

        DragOutline = new DragOutlineWindow
        {
            Width = ViewModel.CaptureConfiguration.Width + 6,
            Height = ViewModel.CaptureConfiguration.Height + 6
        };
        DragOutline.Show();
    }

    private void ArrowKeyEnabler_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (IsDragging == false || DragOutline == null)
        {
            return;
        }

        var (totalDx, totalDy, newCfg) = CalculatePosition(e);

        // Move the outline (this is immediate so user sees it)
        var newLeft = Bounds.X + CaptureStartX + totalDx - 3; // 3 is the border width
        var newTop = Bounds.Y + CaptureStartY + totalDy - 3;

        DragOutline.Position = new PixelPoint((int)newLeft, (int)newTop);

        // Cancel existing debounce timer and start a fresh one
        DebounceCts?.Cancel();
        DebounceCts = new CancellationTokenSource();
        var token = DebounceCts.Token;

        var delayMs = CaptureService.GetConfigurationChangeDelayMs(newCfg);
        if (delayMs > 0)
        {
            // Schedule the config update n ms from now
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs, token);

                    // Only runs if no more movement in last 500 ms
                    UpdateConfiguration(newCfg);
                }
                catch (TaskCanceledException)
                {
                    //
                }
            }, token);
        }
        else
        {
            UpdateConfiguration(newCfg);
        }
    }

    private void ArrowKeyEnabler_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        IsDragging = false;

        // Immediately apply the final offset, in case pointer is released
        DebounceCts?.Cancel();
        DebounceCts = null;

        if (DragOutline != null)
        {
            // Compute final offset
            var (_, _, newCfg) = CalculatePosition(e);
            UpdateConfiguration(newCfg);
        }

        // Close outline
        DragOutline?.Close();
        DragOutline = null;
    }

    private (double TotalDx, double TotalDy, CaptureConfiguration Cfg) CalculatePosition(PointerEventArgs e)
    {
        // Calculate total drag offset from the initial press
        var currentPosition = e.GetPosition(this);
        var totalDx = currentPosition.X - dragStartPosition.X;
        var totalDy = currentPosition.Y - dragStartPosition.Y;

        var cfg = ViewModel.CaptureConfiguration;
        var newCfg = cfg with
        {
            CaptureX = (int)(CaptureStartX + totalDx),
            CaptureY = (int)(CaptureStartY + totalDy)
        };

        return (totalDx, totalDy, newCfg);
    }

    private void UpdateConfiguration(CaptureConfiguration cfg) =>
        Dispatcher.UIThread.Post(() =>
        {
            ViewModel.UpdateCaptureConfiguration(cfg);
        });
}