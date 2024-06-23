using System.Configuration;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
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

    private readonly JsonSerializerSettings _serializerSettings;

    private IDictionary<string, object> _settings;

    private bool _isInitialized;
    private bool _isUpgrade;
    
    public event ILocalSettingsService.Delegate? OnSettingChanged;

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        LocalSettingsOptions op = options.Value;

        _serializerSettings = new JsonSerializerSettings();
        _serializerSettings.Converters.Add(new GalgameSourceCustomConverter());

        _applicationDataFolder = ApplicationData.Current.LocalFolder.Path;
        _localsettingsFile = op.LocalSettingsFile ?? ErrorFileName;

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
        await UpgradeSaveFormat();
        foreach(var path in Directory.GetFiles(_applicationDataFolder, "data.*.json"))
        {
            var key = Path.GetFileName(path)[5..^5];
            var content = await File.ReadAllTextAsync(path);
            _settings[key] = content; // 第一次读取时再反序列化
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

        // 大配置分离保存，而非像原先那样全部放在一个大json中
        if (await ReadSettingAsync<bool>(KeyValues.LargerFileSeparateUpgraded) == false)
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
            await SaveSettingAsync(KeyValues.LargerFileSeparateUpgraded, true);
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

    /// <summary>
    /// 读取配置
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="isLarge">是否从统一的大文件json中读取</param>
    /// <param name="converters">额外的Converter列表，会添加在默认列表之后</param>
    /// <returns>若无相关配置，且无默认配置，返回default</returns>
    public async Task<T?> ReadSettingAsync<T>(string key, bool isLarge = false, List<JsonConverter>? converters = null)
    {
        try
        {
            converters?.ForEach(c => _serializerSettings.Converters.Add(c));
            if (RuntimeHelper.IsMSIX && !isLarge)
            {
                if (ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out var obj))
                {
                    return obj is string ? JsonConvert.DeserializeObject<T>(obj.ToString()!, _serializerSettings) : default;
                }
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
            // 无论如何都要移除新增的converter，防止崩溃保存的时候用到不应该用的converter
            converters?.ForEach(c => _serializerSettings.Converters.Remove(c));
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

    /// <summary>
    /// 保存配置
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="value">value</param>
    /// <param name="isLarge">是否从统一的保存到大文件json中</param>
    /// <param name="triggerEventWhenNull">当value为null时是否要触发OnSettingChanged事件</param>
    /// <param name="converters">额外的Converter列表</param>
    public async Task SaveSettingAsync<T>(string key, T value, bool isLarge = false, bool triggerEventWhenNull = false,
        List<JsonConverter>? converters = null)
    {
        try
        {
            converters?.ForEach(c => _serializerSettings.Converters.Add(c));
            if (RuntimeHelper.IsMSIX && !isLarge)
            {
                ApplicationData.Current.LocalSettings.Values[key] = JsonConvert.SerializeObject(value, _serializerSettings);
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
            // 无论如何都要移除新增的converter，防止崩溃保存的时候用到不应该用的converter
            converters?.ForEach(c => _serializerSettings.Converters.Remove(c));
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
}