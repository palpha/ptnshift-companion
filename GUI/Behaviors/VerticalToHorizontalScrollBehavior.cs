using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using Avalonia.Interactivity;

namespace GUI.Behaviors
{
    public class VerticalToHorizontalScrollBehavior : Behavior<ListBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                AssociatedObject.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            }
            base.OnDetaching();
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (AssociatedObject?.Scroll != null)
            {
                // Use vertical delta to scroll horizontally
                var scrollViewer = AssociatedObject.Scroll;
                double newOffset = scrollViewer.Offset.X - e.Delta.Y * 40; // 40 is a typical scroll step
                scrollViewer.Offset = new Avalonia.Vector(newOffset, scrollViewer.Offset.Y);
                e.Handled = true;
            }
        }
    }
}
