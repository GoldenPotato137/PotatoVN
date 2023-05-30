using GalgameManager.ViewModels;

namespace GalgameManager.Views;

public partial class UpdateContentPage
{
    public UpdateContentViewModel ViewModel
    {
        get;
    }
    
    public UpdateContentPage()
    {
        ViewModel = App.GetService<UpdateContentViewModel>();
        InitializeComponent();
    }
}