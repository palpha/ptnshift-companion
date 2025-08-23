using System;
using System.Collections.Generic;
using Avalonia.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Core.Capturing;
using JetBrains.Annotations;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace GUI.ViewModels
{
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global (Rider's analysis is not great)
    public partial class WindowSelectionViewModel : ViewModelBase, IDisposable
    {
        [ObservableProperty] private bool isReaktorInList;
        [ObservableProperty] private bool isReaktorRunning;
        [ObservableProperty] private bool isVisible;

        private ICapturableWindowService WindowService { get; }
        private ITimer Timer { get; }

        private bool DisposedValue { get; set; }

        [UsedImplicitly] public event Action<WindowInfo?>? WindowSelected;

        /// <summary>
        /// Event fired when a window is inserted at a specific index, allowing view to adjust scroll position.
        /// The parameter is the index where insertion occurred.
        /// </summary>
        [UsedImplicitly]
        public event Action<int>? ScrollInsertionOccurred;

        // ReSharper disable once CollectionNeverQueried.Local (used in view, Rider is weird)
        public ObservableCollection<WindowInfo> Windows { get; } = [];

        public WindowSelectionViewModel(
            ICapturableWindowService windowService,
            TimeProvider timeProvider)
        {
            WindowService = windowService;

            _ = ExecuteRefreshWindowListAsync();

            Timer = timeProvider.CreateTimer(
                x =>
                {
                    if (IsVisible == false)
                    {
                        return;
                    }

                    _ = ExecuteRefreshWindowListAsync();
                }, null,
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2));
        }

        public async Task ExecuteRefreshWindowListAsync()
        {
            try
            {
                var processes = Process.GetProcesses();
                var isReaktorRunning = processes.Any(x => x.ProcessName.StartsWith("Reaktor"));
                var incomingWindows = await WindowService.ListAsync();

                var isReaktorInList = false;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var existingWindows =
                        new Dictionary<int, (int Idx, WindowInfo Window)>(
                            Windows.Select((x, idx) => KeyValuePair.Create(x.WindowId, (idx, x))));
                    var oldWindowIds = Windows.Select(x => x.WindowId).ToHashSet();
                    var newWindowIds = incomingWindows.Select(x => x.WindowId).ToHashSet();
                    oldWindowIds.ExceptWith(newWindowIds);

                    var windowsToRemove = oldWindowIds.Select(x => existingWindows[x]).ToList();
                    foreach (var window in windowsToRemove)
                    {
                        Windows.Remove(window.Window);
                    }

                    // Update existing windows and add new ones
                    foreach (var (idx, window) in incomingWindows.Select((x, idx) => (idx, x)))
                    {
                        if (window.ApplicationName.StartsWith("Reaktor"))
                        {
                            isReaktorInList = true;
                        }

                        var existingWindow = Windows.FirstOrDefault(w => w.WindowId == window.WindowId);
                        if (existingWindow != null)
                        {
                            existingWindow.UpdateProperties(window.Title, window.ApplicationName);
                        }
                        else
                        {
                            var newWindow = new WindowInfo(
                                window.WindowId,
                                window.Title,
                                window.ApplicationName,
                                window.LoadThumbnailAsync);
                            Windows.Insert(Math.Min(idx, Windows.Count), newWindow);
                            ScrollInsertionOccurred?.Invoke(idx);
                        }
                    }

                    IsReaktorInList = isReaktorInList;
                    IsReaktorRunning = isReaktorRunning;
                });
            }
            catch
            {
                // ignored
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (DisposedValue)
            {
                return;
            }

            if (disposing)
            {
                Timer?.Dispose();
            }

            DisposedValue = true;
        }

        /// <summary>
        /// Helper method for views to calculate how much to adjust scroll position when items are inserted
        /// </summary>
        /// <param name="itemHeight">Height of a single item in the list</param>
        /// <param name="currentScrollOffset">Current scroll offset value</param>
        /// <param name="insertionIndex">Index where item was inserted</param>
        /// <param name="visibleItemsBeforeScroll">Number of items visible before scrolling begins</param>
        /// <returns>Scroll offset adjustment value</returns>
        public double CalculateScrollAdjustment(double itemHeight, double currentScrollOffset,
            int insertionIndex, int visibleItemsBeforeScroll)
        {
            // If insertion happened at or before the current viewport, we need to adjust
            // to keep the currently visible items in the same place
            if (insertionIndex <= (currentScrollOffset / itemHeight) + visibleItemsBeforeScroll)
            {
                return itemHeight; // Adjust by one item height
            }

            return 0; // No adjustment needed for insertions below the viewport
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
