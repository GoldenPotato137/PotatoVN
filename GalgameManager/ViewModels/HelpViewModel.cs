using System.Collections.ObjectModel;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Models;
using GalgameManager.Services;

namespace GalgameManager.ViewModels;

public partial class HelpViewModel : ObservableRecipient, INavigationAware
{
    private readonly FaqService _faqService;
    [ObservableProperty] private ObservableCollection<Faq>? _faqs;
    [ObservableProperty] private bool _infoBarVisibility;
    
    public HelpViewModel(IFaqService faqService)
    {
        _faqService = (faqService as FaqService)!;
        _faqService.UpdateStatusChangeEvent += ChangeInfoBar;
        ChangeInfoBar();
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        Faqs = await _faqService.GetFaqAsync();
    }

    public void OnNavigatedFrom()
    {
    }

    private void ChangeInfoBar()
    {
        InfoBarVisibility = _faqService.IsUpdating;
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