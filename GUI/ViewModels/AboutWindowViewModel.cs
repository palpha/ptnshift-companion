using CommunityToolkit.Mvvm.ComponentModel;

namespace GUI.ViewModels;

public partial class AboutWindowViewModel : ViewModelBase
{
    [ObservableProperty] private string versionLabel = "Version 0.0.0";
}
