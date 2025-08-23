using Avalonia.Controls;
using GUI.ViewModels;

namespace GUI.Views
{
    public partial class WindowSelectionView : UserControl
    {
        public WindowSelectionViewModel ViewModel => (WindowSelectionViewModel) DataContext!;

        public WindowSelectionView()
        {
            InitializeComponent();
        }
    }
}
