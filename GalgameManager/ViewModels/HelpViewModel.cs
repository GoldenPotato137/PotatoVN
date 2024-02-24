using System.Collections.ObjectModel;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class HelpViewModel : ObservableRecipient, INavigationAware
{
    private readonly IFaqService _faqService;
    private readonly IInfoService _infoService;
    [ObservableProperty] private ObservableCollection<Faq>? _faqs;
    
    public HelpViewModel(IFaqService faqService, IInfoService infoService)
    {
        _faqService = faqService;
        _infoService = infoService;
        _faqService.UpdateStatusChangeEvent += ChangeInfoBar;
        ChangeInfoBar();
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        Faqs = await _faqService.GetFaqAsync();
    }

    public void OnNavigatedFrom()
    {
        _faqService.UpdateStatusChangeEvent -= ChangeInfoBar;
    }

    private void ChangeInfoBar()
    {
        if (_faqService.IsUpdating)
            _infoService.Info(InfoBarSeverity.Informational, msg: "HelpPage_GettingFaq".GetLocalized(),
                displayTimeMs: 100000);
        else
            _infoService.Info(InfoBarSeverity.Informational); // 关闭InfoBar
    }

    [RelayCommand]
    private async Task DownloadFaqs()
    {
        Faqs = await _faqService.GetFaqAsync(true);
    }

    [RelayCommand]
    private async Task Issues()
    {
        await Launcher.LaunchUriAsync(new Uri("https://github.com/GoldenPotato137/GalgameManager/issues/new/choose"));
    }
}