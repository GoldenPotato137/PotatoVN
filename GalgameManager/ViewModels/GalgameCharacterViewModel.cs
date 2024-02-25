using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class GalgameCharacterViewModel : ObservableRecipient, INavigationAware
{
    [ObservableProperty] private GalgameCharacter? _character;
    private readonly INavigationService _navigationService;
    [ObservableProperty] private Visibility _isSummaryVisible = Visibility.Collapsed;


    public void OnNavigatedTo(object parameter)
    {
        if (parameter is not GalgameCharacterParameter param) //参数不正确，返回主菜单
        {
            _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
            return;
        }

        Character = param.GalgameCharacter;
        UpdateVisibility();
    }

    public void OnNavigatedFrom()
    {
    }

    public GalgameCharacterViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }
    
    private void UpdateVisibility()
    {
        IsSummaryVisible = Character?.Summary! != string.Empty ? Visibility.Visible : Visibility.Collapsed;
    }
}