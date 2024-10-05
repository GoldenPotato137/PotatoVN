using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Helpers;
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
    public bool IsBackEnabled => _currentSource != null;

    private void UpdateIsBackEnabled(object? o, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs) =>
        OnPropertyChanged(nameof(IsBackEnabled));

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
        Source = new AdvancedCollectionView(new ObservableCollection<GalgameSourceBase>(), true);
        Source.Filter = s =>
        {
            if (s is GalgameSourceBase source)
            {
                return SearchKey.IsNullOrEmpty() || source.ApplySearchKey(SearchKey);
            }

            return false;
        };
        NavigateTo(null); //显示根库
        _galSourceCollectionService.OnSourceChanged += HandleSourceCollectionChanged;
    }

    public void OnNavigatedFrom()
    {
        _galSourceCollectionService.OnSourceChanged -= HandleSourceCollectionChanged;
        _lastBackSource = _currentSource = null;
    }

    private void HandleSourceCollectionChanged()
    {
        _currentSource = _lastBackSource = null;
        NavigateTo(null);
    }

    /// <summary>
    /// 点击了某个库（若clickItem为null则显示所有根库）<br/>
    /// 若这个库有子库，保持在LibraryViewModel界面，否则以库为Filter进入主界面
    /// </summary>
    [RelayCommand]
    private void NavigateTo(GalgameSourceBase? clickedItem)
    {
        Source.Clear();
        if (clickedItem == null)
        {
            foreach (GalgameSourceBase src in _galSourceCollectionService.GetGalgameSources()
                         .Where(s => s.ParentSource is null))
                Source.Add(src);
        }
        else if (clickedItem.SubSources.Count > 0)
        {
            foreach (GalgameSourceBase src in _galSourceCollectionService.GetGalgameSources()
                         .Where(s => s.ParentSource == clickedItem))
                Source.Add(src);
        }
        else
        {
            _filterService.ClearFilters();
            _filterService.AddFilter(new SourceFilter(clickedItem));
            _navigationService.NavigateTo(typeof(HomeViewModel).FullName!);
        }

        _currentSource = clickedItem;
    }

    [RelayCommand]
    private void Back()
    {
        if (_currentSource is null) return;
        _lastBackSource = _currentSource;
        NavigateTo(_currentSource.ParentSource);
    }

    [RelayCommand]
    private void Forward()
    {
        if (_lastBackSource is null || _lastBackSource == _currentSource) return;
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
