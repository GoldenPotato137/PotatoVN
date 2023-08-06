using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace GalgameManager.ViewModels;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public partial class HomeViewModel : ObservableRecipient, INavigationAware
{
    private const int SearchDelay = 500;
    private readonly INavigationService _navigationService;
    private readonly IDataCollectionService<Galgame> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IFilterService _filterService;
    private IFilter? _filter; //进入界面时的使用的过滤器，退出界面后移除
    private DateTime _lastSearchTime = DateTime.Now;
    [ObservableProperty] private bool _isInfoBarOpen;
    [ObservableProperty] private string _infoBarMessage = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private Stretch _stretch;
    [ObservableProperty] private string _searchKey = string.Empty;
    [ObservableProperty] private string _searchTitle = string.Empty;
    [ObservableProperty] private bool _fixHorizontalPicture; // 是否修复横向图片（截断为标准的长方形）
    [ObservableProperty] private bool _displayPlayTypePolygon = true; // 是否显示游玩状态的小三角形

    #region UI

    private static readonly ResourceLoader ResourceLoader= new();
    public readonly string UiEdit = ResourceLoader.GetString("HomePage_Edit");
    public readonly string UiDownLoad = ResourceLoader.GetString("HomePage_Download");
    public readonly string UiRemove = ResourceLoader.GetString("HomePage_Remove");
    public readonly string UiAddNewGame = ResourceLoader.GetString("HomePage_AddNewGame");
    public readonly string UiSort = ResourceLoader.GetString("HomePage_Sort");
    public readonly string UiFilter = ResourceLoader.GetString("HomePage_Filter");
    private readonly string _uiSearch = "HomePage_Search_Label".GetLocalized();

    private readonly string _uiAddGameSuccess = ResourceLoader.GetString("HomePage_AddGameSuccess");
    private readonly string _uiAlreadyInLibrary = ResourceLoader.GetString("HomePage_AlreadyInLibrary");
    private readonly string _uiNoInfo = ResourceLoader.GetString("HomePage_NoInfo");
    private readonly string _uiRemoveTitle = ResourceLoader.GetString("HomePage_Remove_Title");
    private readonly string _uiRemoveMessage = ResourceLoader.GetString("HomePage_Remove_Message");
    private readonly string _uiYes = ResourceLoader.GetString("Yes");
    private readonly string _uiCancel = ResourceLoader.GetString("Cancel");

    #endregion

    public ICommand ItemClickCommand
    {
        get;
    }

    public ObservableCollection<Galgame> Source { get; private set; } = new();

    public HomeViewModel(INavigationService navigationService, IDataCollectionService<Galgame> dataCollectionService,
        ILocalSettingsService localSettingsService, IFilterService filterService)
    {
        _navigationService = navigationService;
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)_dataCollectionService;
        _localSettingsService = localSettingsService;
        _filterService = filterService;
        
        ((GalgameCollectionService)dataCollectionService).GalgameLoadedEvent += async () => Source = await dataCollectionService.GetContentGridDataAsync();
        _galgameService.PhrasedEvent += () => IsPhrasing = false;
        // IsPhrasing = _galgameService.IsPhrasing;

        _stretch = localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture).Result
            ? Stretch.UniformToFill : Stretch.Uniform;
        _fixHorizontalPicture = localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture).Result;
        DisplayPlayTypePolygon = localSettingsService.ReadSettingAsync<bool>(KeyValues.DisplayPlayTypePolygon).Result;

        ItemClickCommand = new RelayCommand<Galgame>(OnItemClick);
    }

    public async void OnNavigatedTo(object parameter)
    {
        SearchKey = _galgameService.GetSearchKey();
        UpdateSearchTitle();
        if(parameter is IFilter filter)
        {
            _filter = filter;
            _filterService.AddFilter(_filter);
        }
        Source = await _dataCollectionService.GetContentGridDataAsync();
    }

    public async void OnNavigatedFrom()
    {
        if (_filter is not null)
        {
            await Task.Delay(200); //等待动画结束
            _filterService.RemoveFilter(_filter);
        }
    }

    private void OnItemClick(Galgame? clickedItem)
    {
        if (clickedItem != null)
        {
            _navigationService.SetListDataItemForNextConnectedAnimation(clickedItem);
            _navigationService.NavigateTo(typeof(GalgameViewModel).FullName!, clickedItem.Path);
        }
    }

    public void Grid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Link;
    }

    public async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0)
            {
                foreach (StorageFile item in items)
                {
                    try
                    {
                        if (item != null)
                        {
                            var folder = item.Path.Substring(0, item.Path.LastIndexOf('\\'));
                            IsPhrasing = true;
                            GalgameCollectionService.AddGalgameResult result = await _galgameService.TryAddGalgameAsync(folder, true);
                            if (result == GalgameCollectionService.AddGalgameResult.Success)
                            {
                                IsInfoBarOpen = true;
                                InfoBarMessage = _uiAddGameSuccess;
                                InfoBarSeverity = InfoBarSeverity.Success;
                                await Task.Delay(3000);
                                IsInfoBarOpen = false;
                            }
                            else if (result == GalgameCollectionService.AddGalgameResult.AlreadyExists)
                            {
                                throw new Exception(_uiAlreadyInLibrary);
                            }
                            else //NotFoundInRss
                            {
                                IsInfoBarOpen = true;
                                InfoBarMessage = _uiNoInfo;
                                InfoBarSeverity = InfoBarSeverity.Warning;
                                await Task.Delay(3000);
                                IsInfoBarOpen = false;
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        IsPhrasing = false;
                        IsInfoBarOpen = true;
                        InfoBarMessage = error.Message;
                        InfoBarSeverity = InfoBarSeverity.Error;
                        await Task.Delay(3000);
                        IsInfoBarOpen = false;
                    }
                }
            }
        }
    }

    [RelayCommand]
    private async void AddGalgame()
    {
        try
        {
            FileOpenPicker openPicker = new();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".exe");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var folder = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
                IsPhrasing = true;
                GalgameCollectionService.AddGalgameResult result = await _galgameService.TryAddGalgameAsync(folder, true);
                if (result == GalgameCollectionService.AddGalgameResult.Success)
                {
                    IsInfoBarOpen = true;
                    InfoBarMessage = _uiAddGameSuccess;
                    InfoBarSeverity = InfoBarSeverity.Success;
                    await Task.Delay(3000);
                    IsInfoBarOpen = false;
                }
                else if (result == GalgameCollectionService.AddGalgameResult.AlreadyExists)
                {
                    throw new Exception(_uiAlreadyInLibrary);
                }
                else //NotFoundInRss
                {
                    IsInfoBarOpen = true;
                    InfoBarMessage = _uiNoInfo;
                    InfoBarSeverity = InfoBarSeverity.Warning;
                    await Task.Delay(3000);
                    IsInfoBarOpen = false;
                }
            }
        }
        catch (Exception e)
        {
            IsPhrasing = false;
            IsInfoBarOpen = true;
            InfoBarMessage = e.Message;
            InfoBarSeverity = InfoBarSeverity.Error;
            await Task.Delay(3000);
            IsInfoBarOpen = false;
        }
    }
    
    [RelayCommand]
    private async void Sort()
    {
        await _galgameService.SetSortKeysAsync();
    }

    [RelayCommand]
    private async Task Search(object et)
    {
        _lastSearchTime = DateTime.Now;
        DateTime tmp = _lastSearchTime;
        await Task.Delay(SearchDelay);
        if (tmp == _lastSearchTime) //如果在延迟时间内没有再次输入，则开始搜索
        {
            _galgameService.Search(SearchKey);
            UpdateSearchTitle();
        }
    }

    private void UpdateSearchTitle()
    {
        SearchTitle = SearchKey == string.Empty ? _uiSearch : _uiSearch + " ●";
    }

    [RelayCommand]
    private async void GalFlyOutDelete(Galgame? galgame)
    {
        if(galgame == null) return;
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = _uiRemoveTitle,
            Content = _uiRemoveMessage,
            PrimaryButtonText = _uiYes,
            SecondaryButtonText = _uiCancel
        };
        dialog.PrimaryButtonClick += async (_, _) =>
        {
            await _galgameService.RemoveGalgame(galgame);
        };
        
        await dialog.ShowAsync();
    }
    
    [RelayCommand]
    private void GalFlyOutEdit(Galgame? galgame)
    {
        if(galgame == null) return;
        _navigationService.NavigateTo(typeof(GalgameSettingViewModel).FullName!, galgame);
    }

    [RelayCommand]
    private async void GalFlyOutGetInfoFromRss(Galgame? galgame)
    {
        if(galgame == null) return;
        IsPhrasing = true;
        await _galgameService.PhraseGalInfoAsync(galgame);
        IsPhrasing = false;
    }

    partial void OnFixHorizontalPictureChanged(bool value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.FixHorizontalPicture, value);
        Stretch = value ? Stretch.UniformToFill : Stretch.Uniform;
        if (value == false)
            DisplayPlayTypePolygon = false;
    }

    partial void OnDisplayPlayTypePolygonChanged(bool value) =>
        _localSettingsService.SaveSettingAsync(KeyValues.DisplayPlayTypePolygon, value);
}
