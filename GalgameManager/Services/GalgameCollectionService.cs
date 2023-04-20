using System.Collections.ObjectModel;
using System.Diagnostics;

using Windows.Storage;
using Windows.Storage.Pickers;

using GalgameManager.Contracts.Phrase;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

// ReSharper disable EnforceForeachStatementBraces
// ReSharper disable EnforceIfStatementBraces
public class GalgameCollectionService : IDataCollectionService<Galgame>
{
    private ObservableCollection<Galgame> _galgames = new();
    private static ILocalSettingsService LocalSettingsService { get; set; } = null!;
    private readonly IJumpListService _jumpListService;
    public delegate void GalgameAddedEventHandler(Galgame galgame);
    public event GalgameAddedEventHandler? GalgameAddedEvent; //当有galgame添加时触发
    public event VoidDelegate? GalgameLoadedEvent; //当galgame列表加载完成时触发
    public event VoidDelegate? PhrasedEvent; //当有galgame信息下载完成时触发
    public bool IsPhrasing;
    private bool _isInit;

    private IGalInfoPhraser[] PhraserList
    {
        get;
    } = new IGalInfoPhraser[5];

    public GalgameCollectionService(ILocalSettingsService localSettingsService, IJumpListService jumpListService)
    {
        LocalSettingsService = localSettingsService;
        _jumpListService = jumpListService;
        PhraserList[(int)RssType.Bangumi] = new BgmPhraser(localSettingsService);
        PhraserList[(int)RssType.Vndb] = new VndbPhraser();

        App.MainWindow.AppWindow.Closing += async (_, _) =>
        { 
            await SaveGalgamesAsync();
        };
    }

    private async Task GetGalgames()
    {
        _galgames = await LocalSettingsService.ReadSettingAsync<ObservableCollection<Galgame>>(KeyValues.Galgames, true) ?? new ObservableCollection<Galgame>();
        var toRemove = _galgames.Where(galgame => !galgame.CheckExist()).ToList();
        foreach (var galgame in toRemove)
            _galgames.Remove(galgame);
        _isInit = true;
        GalgameLoadedEvent?.Invoke();
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
        if (removeFromDisk)
            galgame.Delete();
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

        var galgame = new Galgame(path);
        galgame = await PhraseGalInfoAsync(galgame);
        if (!isForce && galgame.RssType == RssType.None)
            return AddGalgameResult.NotFoundInRss;
        _galgames.Add(galgame);
        GalgameAddedEvent?.Invoke(galgame);
        await SaveGalgamesAsync();
        //为了防止在Home添加游戏的时候galgameFolderService还没有初始化，把要加的库暂存起来
        var libToCheck = await LocalSettingsService.ReadSettingAsync<List<string>>(KeyValues.LibToCheck) ?? new List<string>();
        var libPath = galgame.Path[..galgame.Path.LastIndexOf('\\')];
        if (!libToCheck.Contains(libPath))
        {
            libToCheck.Add(libPath);
            await LocalSettingsService.SaveSettingAsync(KeyValues.LibToCheck, libToCheck, true);
        }

        return galgame.RssType == RssType.None ? AddGalgameResult.NotFoundInRss : AddGalgameResult.Success;
    }


    /// <summary>
    /// 从下载源获取这个galgame的信息
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <param name="rssType">信息源，若设置为None则使用galgame指定的数据源，若不存在则使用设置中的默认数据源</param>
    /// <returns>获取信息后的galgame，如果信息源不可达则galgame保持不变</returns>
    public async Task<Galgame> PhraseGalInfoAsync(Galgame galgame, RssType rssType = RssType.None)
    {
        IsPhrasing = true;
        var selectedRss = rssType;
        if(selectedRss == RssType.None)
            selectedRss = galgame.RssType == RssType.None ? await LocalSettingsService.ReadSettingAsync<RssType>(KeyValues.RssType) : galgame.RssType;
        var result = await PhraserAsync(galgame, PhraserList[(int)selectedRss]);
        await LocalSettingsService.SaveSettingAsync(KeyValues.Galgames, _galgames, true);
        IsPhrasing = false;
        PhrasedEvent?.Invoke();
        return result;
    }

    private static async Task<Galgame> PhraserAsync(Galgame galgame, IGalInfoPhraser phraser)
    {
        var tmp = await phraser.GetGalgameInfo(galgame);
        if (tmp == null) return galgame;

        galgame.RssType = phraser.GetPhraseType();
        galgame.Id = tmp.Id;
        if (!galgame.Description.IsLock)
            galgame.Description.Value = tmp.Description.Value;
        if (tmp.Developer != Galgame.DefaultString && !galgame.Developer.IsLock)
            galgame.Developer.Value = tmp.Developer.Value;
        if (tmp.ExpectedPlayTime != Galgame.DefaultString && !galgame.ExpectedPlayTime.IsLock)
            galgame.ExpectedPlayTime.Value = tmp.ExpectedPlayTime.Value;
        if (await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.OverrideLocalName))
            galgame.Name.Value = tmp.Name.Value;
        galgame.ImageUrl = tmp.ImageUrl;
        if (!galgame.Rating.IsLock)
            galgame.Rating.Value = tmp.Rating.Value;
        if (!galgame.Tags.IsLock)
            galgame.Tags.Value = tmp.Tags.Value;
        if (!galgame.ImagePath.IsLock)
            galgame.ImagePath.Value = await DownloadAndSaveImageAsync(galgame.ImageUrl) ?? Galgame.DefaultImagePath;
        galgame.CheckSavePosition();
        return galgame;
    }

    private static async Task<string?> DownloadAndSaveImageAsync(string? imageUrl)
    {
        if (imageUrl == null) return null;
        var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(imageUrl);
        response.EnsureSuccessStatusCode();

        var imageBytes = await response.Content.ReadAsByteArrayAsync();

        var localFolder = ApplicationData.Current.LocalFolder;
        var fileName = imageUrl[(imageUrl.LastIndexOf('/') + 1)..];
        var storageFile = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

        await using (var fileStream = await storageFile.OpenStreamForWriteAsync())
        {
            using var memoryStream = new MemoryStream(imageBytes);
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(fileStream);
        }

        // 返回本地文件的路径
        return storageFile.Path;
    }

    public async Task<ObservableCollection<Galgame>> GetContentGridDataAsync()
    {
        if (!_isInit)
        {
            await GetGalgames();
            await _jumpListService.CheckJumpListAsync(_galgames.ToList());
        }
        return _galgames;
    }

    /// <summary>
    /// 保存galgame列表（以及其内部的galgame）
    /// </summary>
    public async Task SaveGalgamesAsync()
    {
        await LocalSettingsService.SaveSettingAsync(KeyValues.Galgames, _galgames, true);
    }

    /// <summary>
    /// 获取galgame的存档文件夹
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <returns>存档文件夹地址，若用户取消返回null</returns>
    private async Task<string?> GetGalgameSaveAsync(Galgame galgame)
    {
        var subFolders = galgame.GetSubFolders();
        var dialog = new FolderPickerDialog(App.MainWindow.Content.XamlRoot, "选择存档文件夹", subFolders);
        return await dialog.ShowAndAwaitResultAsync();
    }
    
    /// <summary>
    /// 获取并设置galgame的可执行文件
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <returns>可执行文件地址，如果用户取消或找不到可执行文件则返回null</returns>
    public async Task<string?> GetGalgameExeAsync(Galgame galgame)
    {
        var exes = galgame.GetExes();
        switch (exes.Count)
        {
            case 0:
            {
                var dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "未找到可执行文件",
                    PrimaryButtonText = "确定"
                };
                await dialog.ShowAsync();
                return null;
            }
            case 1:
                galgame.ExePath = exes[0];
                break;
            default:
            {
                var dialog = new FilePickerDialog(App.MainWindow.Content.XamlRoot, "选择可执行文件", exes);
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
                var dialog = new ContentDialog
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

            if (new DirectoryInfo(remoteRoot).Exists) //云端已存在同名文件夹
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = App.MainWindow.Content.XamlRoot,
                    Title = "GalgameCollectionService_SelectOperateTitle".GetLocalized(),
                    Content = "GalgameCollectionService_SelectOperateMsg".GetLocalized(),
                    PrimaryButtonText = "GalgameCollectionService_Local".GetLocalized(),
                    SecondaryButtonText = "GalgameCollectionService_Cloud".GetLocalized(),
                    CloseButtonText = "Cancel".GetLocalized()
                };
                dialog.PrimaryButtonClick += (_, _) =>
                {
                    new DirectoryInfo(remoteRoot).Delete(true); //删除云端文件夹
                    FolderOperations.CreateSymbolicLink(localSavePath, remoteRoot);
                };
                dialog.SecondaryButtonClick += (_, _) =>
                {
                    new DirectoryInfo(localSavePath).Delete(true); //删除本地文件夹
                    Process.Start("cmd.exe", $"/c mklink /d \"{localSavePath}\" \"{remoteRoot}\"");
                };
                await dialog.ShowAsync();
            }
            else
                FolderOperations.CreateSymbolicLink(localSavePath, remoteRoot);
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
        var stackPanel = new StackPanel();
        foreach (var file in files)
        {
            var radioButton = new RadioButton
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
        var radioButton = (RadioButton)sender;
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
        SecondaryButtonText = "GalgameCollectionService_FolderPickerDialog_ChoseAnotherFolder".GetLocalized();
        CloseButtonText = "Cancel".GetLocalized();
        IsPrimaryButtonEnabled = false;
        PrimaryButtonClick += (_, _) => { _folderSelectedTcs.TrySetResult(_selectedFolder); };
        SecondaryButtonClick += async (_, _) =>
        {
            var folderPicker = new FolderPicker();
            folderPicker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, App.MainWindow.GetWindowHandle());
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                _selectedFolder = folder.Path;
                _folderSelectedTcs.TrySetResult(folder.Path);
            }
            else
                _folderSelectedTcs.TrySetResult(null);
        };
        CloseButtonClick += (_, _) => { _folderSelectedTcs.TrySetResult(null); };
    }
    private UIElement CreateContent(List<string> files)
    {
        var stackPanel = new StackPanel();
        foreach (var file in files)
        {
            var radioButton = new RadioButton
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
        var radioButton = (RadioButton)sender;
        _selectedFolder = radioButton.Content.ToString()!;
        IsPrimaryButtonEnabled = true;
    }
    public async Task<string?> ShowAndAwaitResultAsync()
    {
        await ShowAsync();
        return await _folderSelectedTcs.Task;
    }
}