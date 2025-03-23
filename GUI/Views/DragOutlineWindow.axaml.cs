using Avalonia.Controls;

namespace GUI.Views;

public partial class DragOutlineWindow : Window
{
    public DragOutlineWindow()
    {
        InitializeComponent();

        // Extend the client area into the titlebar so we can go fully transparent
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = -1;
    }
}