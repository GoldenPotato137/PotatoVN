using System.Configuration;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string ErrorFileName ="You_Should_Not_See_This_File.Check_AppSettingsJson.json";

    private readonly IFileService _fileService;

    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;
    private readonly string _localsettingsBackupFile;

    private IDictionary<string, object> _settings;

    private bool _isInitialized;
    private bool _isUpgrade;
    
    public event ILocalSettingsService.Delegate? OnSettingChanged;

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        LocalSettingsOptions op = options.Value;

        _applicationDataFolder = ApplicationData.Current.LocalFolder.Path;
        _localsettingsFile = op.LocalSettingsFile ?? ErrorFileName;
        _localsettingsBackupFile = op.BackUpSettingsFile ?? ErrorFileName;

        _settings = new Dictionary<string, object>();

        async void OnAppClosing()
        {
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
        var retry = 0;
        while (true)
        {
            if (retry > 3) //配置读取失败且无法回复，全新启动
            {
                _settings = new Dictionary<string, object>();
                return;
            }

            await UpgradeSaveFormat();

            var reset = false;
            try
            {
                _settings = _fileService.Read<IDictionary<string, object>>(_applicationDataFolder, _localsettingsFile) ?? 
                            new Dictionary<string, object>();
            }
            catch
            {
                reset = true;
            }
            
            var settingFile = Path.Combine(_applicationDataFolder, _localsettingsFile);
            var backupFile = Path.Combine(_applicationDataFolder, _localsettingsBackupFile);
            if (reset || CheckSettings() == false)
            {
                // 恢复最后一个正确的配置
                if (File.Exists(Path.Combine(_applicationDataFolder, _localsettingsBackupFile))) 
                    File.Copy(backupFile, settingFile, true);
                retry ++;
                continue;
            }

            _isInitialized = true;
            Task _ = Task.Run(() =>
            {
                // 备份配置
                File.Copy(settingFile, backupFile, true);
            });

            await Task.CompletedTask;
            break;
        }
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
    }

    /// <summary>
    /// 更新配置
    /// </summary>
    private async Task Upgrade()
    {
        if (_isUpgrade) return;
        //public const string SortKey1 = "sortKey1";
        //public const string SortKey2 = "sortKey2";
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

    public async Task<T?> ReadSettingAsync<T>(string key, bool isLarge = false)
    {
        if (RuntimeHelper.IsMSIX && !isLarge)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
            {
                return obj is string? JsonConvert.DeserializeObject<T>(obj.ToString()!): default;
            }
        }
        else
        {
            await InitializeAsync();
            if (_settings.TryGetValue(key, out var obj))
            {
                if (obj is JToken json)
                {
                    _settings[key] = json.ToObject<T>()!;
                    obj = _settings[key];
                }

                return (T?)obj;
            }
        }

        return TryGetDefaultValue<T>(key);
    }

    private T? TryGetDefaultValue<T>(string key)
    {
        switch (key)
        {
            case KeyValues.RssType:
                return (T?)(object?)RssType.Mixed;
            case KeyValues.RemoteFolder:
                var result = Environment.GetEnvironmentVariable("OneDrive");
                result = result==null ? null : result + "\\GameSaves";
                return (T?)(object?)result;
            case KeyValues.SortKeys:
                return (T?)(object?)new [] { SortKeys.LastPlay , SortKeys.Developer};
            case KeyValues.SortKeysAscending:
                return (T?)(object?)new [] { false , false};
            case KeyValues.SearchChildFolder:
                return (T?)(object?)false;
            case KeyValues.SearchChildFolderDepth:
                return (T?)(object?)1;
            case KeyValues.RegexPattern:
                return (T?)(object?)@".+";
            case KeyValues.GameFolderMustContain:
                return (T?)(object)".exe";
            case KeyValues.GameFolderShouldContain:
                return (T?)(object)".xp3\n.arc\n.dat\n.ini\n.dll\n.txt";
            case KeyValues.SaveBackupMetadata:
                return (T?)(object)true;
            case KeyValues.FixHorizontalPicture:
                return (T?)(object)true;
            case KeyValues.LastNoticeUpdateVersion:
                return (T?)(object)"";
            case KeyValues.AutoCategory:
                return (T?)(object)true;
            case KeyValues.OverrideLocalNameWithChinese:
                return (T?)(object)false;
            case KeyValues.MemoryImprove:
                return (T?)(object)true;
            case KeyValues.PlayingWindowMode:
                return (T?)(object)WindowMode.Minimize;
            case KeyValues.NotifyWhenGetGalgameInFolder:
            case KeyValues.NotifyWhenUnpackGame:
            case KeyValues.EventPvnSyncNotify:
                return (T?)(object)true;
            default:
                return default;
        }
    }

    public async Task SaveSettingAsync<T>(string key, T value, bool isLarge = false, bool triggerEventWhenNull = false)
    {
        if (RuntimeHelper.IsMSIX && !isLarge)
        {
            ApplicationData.Current.LocalSettings.Values[key] = JsonConvert.SerializeObject(value);
        }
        else if(value!=null)
        {
            await InitializeAsync();
            _settings[key] = value;
            _fileService.Save(_applicationDataFolder, _localsettingsFile, _settings);
        }

        if (value != null || triggerEventWhenNull)
            await UiThreadInvokeHelper.InvokeAsync(() => OnSettingChanged?.Invoke(key, value));
    }
    
    public async Task RemoveSettingAsync(string key)
    {
        if (RuntimeHelper.IsMSIX)
        {
            ApplicationData.Current.LocalSettings.Values.Remove(key);
        }
        else
        {
            await InitializeAsync();

            _settings.Remove(key);

            _fileService.Save(_applicationDataFolder, _localsettingsFile, _settings);
        }
        await UiThreadInvokeHelper.InvokeAsync(() => OnSettingChanged?.Invoke(key, null));
    }

    /// <summary>
    /// 检查设置是否能正常读取
    /// </summary>
    private bool CheckSettings()
    {
        try
        {
            foreach (var value in _settings.Values)
                if (value is JToken)
                    JsonConvert.DeserializeObject(value.ToString()!);
        }
        catch
        {
            return false;
        }
        return true;
    }

}