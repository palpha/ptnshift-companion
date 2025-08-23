using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace GUI
{
    public interface IModalWindowFactory
    {
        TWindow Create<TWindow, TViewModel>()
            where TWindow : Window
            where TViewModel : class;
    }

    public class ModalWindowFactory(IServiceProvider serviceProvider) : IModalWindowFactory
    {
        private IServiceProvider ServiceProvider { get; } = serviceProvider;

        public TWindow Create<TWindow, TViewModel>()
            where TWindow : Window
            where TViewModel : class
        {
            var window = ServiceProvider.GetRequiredService(typeof(TWindow)) as TWindow
                ?? throw new InvalidOperationException($"Window type {typeof(TWindow).Name} not registered.");
            var viewModel = ServiceProvider.GetRequiredService(typeof(TViewModel)) as TViewModel
                ?? throw new InvalidOperationException($"ViewModel type {typeof(TViewModel).Name} not registered.");
            window.DataContext = viewModel;
            return window;
        }
    }
}
