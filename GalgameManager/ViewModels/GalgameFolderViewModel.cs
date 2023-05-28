using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class GalgameFolderViewModel : ObservableObject, INavigationAware
{
    private readonly IDataCollectionService<GalgameFolder> _dataCollectionService;
    private readonly GalgameCollectionService _galgameService;
    private readonly ILocalSettingsService _localSettingsService;
    private GalgameFolder? _item;
    public ObservableCollection<Galgame> Galgames = new();
    private readonly List<Galgame> _selectedGalgames = new();
    public readonly RssType[] RssTypes = { RssType.Bangumi, RssType.Vndb };

    [ObservableProperty] private bool _isInfoBarOpen;
    [ObservableProperty] private string _infoBarMessage = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    [ObservableProperty] private bool _isUnpacking;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private string _progressMsg = string.Empty;
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddGalgameCommand))] 
    [NotifyCanExecuteChangedFor(nameof(GetInfoFromRssCommand))]
    [NotifyCanExecuteChangedFor(nameof(GetGalInFolderCommand))]
    private bool _canExecute; //是否正在运行命令

    #region UI_STRING

    [ObservableProperty] private string _uiDownloadInfo = "GalgameFolderPage_DownloadInfo".GetLocalized();

    #endregion

    public GalgameFolder? Item
    {
        get => _item;

        private set
        {
            SetProperty(ref _item, value);
            if (value != null)
                Galgames = value.GetGalgameList().Result;
        }
    }

    public GalgameFolderViewModel(IDataCollectionService<GalgameFolder> dataCollectionService, IDataCollectionService<Galgame> galgameService, ILocalSettingsService localSettingsService)
    {
        _dataCollectionService = dataCollectionService;
        _galgameService = (GalgameCollectionService)galgameService;
        _galgameService.GalgameAddedEvent += ReloadGalgameList;
        _localSettingsService = localSettingsService;
    }

    private void ReloadGalgameList(Galgame galgame)
    {
        if (_item == null) return;
        if (galgame.Path.StartsWith(_item.Path))
            Galgames.Add(galgame);
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is string path)
        {
            var data = await _dataCollectionService.GetContentGridDataAsync();
            Item = data.First(i => i.Path == path);
            if (Item != null)
            {
                Item.ProgressChangedEvent += Update;
                Update();
            }
        }
    }

    public void OnNavigatedFrom()
    {
    }

    private void Update()
    {
        if(Item == null) return;
        CanExecute = !Item.IsRunning;
        IsInfoBarOpen = Item.IsRunning;
        InfoBarMessage = Item.ProgressText;
        InfoBarSeverity = InfoBarSeverity.Success;
        
        IsUnpacking = Item.IsUnpacking;
        ProgressValue = (int)((double)Item.ProgressValue / Item.ProgressMax * 100);
        ProgressMsg = Item.ProgressText;
    }
    

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async void AddGalgame()
    {
        var openPicker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
        openPicker.ViewMode = PickerViewMode.Thumbnail;
        openPicker.FileTypeFilter.Add(".exe");
        var file = await openPicker.PickSingleFileAsync();
        if (file != null)
        {
            var folder = file.Path.Substring(0, file.Path.LastIndexOf('\\'));
            if (_item!.IsInFolder(folder) == false)
            {
                await ShowGameExistedInfoBar(new Exception("该游戏不属于这个库（游戏必须在库文件夹里面）"));
                return;
            }
            await TryAddGalgame(folder);
        }
    }

    /// <summary>
    /// 试图添加游戏，如果添加失败，会显示错误信息
    /// </summary>
    /// <param name="folder">游戏文件夹路径</param>
    private async Task TryAddGalgame(string folder)
    {
        try
        {
            var result = await _galgameService.TryAddGalgameAsync(folder, true);
            if (result == GalgameCollectionService.AddGalgameResult.Success)
                await ShowSuccessInfoBar();
            else if (result == GalgameCollectionService.AddGalgameResult.AlreadyExists)
                throw new Exception("库里已经有这个游戏了");
            else //NotFoundInRss
                await ShowNotFoundInfoBar();
        }
        catch (Exception e)
        {
            await ShowGameExistedInfoBar(e);
        }
    }
    
    private async Task ShowGameExistedInfoBar(Exception e)
    {

        IsInfoBarOpen = true;
        InfoBarMessage = e.Message;
        InfoBarSeverity = InfoBarSeverity.Error;
        await Task.Delay(3000);
        IsInfoBarOpen = false;
    }
    private async Task ShowNotFoundInfoBar()
    {
        IsInfoBarOpen = true;
        InfoBarMessage = "成功添加游戏，但没有从信息源中找到这个游戏的信息";
        InfoBarSeverity = InfoBarSeverity.Warning;
        await Task.Delay(3000);
        IsInfoBarOpen = false;
    }
    private async Task ShowSuccessInfoBar()
    {
        IsInfoBarOpen = true;
        InfoBarMessage = "已成功添加游戏到当前库";
        InfoBarSeverity = InfoBarSeverity.Success;
        await Task.Delay(3000);
        IsInfoBarOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async void GetInfoFromRss()
    {
        if (Item == null) return;
        if (_selectedGalgames.Count == 0)
            await Item.GetInfoFromRss();
        else
            await Item.GetInfoFromRss(_selectedGalgames);
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async void GetGalInFolder()
    {
        if (_item == null) return;
        await _item.GetGalgameInFolder(_localSettingsService);
    }
    
    [RelayCommand]
    private async void AddGalFromZip()
    {
        var openPicker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
        openPicker.FileTypeFilter.Add(".zip");
        openPicker.FileTypeFilter.Add(".7z");
        openPicker.FileTypeFilter.Add(".rar");
        openPicker.FileTypeFilter.Add(".tar");
        var file = await openPicker.PickSingleFileAsync();

        if (file == null || _item == null) return;

        var storageFile = await StorageFile.GetFileFromPathAsync(file.Path);
        if (storageFile == null) return;

        var result = await _item.UnpackGame(storageFile, null);
        while (result==null)
        {
            var dialog = new PasswdDialog(App.MainWindow.Content.XamlRoot, "请输入压缩包解压密码");
            await dialog.ShowAsync();
            if(dialog.Password == null) //取消
                return;
            result = await _item.UnpackGame(storageFile, dialog.Password);
        }

        IsUnpacking = true;
        ProgressMsg = "正在从信息源中获取游戏信息...";
        await TryAddGalgame(result);
        IsUnpacking = false;
    }

    [RelayCommand]
    private void OnSelectionChanged(object et)
    {
        SelectionChangedEventArgs e = (SelectionChangedEventArgs) et;
        foreach(Galgame galgame in e.AddedItems)
            _selectedGalgames.Add(galgame);
        foreach (Galgame galgame in e.RemovedItems)
            _selectedGalgames.Remove(galgame);
        UiDownloadInfo = _selectedGalgames.Count == 0
            ? "GalgameFolderPage_DownloadInfo".GetLocalized()
            : "GalgameFolderPage_DownloadSelectedInfo".GetLocalized();
    }
}

public class PasswdDialog : ContentDialog
{
    public string? Password;
    private TextBox? _textBox;

    public PasswdDialog(XamlRoot xamlRoot, string title)
    {
        XamlRoot = xamlRoot;
        Title = title;
        Content = CreateContent();
        PrimaryButtonText = "确定";
        SecondaryButtonText = "取消";

        IsPrimaryButtonEnabled = false;

        PrimaryButtonClick += (_, _) => { Password = _textBox?.Text;};
        SecondaryButtonClick += (_, _) => { Password = null; };
    }

    private UIElement CreateContent()
    {
        var stackPanel = new StackPanel();
        _textBox = new TextBox
        {
            PlaceholderText = "请输入压缩包解压密码",
        };
        _textBox.TextChanged += (_, _) => IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(_textBox?.Text); 
        stackPanel.Children.Add(_textBox);
        return stackPanel;
    }
}
