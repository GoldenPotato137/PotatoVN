using GalgameManager.ViewModels;

namespace GalgameManager.Views;

public partial class CategorySettingPage
{
    public CategorySettingViewModel ViewModel
    {
        get;
    }

    public CategorySettingPage()
    {
        ViewModel = App.GetService<CategorySettingViewModel>();
        InitializeComponent();
    }
}