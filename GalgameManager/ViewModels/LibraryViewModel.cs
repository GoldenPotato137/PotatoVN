using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI;
using GalgameManager.Contracts;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Filters;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class LibraryViewModel : ObservableObject, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly GalgameSourceCollectionService _galSourceCollectionService;
    private readonly IInfoService _infoService;
    private readonly IFilterService _filterService;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsBackEnabled))]
    private GalgameSourceBase? _currentSource;
    private GalgameSourceBase? _lastBackSource;
    
    [ObservableProperty]
    private AdvancedCollectionView _source = null!;
    
    #region UI

    public readonly string UiSearch = "Search".GetLocalized();
    public bool IsBackEnabled => CurrentSource != null;

    #endregion

    #region SERACH

    [ObservableProperty] private string _searchTitle = "Search".GetLocalized();
    [ObservableProperty] private string _searchKey = "";
    [ObservableProperty] private ObservableCollection<string> _searchSuggestions = new();
    
    [RelayCommand]
    private void Search(string searchKey)
    {
        SearchTitle = searchKey == string.Empty ? UiSearch : UiSearch + " ●";
        Source.RefreshFilter();
    }
    
    #endregion

    public LibraryViewModel(INavigationService navigationService, IGalgameSourceCollectionService galFolderService,
        IInfoService infoService, IFilterService filterService)
    {
        _navigationService = navigationService;
        _galSourceCollectionService = (GalgameSourceCollectionService) galFolderService;
        _infoService = infoService;
        _filterService = filterService;
    }

    public void OnNavigatedTo(object parameter)
    {
        Source = new AdvancedCollectionView(new ObservableCollection<IDisplayableGameObject>(), true);
        Source.Filter = s =>
        {
            if (s is GalgameSourceBase source)
                return SearchKey.IsNullOrEmpty() || source.ApplySearchKey(SearchKey);
            if (s is Galgame game)
                return SearchKey.IsNullOrEmpty() || game.ApplySearchKey(SearchKey);
            return false;
        };
        NavigateTo(null); //显示根库
        _galSourceCollectionService.OnSourceChanged += HandleSourceCollectionChanged;
    }

    public void OnNavigatedFrom()
    {
        _galSourceCollectionService.OnSourceChanged -= HandleSourceCollectionChanged;
        _lastBackSource = CurrentSource = null;
    }

    private void HandleSourceCollectionChanged()
    {
        CurrentSource = _lastBackSource = null;
        NavigateTo(null);
    }

    /// <summary>
    /// 点击了某个库（若clickItem为null则显示所有根库）<br/>
    /// 若这个库有子库，保持在LibraryViewModel界面，否则以库为Filter进入主界面
    /// </summary>
    [RelayCommand]
    private void NavigateTo(IDisplayableGameObject? clickedItem)
    {
        Source.Clear();
        if (clickedItem == null)
        {
            foreach (GalgameSourceBase src in _galSourceCollectionService.GetGalgameSources()
                         .Where(s => s.ParentSource is null))
                Source.Add(src);
        }

        if (clickedItem is Galgame galgame)
        {
            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!,
                new GalgamePageParameter { Galgame = galgame });
        }
        else if (clickedItem is GalgameSourceBase source)
        {
            if (source.SubSources.Count > 0)
            {
                foreach (GalgameSourceBase src in _galSourceCollectionService.GetGalgameSources()
                             .Where(s => s.ParentSource == clickedItem))
                    Source.Add(src);
                foreach (GalgameAndPath game in source.Galgames)
                    Source.Add(game.Galgame);
            }
            else
            {
                _filterService.ClearFilters();
                _filterService.AddFilter(new SourceFilter(source));
                _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
                // foreach (GalgameAndPath game in source.Galgames)
                //     Source.Add(game.Galgame);
            }

            CurrentSource = source;
        }
        else if (clickedItem is null)
            CurrentSource = null;
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentSource is null) return;
        _lastBackSource = CurrentSource;
        NavigateTo(CurrentSource.ParentSource);
    }

    [RelayCommand]
    private void Forward()
    {
        if (_lastBackSource is null || _lastBackSource == CurrentSource) return;
        NavigateTo(_lastBackSource);
    }

    [RelayCommand]
    private async void AddLibrary()
    {
        try
        {
            AddSourceDialog dialog = new()
            {
                XamlRoot = App.MainWindow!.Content.XamlRoot,
            };
            await dialog.ShowAsync();
            if (dialog.Canceled) return;
            switch (dialog.SelectItem)
            {
                case 0:
                    await _galSourceCollectionService.AddGalgameSourceAsync(GalgameSourceType.LocalFolder, dialog.Path);
                    break;
                case 1:
                    await _galSourceCollectionService.AddGalgameSourceAsync(GalgameSourceType.LocalZip, dialog.Path);
                    break;
            }

        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, msg:e.Message);
        }
    }

    [RelayCommand]
    private void EditLibrary(GalgameSourceBase? source)
    {
        if (source is null) return;
        _navigationService.NavigateTo(typeof(GalgameSourceViewModel).FullName!, source.Url);
    }

    [RelayCommand]
    private async Task DeleteFolder(GalgameSourceBase? galgameFolder)
    {
        if (galgameFolder is null) return;
        await _galSourceCollectionService.DeleteGalgameFolderAsync(galgameFolder);
    }
    
    [RelayCommand]
    private void ScanAll()
    {
        _galSourceCollectionService.ScanAll();
        _infoService.Info(InfoBarSeverity.Success, msg: "LibraryPage_ScanAll_Success".GetLocalized(Source.Count));
    }
}
