using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using Avalonia;
using Avalonia.VisualTree;
using System;
using GUI.ViewModels;

namespace GUI.Behaviors
{
    public class LazyLoadThumbnailBehavior : Behavior<Image>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.AttachedToVisualTree += OnAttachedToVisualTree;
                AssociatedObject.DetachedFromVisualTree += OnDetachedFromVisualTree;
            }
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (AssociatedObject is {DataContext: WindowInfo vm})
            {
                vm.StartThumbnailLoad();
            }
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (AssociatedObject is Image img && img.DataContext is WindowInfo vm)
                vm.CancelThumbnailLoad();
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.AttachedToVisualTree -= OnAttachedToVisualTree;
                AssociatedObject.DetachedFromVisualTree -= OnDetachedFromVisualTree;
            }
            base.OnDetaching();
        }
    }
}
