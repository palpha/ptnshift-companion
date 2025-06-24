using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using GUI.ViewModels;

namespace GUI.Views;

public partial class AboutWindow : Window
{
    private IDisposable KeyUpSubscription { get; set; }

    public AboutWindow()
    {
        InitializeComponent();
        KeyUpSubscription = KeyUpEvent.AddClassHandler<TopLevel>(OnKeyUp, handledEventsToo: true);
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        KeyUpSubscription.Dispose();
    }
}
