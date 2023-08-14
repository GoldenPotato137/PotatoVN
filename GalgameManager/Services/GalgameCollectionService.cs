using System.Collections.ObjectModel;
using System.Globalization;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public partial class GalgameCollectionService : IDataCollectionService<Galgame>
{
    // _galgames 无序
    private List<Galgame> _galgames = new();
    private readonly Dictionary<string, Galgame> _galgameMap = new(); // 路径->Galgame
    private ObservableCollection<Galgame> _displayGalgames = new(); //用于显示的galgame列表
    private static ILocalSettingsService LocalSettingsService { get; set; } = null!;
    private readonly IJumpListService _jumpListService;
    private readonly IFileService _fileService;
    private readonly IFilterService _filterService;
    private string _searchKey = string.Empty;
    public delegate void GalgameDelegate(Galgame galgame);
    public event GalgameDelegate? GalgameAddedEvent; //当有galgame添加时触发
    public event GalgameDelegate? GalgameDeletedEvent; //当有galgame删除时触发
    public event GalgameDelegate? MetaSavedEvent; //当有galgame元数据保存时触发
    public event VoidDelegate? GalgameLoadedEvent; //当galgame列表加载完成时触发
    public event VoidDelegate? PhrasedEvent; //当有galgame信息下载完成时触发
    public event GenericDelegate<Galgame>? PhrasedEvent2; //当有galgame信息下载完成时触发 
    public bool IsPhrasing;
    public bool[] SortKeysAscending;
    public SortKeys[] SortKeysList;

    public IGalInfoPhraser[] PhraserList
    {
        get;
    } = new IGalInfoPhraser[5];

    public GalgameCollectionService(ILocalSettingsService localSettingsService, IJumpListService jumpListService, 
        IFileService fileService, IFilterService filterService)
    {
        LocalSettingsService = localSettingsService;
        LocalSettingsService.OnSettingChanged += async (key, _) => await OnSettingChanged(key);
        _jumpListService = jumpListService;
        _fileService = fileService;
        _filterService = filterService;
        _filterService.OnFilterChanged += () => UpdateDisplay(UpdateType.ApplyFilter);
        BgmPhraser bgmPhraser = new(GetBgmData().Result);
        VndbPhraser vndbPhraser = new();
        PhraserList[(int)RssType.Bangumi] = bgmPhraser;
        PhraserList[(int)RssType.Vndb] = vndbPhraser;
        PhraserList[(int)RssType.Mixed] = new MixedPhraser(bgmPhraser, vndbPhraser);
        
        SortKeysList = LocalSettingsService.ReadSettingAsync<SortKeys[]>(KeyValues.SortKeys).Result ?? new[]
            { SortKeys.LastPlay , SortKeys.Developer};
        SortKeysAscending = LocalSettingsService.ReadSettingAsync<bool[]>(KeyValues.SortKeysAscending).Result ?? new[]
            {false,false};

        App.MainWindow.AppWindow.Closing += async (_, _) =>
        { 
            await SaveGalgamesAsync();
        };
    }
    
    /// <summary>
    /// 时间转换
    /// </summary>
    /// <param name="time">年/月/日</param>
    /// <returns></returns>
    private long GetTime(string time)
    {
        if (time == Galgame.DefaultString)
            return 0;
        if (DateTime.TryParseExact(time, "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateTime dateTime))
        {
            return (long)(dateTime - DateTime.MinValue).TotalDays;
        }

        return 0;
    }
    
    private bool CompareTo(Galgame? a, Galgame? b)
    {
        if (a is null || b is null ) return true;
        for (var i = 0; i < Math.Min(SortKeysList.Length, SortKeysAscending.Length); i++)
        {
            var result = 0;
            var take = -1; //默认降序
            switch (SortKeysList[i])
            {
                case SortKeys.Developer:
                    result = string.Compare(a.Developer.Value!, b.Developer.Value, StringComparison.Ordinal);
                    break;
                case SortKeys.Name:
                    result = string.Compare(a.Name.Value!, b.Name.Value, StringComparison.CurrentCultureIgnoreCase);
                    take = 1;
                    break;
                case SortKeys.Rating:
                    result = a.Rating.Value.CompareTo(b.Rating.Value);
                    break;
                case SortKeys.LastPlay:
                    result = GetTime(a.LastPlay.Value!).CompareTo(GetTime(b.LastPlay.Value!));
                    break;
                case SortKeys.ReleaseDate:
                    if (a.ReleaseDate != null && b.ReleaseDate != null )
                    {
                        result = a.ReleaseDate.Value.CompareTo(b.ReleaseDate.Value);
                    }
                    break;
            }
            if (result != 0)
                return take * result <= 0 ^ SortKeysAscending[i]; 
        }
        return true;
    }
    
    public async Task InitAsync()
    {
        await GetGalgames();
        await _jumpListService.CheckJumpListAsync(_galgames);
        await Upgrade();
    }

    private async Task GetGalgames()
    {
        _galgames = await LocalSettingsService.ReadSettingAsync<List<Galgame>>(KeyValues.Galgames, true) ?? new List<Galgame>();
        List<Galgame> toRemove = _galgames.Where(galgame => !galgame.CheckExist()).ToList();
        foreach (Galgame galgame in toRemove)
            _galgames.Remove(galgame);
        _galgames.ForEach(g => _galgameMap.Add(g.Path, g));
        GalgameLoadedEvent?.Invoke();
        UpdateDisplay(UpdateType.Init);
    }

    /// <summary>
    /// 可能不同版本行为不同，需要对已存储的galgame进行升级
    /// </summary>
    private async Task Upgrade()
    {
        if (await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.IdFromMixedUpgraded) == false)
        {
            foreach (Galgame galgame in _galgames)
                galgame.UpdateIdFromMixed();
            await LocalSettingsService.SaveSettingAsync(KeyValues.IdFromMixedUpgraded, true);
        }
    }

    /// <summary>
    /// 排序并更新显示的列表
    /// </summary>
    public void Sort()
    {
        UpdateDisplay(UpdateType.Sort);
    }

    public enum AddGalgameResult
    {
        Success,
        AlreadyExists,
        NotFoundInRss
    }

    /// <summary>
    /// 移除一个galgame
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <param name="removeFromDisk">是否要从硬盘移除游戏</param>
    public async Task RemoveGalgame(Galgame galgame, bool removeFromDisk = false)
    {
        _galgames.Remove(galgame);
        _galgameMap.Remove(galgame.Path);
        UpdateDisplay(UpdateType.Remove, galgame);
        if (removeFromDisk)
            galgame.Delete();
        GalgameDeletedEvent?.Invoke(galgame);
        await SaveGalgamesAsync();
    }

    /// <summary>
    /// 试图添加一个galgame，若已存在则不添加
    /// </summary>
    /// <param name="path">galgame路径</param>
    /// <param name="isForce">是否强制添加（若RSS源中找不到相关游戏信息）</param>
    public async Task<AddGalgameResult> TryAddGalgameAsync(string path, bool isForce = false)
    {
        if (_galgames.Any(gal => gal.Path == path))
            return AddGalgameResult.AlreadyExists;

        Galgame galgame;
        var metaFolder = Path.Combine(path, Galgame.MetaPath);
        if (Path.Exists(Path.Combine(metaFolder, "meta.json"))) // 有元数据备份
        {
            galgame =  _fileService.Read<Galgame>(metaFolder, "meta.json");
            Galgame.ResolveMeta(galgame, metaFolder);
            PhrasedEvent?.Invoke();
        }
        else
        {
            galgame = new(path);
            var pattern = await LocalSettingsService.ReadSettingAsync<string>(KeyValues.RegexPattern) ?? ".+";
            var regexIndex = await LocalSettingsService.ReadSettingAsync<int>(KeyValues.RegexIndex);
            var removeBorder = await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.RegexRemoveBorder);
            galgame.Name.Value = NameRegex.GetName(galgame.Name!, pattern, removeBorder, regexIndex);
            if (string.IsNullOrEmpty(galgame.Name)) return AddGalgameResult.NotFoundInRss;

            galgame = await PhraseGalInfoAsync(galgame);
            if (!isForce && galgame.RssType == RssType.None)
                return AddGalgameResult.NotFoundInRss;
        }
        
        _galgames.Add(galgame);
        _galgameMap.Add(galgame.Path, galgame);
        GalgameAddedEvent?.Invoke(galgame);
        await SaveGalgamesAsync(galgame);
        UpdateDisplay(UpdateType.Add, galgame);
        return galgame.RssType == RssType.None ? AddGalgameResult.NotFoundInRss : AddGalgameResult.Success;
    }
    
    /// <summary>
    /// 从下载源获取这个galgame的信息，并获取游玩状态（若设置里开启）
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <param name="rssType">信息源，若设置为None则使用galgame指定的数据源，若不存在则使用设置中的默认数据源</param>
    /// <returns>获取信息后的galgame，如果信息源不可达则galgame保持不变</returns>
    public async Task<Galgame> PhraseGalInfoAsync(Galgame galgame, RssType rssType = RssType.None)
    {
        IsPhrasing = true;
        RssType selectedRss = rssType;
        if(selectedRss == RssType.None)
            selectedRss = galgame.RssType == RssType.None ? await LocalSettingsService.ReadSettingAsync<RssType>(KeyValues.RssType) : galgame.RssType;
        Galgame result = await PhraserAsync(galgame, PhraserList[(int)selectedRss]);
        if (await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.SyncPlayStatusWhenPhrasing))
            await DownLoadPlayStatusAsync(galgame, RssType.Bangumi);
        await LocalSettingsService.SaveSettingAsync(KeyValues.Galgames, _galgames, true);
        IsPhrasing = false;
        PhrasedEvent?.Invoke();
        PhrasedEvent2?.Invoke(galgame);
        return result;
    }

    private static async Task<Galgame> PhraserAsync(Galgame galgame, IGalInfoPhraser phraser)
    {
        Galgame? tmp = await phraser.GetGalgameInfo(galgame);
        if (tmp == null) return galgame;

        galgame.RssType = phraser.GetPhraseType();
        galgame.Id = tmp.Id;
        if (phraser is MixedPhraser)
            galgame.UpdateIdFromMixed();
        galgame.Description.Value = tmp.Description.Value;
        if (tmp.Developer != Galgame.DefaultString)
            galgame.Description.Value = tmp.Description.Value;
        if (tmp.Developer != Galgame.DefaultString)
            galgame.Developer.Value = tmp.Developer.Value;
        if (tmp.ExpectedPlayTime != Galgame.DefaultString)
            galgame.ExpectedPlayTime.Value = tmp.ExpectedPlayTime.Value;
        if (await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.OverrideLocalName))
        {
            if (await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.OverrideLocalNameWithChinese))
            {
                galgame.Name.Value = !string.IsNullOrEmpty(tmp.CnName) ? tmp.CnName : tmp.Name.Value;
            }
            else
            {
                galgame.Name.Value = tmp.Name.Value;
            }
        }
        galgame.ImageUrl = tmp.ImageUrl;
        galgame.Rating.Value = tmp.Rating.Value;
        galgame.Tags.Value = tmp.Tags.Value;
        galgame.ImagePath.Value = await DownloadHelper.DownloadAndSaveImageAsync(galgame.ImageUrl) ?? Galgame.DefaultImagePath;
        galgame.ReleaseDate = tmp.ReleaseDate.Value;
        galgame.CheckSavePosition();
        return galgame;
    }
    
    /// <summary>
    /// 下载某个游戏的游玩状态
    /// </summary>
    /// <param name="galgame">游戏</param>
    /// <param name="source">下载源</param>
    /// <returns>(下载结果，结果解释)</returns>
    public async Task<(GalStatusSyncResult, string)> DownLoadPlayStatusAsync(Galgame galgame, RssType source)
    {
        if (source == RssType.Bangumi && PhraserList[(int)RssType.Bangumi] is BgmPhraser bgmPhraser)
            return await bgmPhraser.DownloadAsync(galgame);
        return (GalStatusSyncResult.Other, "这个数据源不支持同步游玩状态");
    }

    /// <summary>
    /// 从某个信息源下载所有游戏的游玩状态
    /// </summary>
    /// <param name="source">信息源</param>
    /// <returns>(结果，结果解释)</returns>
    public async Task<(GalStatusSyncResult ,string)> DownloadAllPlayStatus(RssType source)
    {
        var msg = string.Empty;
        GalStatusSyncResult result = GalStatusSyncResult.Other;
        IGalInfoPhraser phraser = PhraserList[(int)source];
        if (phraser is IGalStatusSync sync)
            (result, msg) = await sync.DownloadAllAsync(_galgames);
        return (result, msg);
    }

    /// <summary>
    /// 获取要显示的galgame列表
    /// </summary>
    public async Task<ObservableCollection<Galgame>> GetContentGridDataAsync()
    {
        await Task.CompletedTask;
        return _displayGalgames;
    }

    /// <summary>
    /// 向信息源上传游玩状态
    /// </summary>
    /// <param name="galgame">要同步的游戏</param>
    /// <param name="rssType">信息源</param>
    /// <returns>(上传结果， 结果解释)</returns>
    /// <exception cref="NotSupportedException">若信息源没有实现IGalStatusSync，则抛此异常</exception>
    public async Task<(GalStatusSyncResult, string)> UploadPlayStatusAsync(Galgame galgame, RssType rssType)
    {
        IGalInfoPhraser phraser = PhraserList[(int)rssType];
        if (phraser is IGalStatusSync syncer)
            return await syncer.UploadAsync(galgame);
        throw new NotSupportedException("这个数据源不支持同步游玩状态");
    }

    /// <summary>
    /// 获取所有galgame
    /// </summary>
    public List<Galgame> Galgames => _galgames;

    /// <summary>
    /// 搜索galgame并更新显示列表
    /// </summary>
    /// <param name="searchKey">搜索关键字</param>
    public void Search(string searchKey)
    {
        _searchKey = searchKey;
        UpdateDisplay(UpdateType.ApplySearch);
    }

    /// <summary>
    /// 获取搜索关键字(的clone)
    /// </summary>
    public string GetSearchKey()
    {
        return (string)_searchKey.Clone();
    }

    /// <summary>
    /// 从路径获取galgame
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns>galgame,若找不到则返回null</returns>
    public Galgame? GetGalgameFromPath(string path)
    {
        return _galgameMap.TryGetValue(path, out Galgame? result) ? result : null;
    }

    /// <summary>
    /// 从id获取galgame
    /// </summary>
    /// <param name="id">id</param>
    /// <param name="rssType">id的信息源</param>
    /// <returns>galgame，若找不到返回null</returns>
    public Galgame? GetGalgameFromId(string id, RssType rssType)
    {
        return _galgames.FirstOrDefault(g => g.Ids[(int)rssType] == id);
    }
    
    /// <summary>
    /// 保存galgame列表（以及其内部的galgame）
    /// </summary>
    /// <param name="galgame">
    /// 要指定保存的galgame，若为null则不做文件夹保存，只做缓存保存 <br/>
    /// 如果设置中没有打开保存备份则不会保存到游戏文件夹
    /// </param>
    public async Task SaveGalgamesAsync(Galgame? galgame = null)
    {
        if (galgame != null && await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.SaveBackupMetadata))
            await SaveMetaAsync(galgame);
        await LocalSettingsService.SaveSettingAsync(KeyValues.Galgames, _galgames, true);
    }
    
    /// <summary>
    /// 保存galgame的信息备份（包括meta.json和封面图）
    /// </summary>
    /// <param name="galgame"></param>
    private async Task SaveMetaAsync(Galgame galgame)
    {
        if (await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.SaveBackupMetadata) == false) return;
        _fileService.Save(galgame.GetMetaPath(), "meta.json", galgame.GetMetaCopy());
        var imagePath = Path.Combine(galgame.Path, Galgame.MetaPath);
        imagePath = Path.Combine(imagePath, Path.GetFileName(galgame.ImagePath));
        if(galgame.ImagePath.Value != Galgame.DefaultImagePath && !File.Exists(imagePath))
            File.Copy(galgame.ImagePath.Value!, imagePath);
    }

    /// <summary>
    /// 保存所有galgame的信息备份（包括meta.json和封面图）
    /// </summary>
    public async Task SaveAllMetaAsync()
    {
        foreach (Galgame galgame in _galgames)
        {
            MetaSavedEvent?.Invoke(galgame);
            await SaveMetaAsync(galgame);
        }
    }

    /// <summary>
    /// 获取galgame的存档文件夹
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <returns>存档文件夹地址，若用户取消返回null</returns>
    private async Task<string?> GetGalgameSaveAsync(Galgame galgame)
    {
        List<string> subFolders = galgame.GetSubFolders();
        FolderPickerDialog dialog = new(App.MainWindow.Content.XamlRoot, "GalgameCollectionService_SelectSavePosition".GetLocalized(), subFolders);
        return await dialog.ShowAndAwaitResultAsync();
    }
    
    /// <summary>
    /// 获取并设置galgame的可执行文件
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <returns>可执行文件地址，如果用户取消或找不到可执行文件则返回null</returns>
    public async Task<string?> GetGalgameExeAsync(Galgame galgame)
    {
        List<string> exes = galgame.GetExesAndBats();
        switch (exes.Count)
        {
            case 0:
            {
                ContentDialog dialog = new()
                {
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Title = "Error".GetLocalized(),
                    Content = "GalgameCollectionService_NotExeFounded".GetLocalized(),
                    PrimaryButtonText = "Yes".GetLocalized()
                };
                await dialog.ShowAsync();
                return null;
            }
            case 1:
                galgame.ExePath = exes[0];
                break;
            default:
            {
                FilePickerDialog dialog = new(App.MainWindow.Content.XamlRoot, "GalgameCollectionService_SelectExe".GetLocalized(), exes);
                await dialog.ShowAsync();
                if (dialog.SelectedFile == null) return null;
                galgame.ExePath = dialog.SelectedFile;
                break;
            }
        }
        return galgame.ExePath;
    }

    /// <summary>
    /// 转换存档位置
    /// </summary>
    /// <param name="galgame">galgame</param>
    public async Task ChangeGalgameSavePosition(Galgame galgame)
    {
        if (galgame.CheckSavePosition()) //目前在云端
        {
            await Task.Run(() =>
            {
                FolderOperations.ConvertSymbolicLinksToActual(galgame.Path);
            });
        }
        else //目前在本地
        {
            var remoteRoot = await LocalSettingsService.ReadSettingAsync<string>(KeyValues.RemoteFolder);
            if (string.IsNullOrEmpty(remoteRoot))
            {
                ContentDialog dialog = new()
                {
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Title = "Error".GetLocalized(),
                    Content = "GalgameCollectionService_CloudRootNotSet".GetLocalized(),
                    PrimaryButtonText = "Yes".GetLocalized()
                };
                await dialog.ShowAsync();
                return;
            }
            var localSavePath = await GetGalgameSaveAsync(galgame);
            if (localSavePath == null) return;
            var tmp = localSavePath[..localSavePath.LastIndexOf('\\')];
            var target = tmp[tmp.LastIndexOf('\\')..] + localSavePath[localSavePath.LastIndexOf('\\')..];
            remoteRoot += target;

            try
            {
                if (new DirectoryInfo(remoteRoot).Exists) //云端已存在同名文件夹
                {
                    var choose = 0;
                    ContentDialog dialog = new()
                    {
                        XamlRoot = App.MainWindow.Content.XamlRoot,
                        Title = "GalgameCollectionService_SelectOperateTitle".GetLocalized(),
                        Content = "GalgameCollectionService_SelectOperateMsg".GetLocalized(),
                        PrimaryButtonText = "GalgameCollectionService_Local".GetLocalized(),
                        SecondaryButtonText = "GalgameCollectionService_Cloud".GetLocalized(),
                        CloseButtonText = "Cancel".GetLocalized()
                    };
                    dialog.PrimaryButtonClick += (_, _) => choose = 1;
                    dialog.SecondaryButtonClick += (_, _) => choose = 2;
                    await dialog.ShowAsync();
                    if (choose == 1)
                    {
                        new DirectoryInfo(remoteRoot).Delete(true); //删除云端文件夹
                        FolderOperations.CreateSymbolicLink(localSavePath, remoteRoot);
                    }
                    else if (choose == 2)
                    {
                        new DirectoryInfo(localSavePath).Delete(true); //删除本地文件夹
                        Directory.CreateSymbolicLink(localSavePath, remoteRoot);
                    }
                }
                else
                    FolderOperations.CreateSymbolicLink(localSavePath, remoteRoot);
            }
            catch (Exception e) //创建符号链接失败，把存档复制回去
            {
                if(Directory.Exists(localSavePath))
                    Directory.Delete(localSavePath, true);
                FolderOperations.Copy(remoteRoot, localSavePath);
                //弹出提示框
                StackPanel stackPanel = new();
                stackPanel.Children.Add(new TextBlock {Text = "GalgameCollectionService_CreateSymbolicLinkFailed".GetLocalized()});
                stackPanel.Children.Add(new TextBlock {Text = e.Message});
                ContentDialog dialog = new()
                {
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Title = "Error".GetLocalized(),
                    Content = stackPanel,
                    PrimaryButtonText = "Yes".GetLocalized()
                };
                await dialog.ShowAsync();
            }
        }
    }

    /// <summary>
    /// 获取并设置galgame排序的关键字
    /// </summary>
    public async Task SetSortKeysAsync()
    {
        List<SortKeys> sortKeysList = new()
        {
            SortKeys.Name,
            SortKeys.Developer,
            SortKeys.Rating,
            SortKeys.LastPlay,
            SortKeys.ReleaseDate
        };
        ContentDialog dialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = "排序",
            PrimaryButtonText = "Yes".GetLocalized(),
            SecondaryButtonText = "Cancel".GetLocalized(),
        };
        
        ComboBox comboBox1 = new()
        {
            Header = "第一关键字",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = sortKeysList,
            Margin = new Thickness(0, 0, 5, 0),
            SelectedItem = SortKeysList[0]
        };
        ToggleSwitch toggleSwitch1 = new()
        {
            Header = "降序/升序",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(5, 0, 0, 0),
            OnContent = "升序",
            OffContent = "降序",
            IsOn = SortKeysAscending[0]
        };
        StackPanel panel1 = new ();
        panel1.Children.Add(comboBox1);
        panel1.Children.Add(toggleSwitch1);
        Grid.SetColumn(panel1, 0 );
        
        ComboBox comboBox2 = new()
        {
            Header = "第二关键字",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = sortKeysList,
            Margin = new Thickness(0, 0, 5, 0),
            SelectedItem = SortKeysList[1]
        };
        ToggleSwitch toggleSwitch2 = new()
        {
            Header = "降序/升序",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(5, 0, 0, 0),
            OnContent = "升序",
            OffContent = "降序",
            IsOn = SortKeysAscending[1]
        };
        StackPanel panel2 = new ();
        panel2.Children.Add(comboBox2);
        panel2.Children.Add(toggleSwitch2);
        Grid.SetColumn(panel2, 1 );
        

        dialog.PrimaryButtonClick += async (_, _) =>
        {
            SortKeysList = new[] { (SortKeys)comboBox1.SelectedItem, (SortKeys)comboBox2.SelectedItem };
            SortKeysAscending = new []{toggleSwitch1.IsOn, toggleSwitch2.IsOn};
            await LocalSettingsService.SaveSettingAsync(KeyValues.SortKeys, SortKeysList);
            await LocalSettingsService.SaveSettingAsync(KeyValues.SortKeysAscending, SortKeysAscending);
            Sort();
        };
        Grid content = new();
        content.ColumnDefinitions.Add(new ColumnDefinition{Width = new GridLength(1, GridUnitType.Star)});
        content.ColumnDefinitions.Add(new ColumnDefinition{Width = new GridLength(1, GridUnitType.Star)});
        content.Children.Add(panel1);
        content.Children.Add(panel2);
        dialog.Content = content;
        await dialog.ShowAsync();
    }

    /// <summary>
    /// 从设置中读取bangumi的设置
    /// </summary>
    private async Task<BgmPhraserData> GetBgmData()
    {
        BgmPhraserData data = new()
        {
            Token = (await LocalSettingsService.ReadSettingAsync<BgmAccount>(KeyValues.BangumiOAuthState))?.BangumiAccessToken ?? ""
        };
        return data;
    }

    private async Task OnSettingChanged(string key)
    {
        switch (key)
        {
            case KeyValues.BangumiOAuthState:
                PhraserList[(int)RssType.Bangumi].UpdateData(await GetBgmData());
                break;
            case KeyValues.SortKeys:
                SortKeysList = await LocalSettingsService.ReadSettingAsync<SortKeys[]>(KeyValues.SortKeys) ?? new[]
                {
                    SortKeys.Name,
                    SortKeys.Rating
                };
                break;
            case KeyValues.SortKeysAscending:
                SortKeysAscending = await LocalSettingsService.ReadSettingAsync<bool[]>(KeyValues.SortKeysAscending) ?? new[]
                {
                    false,
                    false
                };
                break;
        }
    }
}

public class FilePickerDialog : ContentDialog
{
    public string? SelectedFile
    {
        get; private set;
    }

    public FilePickerDialog(XamlRoot xamlRoot, string title, List<string> files)
    {
        XamlRoot = xamlRoot;
        Title = title;
        Content = CreateContent(files);
        PrimaryButtonText = "Yes".GetLocalized();
        SecondaryButtonText = "Cancel".GetLocalized();

        IsPrimaryButtonEnabled = false;

        PrimaryButtonClick += (_, _) => { };
        SecondaryButtonClick += (_, _) => { SelectedFile = null; };
    }

    private UIElement CreateContent(List<string> files)
    {
        StackPanel stackPanel = new();
        foreach (var file in files)
        {
            RadioButton radioButton = new()
            {
                Content = file,
                GroupName = "ExeFiles"
            };
            radioButton.Checked += RadioButton_Checked;
            stackPanel.Children.Add(radioButton);
        }
        return stackPanel;
    }

    private void RadioButton_Checked(object sender, RoutedEventArgs e)
    {
        RadioButton radioButton = (RadioButton)sender;
        SelectedFile = radioButton.Content.ToString()!;
        IsPrimaryButtonEnabled = true;
    }
}

public class FolderPickerDialog : ContentDialog
{
    private string? _selectedFolder;
    private readonly TaskCompletionSource<string?> _folderSelectedTcs = new TaskCompletionSource<string?>();
    public FolderPickerDialog(XamlRoot xamlRoot, string title, List<string> files)
    {
        XamlRoot = xamlRoot;
        Title = title;
        Content = CreateContent(files);
        PrimaryButtonText = "Yes".GetLocalized();
        //todo: 自定义文件夹需要保存存档位置，暂时关闭这个功能
        // SecondaryButtonText = "GalgameCollectionService_FolderPickerDialog_ChoseAnotherFolder".GetLocalized();
        CloseButtonText = "Cancel".GetLocalized();
        IsPrimaryButtonEnabled = false;
        PrimaryButtonClick += (_, _) => { _folderSelectedTcs.TrySetResult(_selectedFolder); };
        // SecondaryButtonClick += async (_, _) =>
        // {
        //     var folderPicker = new FolderPicker();
        //     folderPicker.FileTypeFilter.Add("*");
        //     WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, App.MainWindow.GetWindowHandle());
        //     var folder = await folderPicker.PickSingleFolderAsync();
        //     if (folder != null)
        //     {
        //         _selectedFolder = folder.Path;
        //         _folderSelectedTcs.TrySetResult(folder.Path);
        //     }
        //     else
        //         _folderSelectedTcs.TrySetResult(null);
        // };
        CloseButtonClick += (_, _) => { _folderSelectedTcs.TrySetResult(null); };
    }
    private UIElement CreateContent(List<string> files)
    {
        StackPanel stackPanel = new();
        foreach (var file in files)
        {
            RadioButton radioButton = new()
            {
                Content = file,
                GroupName = "ExeFiles"
            };
            radioButton.Checked += RadioButton_Checked;
            stackPanel.Children.Add(radioButton);
        }
        return stackPanel;
    }
    private void RadioButton_Checked(object sender, RoutedEventArgs e)
    {
        RadioButton radioButton = (RadioButton)sender;
        _selectedFolder = radioButton.Content.ToString()!;
        IsPrimaryButtonEnabled = true;
    }
    public async Task<string?> ShowAndAwaitResultAsync()
    {
        await ShowAsync();
        return await _folderSelectedTcs.Task;
    }
}
