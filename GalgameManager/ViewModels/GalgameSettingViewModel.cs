using System.ComponentModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Converter;
using GalgameManager.Models;
using System.Collections.ObjectModel;
using GalgameManager.Services;
using GalgameManager.Views.Dialog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.ViewModels;

public partial class GalgameSettingViewModel : ObservableObject, INavigationAware, IRecipient<GalgameParsingEventArgs>
{
    [ObservableProperty]
    private Galgame _gal = null!;

    public List<RssType> RssTypes { get; } = new() { RssType.Bangumi, RssType.Vndb, RssType.Mixed, 
        RssType.Ymgal, RssType.Cngal, RssType.Steam };

    private readonly GalgameCollectionService _galService;
    private readonly GalgameSourceCollectionService _sourceService;
    private readonly INavigationService _navigationService;
    private readonly IPvnService _pvnService;
    private readonly IInfoService _infoService;
    private readonly ILocalSettingsService _settingsService;
    private readonly IMessenger _bus;
    private readonly string[] _searchUrlList = new string[Galgame.PhraserNumber];
    [ObservableProperty] private string _searchUri = "";
    [ObservableProperty] private bool _isPhrasing;
    [ObservableProperty] private string _parsingMsg = string.Empty;
    [ObservableProperty] private RssType _selectedRss = RssType.None;
    [ObservableProperty] private string _galgameInfoDescription = string.Empty;
    [ObservableProperty] private ObservableCollection<KeyMapping> _keyMappings = new();
    [ObservableProperty] private DateTimeOffset _releasedDate; //包一层的原因：CalendarDatePicker的Date为DateTimeOffset（而非datetime）
    [ObservableProperty] private double _tagWidth = 20; //没法设置Expander为Stretch，故暂直接设置宽度
    public string LocalPathMsg => Gal.LocalPath ?? "GalgameSettingPage_NotLocalGame".GetLocalized();
    public string ExePathMsg => Gal.ExePath ?? "GalgameSettingPage_NoExe".GetLocalized();
    public bool IsLocalGame => Gal.IsLocalGame;
    public string SavePositionDescription =>
        string.IsNullOrEmpty(Gal?.DetectedSavePosition)
            ? "GalgameSettingPage_DetectedSavePosition".GetLocalized()!
            : Gal.DetectedSavePosition;

    public GalgameSettingViewModel(IGalgameCollectionService galCollectionService, INavigationService navigationService,
        IPvnService pvnService, IInfoService infoService, IGalgameSourceCollectionService sourceService,
        ILocalSettingsService settingsService, IMessenger bus)
    {
        Gal = new Galgame();
        _galService = (GalgameCollectionService)galCollectionService;
        _sourceService = (GalgameSourceCollectionService)sourceService;
        _navigationService = navigationService;
        _pvnService = pvnService;
        _infoService = infoService;
        _settingsService = settingsService;
        _bus = bus;
        _searchUrlList[(int)RssType.Bangumi] = "https://bgm.tv/subject_search/";
        _searchUrlList[(int)RssType.Vndb] = "https://vndb.org/v/all?sq=";
        _searchUrlList[(int)RssType.Mixed] = "https://bgm.tv/subject_search/";
        _searchUrlList[(int)RssType.Ymgal] = "https://www.ymgal.games/search?type=ga&keyword=";
        _searchUrlList[(int)RssType.Cngal] = "https://www.cngal.org/search?Types=Game&Text=";
        SearchUri = _searchUrlList[(int)RssType.Vndb]; // default
    }

    public async void OnNavigatedFrom()
    {
        Gal.KeyMappings = new List<KeyMapping>(KeyMappings);
        if (Gal.ImagePath.Value != Galgame.DefaultImagePath && !File.Exists(Gal.ImagePath.Value))
            Gal.ImagePath.Value = Galgame.DefaultImagePath;
        await _galService.SaveGalgameAsync(Gal);
        _pvnService.Upload(Gal, PvnUploadProperties.Infos | PvnUploadProperties.ImageLoc);
        _galService.PhrasedEvent -= Update;
        Gal.PropertyChanged -= HandleGalPropertyChanged;
        _bus.Unregister<GalgameParsingEventArgs>(this);
    }

        public async void OnNavigatedTo(object parameter)
        {
            if (parameter is not Galgame galgame)
            {
                return;
            }

            Gal = galgame;
            KeyMappings = new ObservableCollection<KeyMapping>();

            // 用户设置优先：先导入用户的快捷键设置
            List<KeyMapping> userMappings = Gal.KeyMappings.ToList();
            List<KeyMapping> globalMappings = await GetGlobalKeyMappingsAsync();

            // 先添加用户的所有快捷键设置
            foreach (var userMapping in userMappings)
            {
                KeyMappings.Add(userMapping);
            }

            // 然后处理全局快捷键，只添加用户没有定义的
            foreach (KeyMapping globalMapping in globalMappings)
            {
                // 检查用户是否已经定义了这个按键（通过From键匹配）
                var hasUserMapping = userMappings.Any(um =>
                    um.From != null && globalMapping.From != null &&
                    um.From.SequenceEqual(globalMapping.From));

                if (!hasUserMapping)
                {
                    // 用户没有定义这个按键，添加全局设置到头部
                    KeyMappings.Insert(0, new KeyMapping
                    {
                        From = new List<int>(globalMapping.From),
                        To = globalMapping.To != null ? new List<int>(globalMapping.To) : new List<int>(),
                        Remark = globalMapping.Remark,
                        IsGlobal = true,
                        IsEnabled = true
                    });
                }
            }

            Gal.PropertyChanged += HandleGalPropertyChanged;
            SelectedRss = Gal.RssType;
            if (Gal.ReleaseDate.Value > DateTime.MinValue)
                ReleasedDate = Gal.ReleaseDate.Value;
            _galService.PhrasedEvent += Update;
            _bus.Register(this);
            Update();
        }
    
        partial void OnSelectedRssChanged(RssType value)
        {
            Gal.RssType = value;
            if (!string.IsNullOrEmpty(_searchUrlList[(int)value]))
                SearchUri = _searchUrlList[(int)value] + Gal.Name.Value;
        }
    [RelayCommand]
    private void OnBack()
    {
        if(_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
    }

    [RelayCommand]
    private async Task OnGetInfoFromRss(object parameter)
    {
        IsPhrasing = true;
        // 检查是否是 isNameOnly 模式
        if (parameter is string isNameOnly && isNameOnly == "True")
        {
            // 清除目前存储的id信息
            for (var i = 0; i < Galgame.PhraserNumber; i++)
            {
                // 跳过PotatoVn
                if (i == (int)RssType.PotatoVn)
                    continue;
                Gal.Ids[i] = null;
            }
        }
        
        try
        {
            await _galService.ParseGalInfoAsync(Gal, Gal.RssType);
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgameSettingPage_GetInfoFromRssFailed".GetLocalized(),
                e.Message);
            _infoService.Log(InfoBarSeverity.Error, $"{e.Message}\n{e.StackTrace}");
            Update(); // 处理IsPhrasing
        }
    }

    private void Update()
    {
        IsPhrasing = _galService.IsPhrasing;
        GalgameInfoDescription = $"{"GalgameSettingPage_GameInfo_SettingDescription".GetLocalized()}" +
                                 $"   |    {"GalgameSettingPage_LastFetchInfoTime".GetLocalized(
                                     new DateTimeToStringConverter().Convert(Gal.LastFetchInfoTime, null!, null!, null!))}";
    }

    private void HandleGalPropertyChanged(object? sender, PropertyChangedEventArgs propertyChangedEventArgs)
    {
        OnPropertyChanged(nameof(LocalPathMsg));
        OnPropertyChanged(nameof(ExePathMsg));
        OnPropertyChanged(nameof(IsLocalGame));
        OnPropertyChanged(nameof(SavePositionDescription));
    }
    
    [RelayCommand]
    private async Task PickImageAsync()
    {
        var newFile = await DownloadHelper.PickImageAsync();
        if (newFile is null) return;
        Gal.ImagePath.Value= newFile;
    }
    
    [RelayCommand]
    private async Task PickHeaderImageAsync()
    {
        var newFile = await DownloadHelper.PickImageAsync();
        if (newFile is null) return;
        DownloadHelper.DeleteImgIfExists(Gal.HeaderImagePath.Value);
        Gal.HeaderImagePath.Value = null;
        var targetPath = Path.Combine((await FileHelper.GetFolderAsync(FileHelper.FolderType.Images)).Path,
            $"{Gal.Name.Value}_Header.png");
        await Task.Run(() =>
        {
            DownloadHelper.ProcessImage(newFile, targetPath, false);
        });
        Gal.HeaderImagePath.Value = targetPath;
        Gal.HeaderImageUrl = null; //置空，让同步服务上传手动指定的图片
        if (await _settingsService.ReadSettingAsync<bool>(KeyValues.SyncGames) &&
            await _settingsService.ReadSettingAsync<bool>(KeyValues.SyncHeaderImage))
            Gal.PvnUploadProperties |= PvnUploadProperties.HeaderImageLoc;
    }

    [RelayCommand]
    private async Task SetGalgamePathAsync()
    {
        try
        {
            FileOpenPicker openPicker = new();
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow!.GetWindowHandle());
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.FileTypeFilter.Add(".exe");
            openPicker.FileTypeFilter.Add(".bat");
            openPicker.FileTypeFilter.Add(".EXE");
            StorageFile? file = await openPicker.PickSingleFileAsync();
            if (file is not null)
            {
                // 从库中移除该游戏，再设置路径
                var source = _sourceService.GetGalgameSources().FirstOrDefault(s => s.Galgames.Any(g => g.Galgame == Gal));
                if (source != null)
                {
                    _sourceService.MoveOutNoOperate(source, Gal);
                }

                var folder = file.Path[..file.Path.LastIndexOf('\\')];
                Gal.ExePath = file.Path;
                await _galService.SetLocalPathAsync(Gal, folder);
                

                _infoService.Info(InfoBarSeverity.Success, "GalgameSettingPage_PathSetSuccess".GetLocalized());

            }
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgameSettingPage_PathSetFailed".GetLocalized(), e.Message);
            _infoService.Log(InfoBarSeverity.Error, $"{e.Message}\n{e.StackTrace}");
        }
    }

    [RelayCommand]
    private async Task SetGalgameExePathAsync()
    {
        if(!Gal.IsLocalGame)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgameSettingPage_NotLocalGame".GetLocalized());
            return;
        }
        try
        {
            // 先清空ExePath，再调用函数来设置
            // Gal.ExePath = null;
            var result = await _galService.GetGalgameExeAsync(Gal);
            if (result is null)
            {
                _infoService.Info(InfoBarSeverity.Warning, "GalgameSettingPage_ExePathSetCanceled".GetLocalized());
                return;
            }
            _infoService.Info(InfoBarSeverity.Success, "GalgameSettingPage_ExePathSetSuccess".GetLocalized());
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgameSettingPage_ExePathSetFailed".GetLocalized(), e.Message);
            _infoService.Log(InfoBarSeverity.Error, $"{e.Message}\n{e.StackTrace}");
        }
    }

    [RelayCommand]
    private async Task SetSavePositionAsync()
    {
        try
        {
            // 确定起始路径：如果已检测到存档位置则使用它，否则使用 AppData
            string initialPath;
            var absolutePath = SaveDetectionConstants.GetAbsolutePath(Gal.DetectedSavePosition, Gal.LocalPath);
            if (!string.IsNullOrEmpty(absolutePath) && Directory.Exists(absolutePath))
            {
                initialPath = absolutePath;
            }
            else
            {
                initialPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            // 检查是否已有检测到的存档位置
            var hasDetectedSavePosition = !string.IsNullOrEmpty(Gal.DetectedSavePosition);

            // 尝试打开资源管理器到指定路径，然后让用户选择
            await ShowFolderPickerWithPath(initialPath, hasDetectedSavePosition);
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error, "GalgameSettingPage_SavePositionSetFailed".GetLocalized(), e.Message);
            _infoService.Log(InfoBarSeverity.Error, $"{e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 显示文件夹选择器，并打开资源管理器到指定路径
    /// </summary>
    private async Task ShowFolderPickerWithPath(string initialPath, bool hasDetectedSavePosition)
    {
        try
        {
            // 使用自定义的存档位置对话框
            var dialog = new SavePositionDialog(Gal, initialPath, hasDetectedSavePosition);
            await dialog.ShowAsync();

            switch (dialog.Result)
            {
                case SavePositionDialogResult.OpenExplorer:
                    _infoService.Info(InfoBarSeverity.Informational,
                        "已在资源管理器中打开路径，您可以浏览后使用文件夹选择器选择具体位置", displayTimeMs: 3000);

                    // 询问用户是否需要使用文件夹选择器
                    await AskForFolderPickerAfterExplorer();
                    break;

                case SavePositionDialogResult.UseStandardPicker:
                    // 用户选择使用标准选择器
                    await ShowStandardFolderPicker();
                    break;

                case SavePositionDialogResult.Cancel:
                    // 用户取消，什么都不做
                    break;
            }
        }
        catch (Exception ex)
        {
            _infoService.Info(InfoBarSeverity.Warning, "OpenExplorer failed", ex.Message);
            // 回退到标准选择器
            await ShowStandardFolderPicker();
        }
    }

    /// <summary>
    /// 打开资源管理器后询问用户是否需要文件夹选择器
    /// </summary>
    private async Task AskForFolderPickerAfterExplorer()
    {
        var dialog = new FolderPickerConfirmationDialog();
        await dialog.ShowAsync();

        switch (dialog.Result)
        {
            case FolderPickerConfirmationResult.ShowPicker:
                await ShowStandardFolderPicker();
                break;

            case FolderPickerConfirmationResult.Skip:
                // 用户选择不需要，什么都不做
                break;

            case FolderPickerConfirmationResult.Cancel:
                // 用户取消，什么都不做
                break;
        }
    }

    /// <summary>
    /// 显示标准文件夹选择器
    /// </summary>
    private async Task ShowStandardFolderPicker()
    {
        var folderPicker = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, App.MainWindow!.GetWindowHandle());
        folderPicker.FileTypeFilter.Add("*");
        folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
        if (folder is not null)
        {
            Gal.DetectedSavePosition = SaveDetectionConstants.GetPortablePath(folder.Path, Gal.LocalPath);
            await _galService.SaveGalgameAsync(Gal);
            _infoService.Info(InfoBarSeverity.Success, "GalgameSettingPage_SavePositionUpdated".GetLocalized(), displayTimeMs: 2000);
        }
    }

    partial void OnReleasedDateChanged(DateTimeOffset value)
    {
        if (value.LocalDateTime == Gal.ReleaseDate.Value) return;
        Gal.ReleaseDate.Value = value.LocalDateTime;
    }
    
    [RelayCommand]
    private void OnPageSizeChanged(SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 65) return;
        TagWidth = e.NewSize.Width - 65;
    }

    public void Receive(GalgameParsingEventArgs message)
    {
        if (message.Galgame.Uuid != Gal.Uuid) return;
        UiThreadInvokeHelper.Invoke(() => ParsingMsg = message.Message);
    }

    [RelayCommand]
    private async Task ReDetectSavePosition()
    {
        try
        {
            // 清空检测到的存档位置
            Gal.DetectedSavePosition = null;

            // 保存游戏设置
            await _galService.SaveGalgameAsync(Gal);

            // 显示成功信息
            _infoService.Info(InfoBarSeverity.Success,
                "GalgameSettingPage_ReDetectSuccess".GetLocalized(),
                displayTimeMs: 3000);

            // 刷新UI显示
            OnPropertyChanged(nameof(SavePositionDescription));
        }
        catch (Exception e)
        {
            _infoService.Info(InfoBarSeverity.Error,
                "GalgameSettingPage_ReDetectFailed".GetLocalized(),
                e.Message);
            _infoService.Log(InfoBarSeverity.Error, $"{e.Message}\n{e.StackTrace}");
        }
    }

    [RelayCommand]
    private async Task OpenKeyMappingDialog()
    {
        KeyMappingDialog dialog = new(this)
        {
            XamlRoot = App.MainWindow!.Content.XamlRoot,
            RequestedTheme = App.MainWindow.Content is FrameworkElement element ? element.RequestedTheme : ElementTheme.Default
        };

        ContentDialogResult result = await dialog.ShowAsync();

        // 只有在用户点击保存时才保存设置并显示通知
        if (result == ContentDialogResult.Primary)
        {
            await SaveKeyMappingsAsync();
            _infoService.Info(InfoBarSeverity.Success, msg:"KeyMapping_Info_KeyMappingSaved".GetLocalized(), displayTimeMs: 2000);
        }
    }

  
    /// <summary>
    /// 检查全局快捷键是否已存在于当前快捷键列表中
    /// </summary>
    /// <param name="globalMapping">要检查的全局快捷键</param>
    /// <returns>如果不存在返回true，存在返回false</returns>
    private bool IsGlobalKeyMappingNotExists(KeyMapping globalMapping)
    {
        return globalMapping.From != null && KeyMappings.All(k => k.From == null || !k.From.SequenceEqual(globalMapping.From));
    }

    /// <summary>
    /// 从全局设置中获取所有全局快捷键
    /// </summary>
    /// <returns>全局快捷键列表</returns>
    private async Task<List<KeyMapping>> GetGlobalKeyMappingsAsync()
    {
        try
        {
            return await _settingsService.ReadSettingAsync<List<KeyMapping>>(KeyValues.GlobalKeyMappings) ?? new();
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(e: e);
            return new List<KeyMapping>();
        }
    }

    
    [RelayCommand]
    private void AddKeyMapping()
    {
        KeyMappings.Add(new KeyMapping { IsGlobal = false });
    }

    [RelayCommand]
    private void RemoveKeyMapping(KeyMapping? mapping)
    {
        if (mapping != null)
        {
            KeyMappings.Remove(mapping);
        }
    }

    /// <summary>
    /// 保存当前游戏的快捷键映射设置
    /// </summary>
    public async Task SaveKeyMappingsAsync()
    {
        // 保存所有映射，包括用户修改过的全局快捷键设置
        // 这样用户对全局快捷键的自定义设置会被保留
        Gal.KeyMappings = new List<KeyMapping>(KeyMappings);

        // 立即保存游戏数据
        await _galService.SaveGalgameAsync(Gal);
    }
}
