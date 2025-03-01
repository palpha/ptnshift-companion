using MainPageModel = LiveshiftCompanion.PageModels.MainPageModel;

namespace LiveshiftCompanion.Pages;

public partial class MainPage
{
    public MainPage(MainPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}