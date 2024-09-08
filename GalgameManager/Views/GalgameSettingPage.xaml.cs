using GalgameManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Views;

public sealed partial class GalgameSettingPage : Page
{
    public GalgameSettingViewModel ViewModel
    {
        get;
    }

    public GalgameSettingPage()
    {
        ViewModel = App.GetService<GalgameSettingViewModel>();
        InitializeComponent();
    }

    private void GalgameSettingPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        // 延迟加载，减少卡顿
        Task.Run(() =>
        {
            Task.Delay(100);
            App.DispatcherQueue.TryEnqueue(() => FindName("TagsBox"));
        });
    }
}
