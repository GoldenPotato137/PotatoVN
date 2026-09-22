using System.Configuration;
using System.Diagnostics;
using Windows.Storage;
using Windows.System;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using LiteDB;
using FileAttributes = System.IO.FileAttributes;

namespace GalgameManager.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string ErrorFileName ="You_Should_Not_See_This_File.Check_AppSettingsJson.json";
    private const string TmpBackupFolderName = "Export";
    private const string FailDataFolderName = "FailData";
    private const string DatabaseFileName = "pvn_data.db";
    private const string SettingDatabaseFileName = "pvn_settings.db"; //这个数据库只在非MSIX模式下使用

    private readonly IFileService _fileService;

    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;

    private readonly JsonSerializerSettings _serializerSettings;

    private IDictionary<string, object> _settings;

    private bool _isInitialized;
    private bool _isUpgrade;

    public event ILocalSettingsService.Delegate? OnSettingChanged;
    public DirectoryInfo LocalFolder => new(AppStoragePaths.LocalDataPath);
    public DirectoryInfo TemporaryFolder => new(AppStoragePaths.TempPath);
    private DirectoryInfo SettingsFolder => new(Path.Combine(LocalFolder.FullName, "../",  "Settings"));
    public LiteDatabase Database { get; private set; } = null!;
    private LiteDatabase? _settingDatabase; //这个数据库只在非MSIX模式下使用
    private ILiteCollection<SettingItem>? _settingCollection; //这个表只在非MSIX模式下使用
    public bool IsDatabaseUsable { get; private set; }

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        LocalSettingsOptions op = options.Value;

        _serializerSettings = new JsonSerializerSettings();

        _applicationDataFolder = AppStoragePaths.LocalDataPath;
        _localsettingsFile = op.LocalSettingsFile ?? ErrorFileName;

        _settings = new Dictionary<string, object>();

        async void OnAppClosing()
        {
            IsDatabaseUsable = false;
            Database.Dispose();
            _settingDatabase?.Dispose();
            await _fileService.WaitForWriteFinishAsync();
        }

        App.OnAppClosing += OnAppClosing;
        Upgrade().Wait();
    }

    /// <summary>
    /// 仅在读大文件时调用
    /// </summary>
    /// <exception cref="ConfigurationErrorsException"></exception>
    private async Task InitializeAsync()
    {
        if (_isInitialized) return;
        await UpgradeSaveFormat();
        foreach(var path in Directory.GetFiles(_applicationDataFolder, "data.*.json"))
        {
            try
            {
                var key = Path.GetFileName(path)[5..^5];
                var content = await File.ReadAllTextAsync(path);
                _settings[key] = content; // 第一次读取时再反序列化
            }
            catch (Exception e)
            {
                App.GetService<IInfoService>().DeveloperEvent(e: e);
            }
        }
        _isInitialized = true;
    }

    /// <summary>
    /// 更新存储格式, 用于大文件
    /// </summary>
    private async Task UpgradeSaveFormat()
    {
        if (await ReadSettingAsync<bool>(KeyValues.SaveFormatUpgraded) == false)
        {
            IDictionary<string, object> old = _fileService.Read<IDictionary<string, object>>
                (_applicationDataFolder, _localsettingsFile) ??new Dictionary<string, object>();
            // 原本莫名其妙把数据序列化了两次，弱智了
            // 把被序列化两次的数据恢复过来
            Dictionary<string, object> tmp = new();
            foreach (var key in old.Keys)
                tmp[key] = JsonConvert.DeserializeObject(old[key].ToString()!)!;
            _fileService.SaveNow(_applicationDataFolder, _localsettingsFile, tmp);
            await SaveSettingAsync(KeyValues.SaveFormatUpgraded, true);
        }

        // 以上的配置均在可导出数据版本前，不需要特殊处理迁移问题

        LocalSettingStatus status = _fileService.Read<LocalSettingStatus>
            (_applicationDataFolder, $"data.{KeyValues.DataStatus}.json") ?? new();

        // 大配置分离保存，而非像原先那样全部放在一个大json中
        if (status.LargerFileSeparateUpgraded == false)
        {
            IDictionary<string, object> old = _fileService.Read<IDictionary<string, object>>
                (_applicationDataFolder, _localsettingsFile) ??new Dictionary<string, object>();
            foreach(var key in old.Keys)
            {
                _fileService.SaveWithoutJson(_applicationDataFolder, $"data.{key}.json", old[key].ToString()!);
            }
            _fileService.Delete(_applicationDataFolder, _localsettingsFile);
            _fileService.Delete(_applicationDataFolder, "LocalSettings.backup.json");
            await _fileService.WaitForWriteFinishAsync();
            status.LargerFileSeparateUpgraded = true;
            _fileService.SaveNow(_applicationDataFolder, $"data.{KeyValues.DataStatus}.json", status);
        }
    }

    /// <summary>
    /// 更新配置
    /// </summary>
    private async Task Upgrade()
    {
        if (_isUpgrade) return;
        InitSettingDatabase(); //以下为最早会读取配置的地方，在此之前必须初始化SettingDatabase
        if (await ReadSettingAsync<bool>(KeyValues.SortKeysUpgraded) == false)
        {
            SortKeys? sortKey1 = await ReadSettingAsync<SortKeys?>("sortKey1");
            SortKeys? sortKey2 = await ReadSettingAsync<SortKeys?>("sortKey2");
            if (sortKey1 != null && sortKey2 != null)
            {
                await SaveSettingAsync(KeyValues.SortKeys, new []{sortKey1.Value, sortKey2.Value});
                await SaveSettingAsync(KeyValues.SortKeysAscending, new []{false, false});
            }
            await SaveSettingAsync(KeyValues.SortKeysUpgraded, true);
        }

        _isUpgrade = true;
    }

    public void InitDatabase()
    {
        BsonMapper.Global.EnumAsInteger = true;
        BsonMapper.Global.RegisterType<Version>
        (
            serialize: v => v.ToString(),
            deserialize: b => Version.Parse(b.AsString)
        );
        Database = new(Path.Combine(LocalFolder.FullName, DatabaseFileName));
        IsDatabaseUsable = true;
    }

    public void InitSettingDatabase()
    {
        if (RuntimeHelper.IsMSIX) return;
        if (_settingDatabase is not null) return;
        if (!SettingsFolder.Exists) SettingsFolder.Create();
        _settingDatabase = new(Path.Combine(SettingsFolder.FullName, SettingDatabaseFileName));
        _settingCollection = _settingDatabase.GetCollection<SettingItem>("settings");
    }

    /// <summary>
    /// 读取配置
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="isLarge">是否从统一的大文件json中读取</param>
    /// <param name="converters">额外的Converter列表，会添加在默认列表之后</param>
    /// <param name="typeNameHandling">json配置中是否包含TypeName信息</param>
    /// <returns>若无相关配置，且无默认配置，返回default</returns>
    public async Task<T?> ReadSettingAsync<T>(string key, bool isLarge = false, List<JsonConverter>? converters = null,
        bool typeNameHandling = false)
    {
        try
        {
            converters?.ForEach(c => _serializerSettings.Converters.Add(c));
            if (typeNameHandling) _serializerSettings.TypeNameHandling = TypeNameHandling.All;
            if (RuntimeHelper.IsMSIX && !isLarge)
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
                {
                    return obj is string ? JsonConvert.DeserializeObject<T>(obj.ToString()!, _serializerSettings) : default;
                }
            }
            else if (!RuntimeHelper.IsMSIX && !isLarge)
            {
                SettingItem? settingItem = _settingCollection?.FindById(key);
                if (settingItem != null) return JsonConvert.DeserializeObject<T>(settingItem.Value, _serializerSettings);
            }
            else
            {
                await InitializeAsync();
                if (_settings.TryGetValue(key, out var obj))
                {
                    if (obj is T value) return value;
                    _settings[key] = JsonConvert.DeserializeObject<T>(obj.ToString()!, _serializerSettings)!;
                    obj = _settings[key];
                    return (T?)obj;
                }
            }
        }
        finally
        {
            _serializerSettings.TypeNameHandling = TypeNameHandling.None; // 恢复默认值
            // 无论如何都要移除新增的converter，防止崩溃保存的时候用到不应该用的converter
            converters?.ForEach(c => _serializerSettings.Converters.Remove(c));
        }

        return TryGetDefaultValue<T>(key);
    }

    public Task<T?> ReadOldSettingAsync<T>(string key, T template, JsonSerializerSettings? settings = null)
    {
        return Task.Run(() =>
        {
            var content = _fileService.ReadWithoutJson(_applicationDataFolder, $"data.{key}.json");
            if (string.IsNullOrEmpty(content)) return default;
            return JsonConvert.DeserializeAnonymousType(content, template, settings!);
        });
    }

    private T? TryGetDefaultValue<T>(string key)
    {
        switch (key)
        {
            case KeyValues.RssType:
                return (T?)(object?)RssType.Mixed;
            case KeyValues.RemoteFolder:
                var saveFolderName = "GameSaves";
                // NOTE(kuriko): 优先使用个人版 OneDrive，其次使用任意可行的 OneDrive 路径
                //      防止默认值为 SharePoint 等 OneDrive 路径
                var root = Environment.GetEnvironmentVariable("OneDriveConsumer")
                           ?? Environment.GetEnvironmentVariable("OneDrive");
                if (root.IsNullOrWhiteSpace()) return (T?)(object?)null;;

                root = root.Trim();
                var finalPath = Path.Combine(root, saveFolderName);
                if (Path.Exists(finalPath)) return (T?)(object?)finalPath;

                // Note(kuriko): 进行一次快速的搜索
                try
                {
                    var maxSearchDepth = 2;
                    var maxSearchTimeout = 500;

                    Stopwatch sw = Stopwatch.StartNew();
                    EnumerationOptions options = new()
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = true,
                        MaxRecursionDepth = maxSearchDepth,
                        AttributesToSkip = FileAttributes.ReparsePoint
                                           | FileAttributes.System | FileAttributes.Hidden,
                    };
                    IEnumerable<string> dirs = Directory.EnumerateDirectories(root, "*", options);
                    foreach (var dir in dirs)
                    {
                        if (sw.ElapsedMilliseconds > maxSearchTimeout) break;
                        if (string.Equals(Path.GetFileName(dir), saveFolderName, StringComparison.OrdinalIgnoreCase))
                        {
                            finalPath = dir;
                            break;
                        }
                    }
                } catch (Exception) { /* ignroed */ }
                return (T?)(object?)finalPath;
            case KeyValues.SortKeys:
                return (T?)(object?)new [] { SortKeys.LastPlay , SortKeys.Developer};
            case KeyValues.PrimarySortKey:
                return (T?)(object?)SortKeys.LastPlay;
            case KeyValues.SecondarySortKey:
                return (T?)(object?)SortKeys.Name;
            case KeyValues.LibrarySortKey:
                return (T?)(object?)SortKeys.LastPlay;
            case KeyValues.LibraryFolderSortKey:
                return (T?)(object?)SortKeys.Name;
            case KeyValues.PrimarySortDescending:
            case KeyValues.LibraryGameSortDescending:
                return (T?)(object?)true;
            case KeyValues.SortKeysAscending:
                return (T?)(object?)new [] { false , false};
            case KeyValues.SearchChildFolder:
                return (T?)(object?)true;
            case KeyValues.SearchChildFolderDepth:
                return (T?)(object?)1;  // 现在这个设置已被废弃
            case KeyValues.RegexPattern:
                return (T?)(object?)@".+";
            case KeyValues.GameFolderMustContain:
                return (T?)(object)".exe";
            case KeyValues.GameFolderShouldContain:
                return (T?)(object)".xp3\n.arc\n.dat\n.ini\n.dll\n.txt\n.pac\n.noa\n.sh\n.bin\n.pck";
            case KeyValues.SaveBackupMetadata:
                return (T?)(object)false;
            case KeyValues.FixHorizontalPicture:
                return (T?)(object)true;
            case KeyValues.LastNoticeUpdateVersion:
                return (T?)(object)"";
            case KeyValues.AutoCategory:
            case KeyValues.DownloadCharacters:
                return (T?)(object)true;
            case KeyValues.OverrideLocalNameWithChinese:
            case KeyValues.MemoryImprove:
                return (T?)(object)true;
            case KeyValues.MagpieHotkeys:
                return (T?)(object)new List<int>([(int)VirtualKey.LeftWindows, (int)VirtualKey.Shift, (int)VirtualKey.A]);
            case KeyValues.PlayingWindowMode:
                return (T?)(object)WindowMode.Minimize;
            case KeyValues.NotifyWhenGetGalgameInFolder:
            case KeyValues.NotifyWhenUnpackGame:
            case KeyValues.EventPvnSyncNotify:
                return (T?)(object)true;
            case KeyValues.DisplayVirtualGame:
            case KeyValues.SpecialDisplayVirtualGame:
            case KeyValues.HomeFilterShowPlayStatusAndSourcePanel:
            case KeyValues.HomeFilterShowDeveloperPanel:
            case KeyValues.HomeFilterShowTagPanel:
            case KeyValues.LibraryNavBar:
            case KeyValues.LibraryStatistics:
            case KeyValues.SyncGameCharacters:
            case KeyValues.SyncStaff:
            case KeyValues.SyncHeaderImage:
            case KeyValues.GalgamePageNewLayout:
            case KeyValues.GalgamePageNewLayout_ShowPainter:
            case KeyValues.GalgamePageNewLayout_ShowSeiyu:
            case KeyValues.GalgamePageNewLayout_ShowWriter:
            case KeyValues.GalgamePageNewLayout_ShowMusician:
            case KeyValues.GalgamePageNewLayout_ShowHeaderImage:
            case KeyValues.GalgamePageNewLayout_CoverImage:
            case KeyValues.GalgamePageNewLayout_ShowCoverWhenNoBackground:
            case KeyValues.GalgameSourcePageShowSubSourceGames:
            case KeyValues.ShowGameNameInControl:
            case KeyValues.GalgamePageNewLayout_ShowExpectedPlayTime:
            case KeyValues.GalgamePageNewLayout_ShowRating:
            case KeyValues.GalgamePageNewLayout_ShowTags:
            case KeyValues.GalgamePageNewLayout_ShowCharacters:
                return (T?)(object)true;
            case KeyValues.MixedPhraserOrder:
                LanguageEnum language = App.GetService<ILocalSettingsService>().ReadSettingAsync<LanguageEnum>(KeyValues.Language).Result;
                bool isChineseCulture = language == LanguageEnum.ChineseSimplified ||
                                        (language == LanguageEnum.Auto &&
                                         System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh"));

                return (T?)(object)new MixedPhraserOrder().SetToDefault(isChineseCulture);
            case KeyValues.DefaultGameName:
                return (T?)(object)DisplayName.Name;
            case KeyValues.GalgamePagePrimaryTitleType:
                return (T?)(object)DisplayName.ChineseName;
            case KeyValues.GalgamePageSecondaryTitleType:
                return (T?)(object)DisplayName.OriginalName;
            case KeyValues.MinPlayTimeRecordThreshold:
                return (T?)(object)5; // 默认5分钟
            case KeyValues.MixedPhraserTimeout:
                return (T?)(object)12; // 默认12秒
            case KeyValues.CustomTextFileExtensions:
                return (T?)(object)new List<string> { ".doc", ".docx", ".pdf", ".txt", ".md" };
            case KeyValues.AutoExportInterval:
                return (T?)(object)168.0;
            case KeyValues.VndbTranslateTags:
            case KeyValues.VndbCensorTags:
            case KeyValues.VndbRemoveSpoilerTags:
                return (T?)(object)true;
            default:
                return default;
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="value">value</param>
    /// <param name="isLarge">是否从统一的保存到大文件json中</param>
    /// <param name="triggerEventWhenNull">当value为null时是否要触发OnSettingChanged事件</param>
    /// <param name="converters">额外的Converter列表</param>
    /// <param name="typeNameHandling">json配置中是否包含TypeName信息</param>
    public async Task SaveSettingAsync<T>(string key, T value, bool isLarge = false, bool triggerEventWhenNull = false,
        List<JsonConverter>? converters = null, bool typeNameHandling = false)
    {
        try
        {
            if (typeNameHandling) _serializerSettings.TypeNameHandling = TypeNameHandling.All;
            converters?.ForEach(c => _serializerSettings.Converters.Add(c));
            if (RuntimeHelper.IsMSIX && !isLarge)
            {
                ApplicationData.Current.LocalSettings.Values[key] = JsonConvert.SerializeObject(value, _serializerSettings);
            }
            else if(!RuntimeHelper.IsMSIX && !isLarge)
            {
                SettingItem settingItem = _settingCollection?.FindById(key) ?? new SettingItem{Key = key};
                settingItem.Value = JsonConvert.SerializeObject(value, _serializerSettings);
                _settingCollection?.Upsert(settingItem);
            }
            else if(value!=null)
            {
                await InitializeAsync();
                _settings[key] = value;
                _fileService.Save(_applicationDataFolder, $"data.{key}.json", value, _serializerSettings);
            }
        }
        finally
        {
            _serializerSettings.TypeNameHandling = TypeNameHandling.None; // 恢复默认值
            // 无论如何都要移除新增的converter，防止崩溃保存的时候用到不应该用的converter
            converters?.ForEach(c => _serializerSettings.Converters.Remove(c));
        }

        if (value != null || triggerEventWhenNull)
            await UiThreadInvokeHelper.InvokeAsync(() => OnSettingChanged?.Invoke(key, value));
    }

    public async Task RemoveSettingAsync(string key, bool isLarge = false)
    {
        if (RuntimeHelper.IsMSIX && !isLarge)
        {
            ApplicationData.Current.LocalSettings.Values.Remove(key);
        }
        else if (!RuntimeHelper.IsMSIX && !isLarge) _settingCollection?.Delete(key);
        else
        {
            await InitializeAsync();
            _settings.Remove(key);
            _fileService.Delete(_applicationDataFolder, $"data.{key}.json");
        }
        await UiThreadInvokeHelper.InvokeAsync(() => OnSettingChanged?.Invoke(key, null));
    }

    public async Task AddToExportAsync(string key, object value, List<JsonConverter>? converters = null,
        bool typeNameHandling = false)
    {
        try
        {
            if (typeNameHandling) _serializerSettings.TypeNameHandling = TypeNameHandling.All;
            converters?.ForEach(c => _serializerSettings.Converters.Add(c));
            StorageFolder tmp = await GetTmpExportFolder();
            _fileService.Save(tmp.Path, $"data.{key}.json", value, _serializerSettings);
        }
        finally
        {
            _serializerSettings.TypeNameHandling = TypeNameHandling.None; // 恢复默认值
            // 无论如何都要移除新增的converter，防止崩溃保存的时候用到不应该用的converter
            converters?.ForEach(c => _serializerSettings.Converters.Remove(c));
        }
    }

    public async Task AddToExportDirectlyAsync(string key)
    {
        var filePath = Path.Combine(_applicationDataFolder, $"data.{key}.json");
        if (File.Exists(filePath))
        {
            StorageFolder tmp = await GetTmpExportFolder();
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            await file.CopyAsync(tmp, file.Name, NameCollisionOption.ReplaceExisting);
        }
    }

    public async Task<string?> AddImageToExportAsync(string? imagePath)
    {
        if (imagePath.IsNullOrEmpty()) return null;
        if (!File.Exists(imagePath)) return null;
        try
        {
            StorageFolder tmp = await GetTmpExportFolder();
            StorageFolder imageFolder = await tmp.CreateFolderAsync(FileHelper.FolderType.Images.ToString(),
                CreationCollisionOption.OpenIfExists);
            StorageFile image = await StorageFile.GetFileFromPathAsync(imagePath);
            StorageFile result = await image.CopyAsync(imageFolder, Path.GetFileName(imagePath),
                NameCollisionOption.GenerateUniqueName);
            return $".\\{imageFolder.Name}\\{result.Name}";
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<string?> GetImageFromImportAsync(string? imagePath)
    {
        await Task.CompletedTask; // 预留异步坑位
        if (string.IsNullOrEmpty(imagePath)) return null;
        if (Path.IsPathRooted(imagePath)) return imagePath;
        if (imagePath == Galgame.DefaultImagePath || imagePath == Galgame.DefaultCharacterImagePath) return imagePath;
        try
        {
            var path = Path.GetFullPath(Path.Combine(LocalFolder.FullName, imagePath));
            return File.Exists(path) ? path : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<StorageFolder> GetTmpExportFolder()
    {
        StorageFolder tempRoot = await StorageFolder.GetFolderFromPathAsync(TemporaryFolder.FullName);
        StorageFolder tmp = await tempRoot.CreateFolderAsync(TmpBackupFolderName, CreationCollisionOption.OpenIfExists);
        return tmp;
    }

    public async Task<string> BackupFailedDataAsync(bool removeAfterBackup = false)
    {
        DirectoryInfo failedFolder = TemporaryFolder.CreateSubdirectory(FailDataFolderName);
        await Task.Run(() =>
        {
            failedFolder.Delete(true);
            try
            {
                // 把LocalFolder所有内容移动至FailData文件夹
                FolderOperations.CopyEx(LocalFolder.FullName, failedFolder.FullName, allowDecrypted: true);
            }
            catch (Exception e)
            {
                App.GetService<IInfoService>().DeveloperEvent(e: e);
            }
            if (removeAfterBackup)
            {
                LocalFolder.Delete(true);
                LocalFolder.Create();
            }
        });
        return failedFolder.FullName;
    }

    public async Task ImportPageSettingsAsync()
    {
        try
        {
            LocalSettingStatus? status = await ReadSettingAsync<LocalSettingStatus>(KeyValues.DataStatus, true);
            if (status?.ImportPageSettings is not false) return; // 仅在来自导入且未处理过的数据上执行

            PageSettings? pageSettings = await ReadSettingAsync<PageSettings>(KeyValues.PageSettings, true);
            if (pageSettings is not null)
            {
                if (pageSettings.PrimarySortKey is { } primarySortKey)
                    await SaveSettingAsync(KeyValues.PrimarySortKey, primarySortKey);
                if (pageSettings.PrimarySortDescending is { } primaryDescending)
                    await SaveSettingAsync(KeyValues.PrimarySortDescending, primaryDescending);
                if (pageSettings.SecondarySortKey is { } secondarySortKey)
                    await SaveSettingAsync(KeyValues.SecondarySortKey, secondarySortKey);
                if (pageSettings.SecondarySortDescending is { } secondaryDescending)
                    await SaveSettingAsync(KeyValues.SecondarySortDescending, secondaryDescending);
                if (pageSettings.CustomSortOrder is { } customSortOrder)
                    await SaveSettingAsync(KeyValues.CustomSortOrder, customSortOrder, true);
                if (pageSettings.LibrarySortKey is { } librarySortKey)
                    await SaveSettingAsync(KeyValues.LibrarySortKey, librarySortKey);
                if (pageSettings.LibraryGameSortDescending is { } libraryGameSortDescending)
                    await SaveSettingAsync(KeyValues.LibraryGameSortDescending, libraryGameSortDescending);
                if (pageSettings.LibraryFolderSortKey is { } libraryFolderSortKey)
                    await SaveSettingAsync(KeyValues.LibraryFolderSortKey, libraryFolderSortKey);
                if (pageSettings.LibraryFolderSortDescending is { } libraryFolderSortDescending)
                    await SaveSettingAsync(KeyValues.LibraryFolderSortDescending, libraryFolderSortDescending);
            }

            status.ImportPageSettings = true;
            await SaveSettingAsync(KeyValues.DataStatus, status, true);
            await RemoveSettingAsync(KeyValues.PageSettings, true); // 清理仅供导入使用的临时文件
        }
        catch (Exception e)
        {
            App.GetService<IInfoService>().DeveloperEvent(e: e);
        }
    }

    public class SettingItem
    {
        [BsonId] public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
