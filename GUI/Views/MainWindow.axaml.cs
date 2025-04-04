using System;
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
    private IDisplayService DisplayService { get; }

    private MainWindowViewModel ViewModel => (MainWindowViewModel) DataContext!;

    private PixelPoint previousPointerPosition;

    private DragOutlineWindow? DragOutline { get; set; }
    private bool IsDragging { get; set; }
    private DisplayInfo? SelectedDisplay { get; set; }
    private CancellationTokenSource? DebounceCts { get; set; }

    public MainWindow(
        ICaptureService captureService,
        IDisplayService displayService)
    {
        CaptureService = captureService;
        DisplayService = displayService;
        DisplayService.Screens = Screens;

        InitializeComponent();

        PreviewImage.AddHandler(PointerPressedEvent, DragPointerPressed, handledEventsToo: true);
        PreviewImage.AddHandler(PointerMovedEvent, DragPointerMoved, handledEventsToo: true);
        PreviewImage.AddHandler(PointerReleasedEvent, DragPointerReleased, handledEventsToo: true);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var focused = FocusManager?.GetFocusedElement();

        if (e.Key == Key.Escape)
        {
            ArrowKeyEnabler.Focus();
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) == false
            && (focused is not Button b || b != ArrowKeyEnabler)
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

    private int Scaled(int pixels) => (int) (pixels * SelectedDisplay?.ScalingFactor ?? 1 + 0.5);

    private void DragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Cursor = new Cursor(StandardCursorType.DragMove);

        IsDragging = true;
        ViewModel.IsAutoLocateEnabled = false;

        // The user's current capture config
        var cfg = ViewModel.CaptureConfiguration;

        // Find which display we're clamping to
        SelectedDisplay =
            DisplayService.GetDisplay(cfg.DisplayId)
            ?? throw new InvalidOperationException("Display not selected");

        // Make the drag overlay at the current capture location (minus border, plus discarded first row)
        var dragWindowLeft = SelectedDisplay.BoundsX + cfg.CaptureX - Scaled(3);
        var dragWindowTop = SelectedDisplay.BoundsY + cfg.CaptureY - Scaled(3 - 1);

        DragOutline?.Close();
        DragOutline = new DragOutlineWindow
        {
            Width = 960 + 6,
            Height = 160 + 6,
            Position = new PixelPoint(dragWindowLeft, dragWindowTop)
        };
        DragOutline.Show();

        // Record the pointer's position in *screen* coordinates
        // so we can do incremental deltas from it later
        previousPointerPosition = PixelPoint.FromPoint(e.GetPosition(null), SelectedDisplay.ScalingFactor);
    }

    private void DragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (IsDragging == false || DragOutline == null || SelectedDisplay == null)
        {
            return;
        }

        // Current pointer position in screen coords
        var pointerPosition = PixelPoint.FromPoint(e.GetPosition(null), SelectedDisplay.ScalingFactor);

        // Incremental delta from the last pointer position
        var dx = pointerPosition.X - previousPointerPosition.X;
        var dy = pointerPosition.Y - previousPointerPosition.Y;

        // If shift is held, multiply
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            dx *= 2;
            dy *= 2;
        }

        // New window position
        var dragOutlinePosition = DragOutline.Position;
        var newLeft = dragOutlinePosition.X + dx;
        var newTop = dragOutlinePosition.Y + dy;

        // Clamp to display so the user can't drag partially off the chosen display
        int Clamp(int value, int min, int max) =>
            Math.Clamp(value, min, Math.Max(min, max));

        newLeft = Clamp(newLeft,
            SelectedDisplay.BoundsX - Scaled(3),
            SelectedDisplay.BoundsX + SelectedDisplay.Width
            - Scaled((int) DragOutline.Width - 3));
        newTop = Clamp(newTop,
            SelectedDisplay.BoundsY - Scaled(3),
            SelectedDisplay.BoundsY + SelectedDisplay.Height
            - Scaled((int) DragOutline.Height - 3));

        // Move the overlay
        DragOutline.Position = new PixelPoint(newLeft, newTop);

        // Abort pending configuration changes
        DebounceCts?.Cancel();

        var cfg = GetConfigurationFromDragOutline();

        var delayMs = CaptureService.GetConfigurationChangeDelayMs(cfg);
        if (delayMs > 0)
        {
            DebounceCts = new CancellationTokenSource();
            var token = DebounceCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delayMs, token);
                    UpdateConfiguration(cfg);
                }
                catch (TaskCanceledException)
                {
                }
            }, token);
        }
        else
        {
            UpdateConfiguration(cfg);
        }

        previousPointerPosition = pointerPosition;
    }

    private void DragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        Cursor = new Cursor(StandardCursorType.Arrow);

        IsDragging = false;
        DebounceCts?.Cancel();
        DebounceCts = null;

        var cfg = GetConfigurationFromDragOutline();
        UpdateConfiguration(cfg);

        DragOutline?.Close();
        DragOutline = null;
    }

    private CaptureConfiguration GetConfigurationFromDragOutline()
    {
        if (DragOutline == null || SelectedDisplay == null)
        {
            return ViewModel.CaptureConfiguration;
        }

        // Capture coords based on the drag window's current position,
        // relative to the display origin (plus border width, minus discarded first row)
        var currentPosition = DragOutline.Position;
        return ViewModel.CaptureConfiguration with
        {
            CaptureX = currentPosition.X - SelectedDisplay.BoundsX + Scaled(3),
            CaptureY = currentPosition.Y - SelectedDisplay.BoundsY + Scaled(3 - 1)
        };
    }

    private void UpdateConfiguration(CaptureConfiguration cfg) =>
        Dispatcher.UIThread.Post(() => ViewModel.UpdateCaptureConfiguration(cfg));
}
