using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace GalgameManager.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private const string DefaultApplicationDataFolder = "GalgameManager/ApplicationData";
    private const string DefaultLocalSettingsFile = "LocalSettings.json";

    private readonly IFileService _fileService;
    private readonly LocalSettingsOptions _options;

    private readonly string _localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _applicationDataFolder;
    private readonly string _localsettingsFile;

    private IDictionary<string, object> _settings;

    private bool _isInitialized;

    public LocalSettingsService(IFileService fileService, IOptions<LocalSettingsOptions> options)
    {
        _fileService = fileService;
        _options = options.Value;

        // _applicationDataFolder = Path.Combine(_localApplicationData, _options.ApplicationDataFolder ?? DefaultApplicationDataFolder);
        _applicationDataFolder = ApplicationData.Current.LocalFolder.Path;
        _localsettingsFile = _options.LocalSettingsFile ?? DefaultLocalSettingsFile;

        _settings = new Dictionary<string, object>();
    }

    private async Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _settings = _fileService.Read<IDictionary<string, object>>(_applicationDataFolder, _localsettingsFile) ?? new Dictionary<string, object>();
            _isInitialized = true;
            await Task.CompletedTask;
        }
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
                return obj is string? JsonConvert.DeserializeObject<T>(obj.ToString()!): default;
            }
        }

        return TryGetDefaultValue<T>(key);
    }

    private T? TryGetDefaultValue<T>(string key)
    {
        switch (key)
        {
            case KeyValues.RemoteFolder:
                var result = Environment.GetEnvironmentVariable("OneDrive");
                result = result==null ? null : result + "\\GameSaves";
                return (T?)(object?)result;
            case KeyValues.SortKey1:
                return (T?)(object?)SortKeys.LastPlay;
            case KeyValues.SortKey2:
                return (T?)(object?)SortKeys.Developer;
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
            default:
                return default;
        }
    }

    public async Task SaveSettingAsync<T>(string key, T value, bool isLarge = false)
    {
        if (RuntimeHelper.IsMSIX && !isLarge)
        {
            ApplicationData.Current.LocalSettings.Values[key] = JsonConvert.SerializeObject(value);
        }
        else if(value!=null)
        {
            await InitializeAsync();

            _settings[key] = JsonConvert.SerializeObject(value);

            await Task.Run(() => _fileService.Save(_applicationDataFolder, _localsettingsFile, _settings));
        }
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

            await Task.Run(() => _fileService.Save(_applicationDataFolder, _localsettingsFile, _settings));
        }
    }

}

public static class KeyValues
{
    public const string BangumiToken = "bangumiToken";
    public const string RssType = "rssType";
    public const string GalgameFolders = "galgameFolders";
    public const string Galgames = "galgames";
    public const string OverrideLocalName = "overrideLocalName";
    public const string RemoteFolder = "remoteFolder";
    public const string QuitStart = "quitStart";
    public const string SortKey1 = "sortKey1";
    public const string SortKey2 = "sortKey2";
    public const string SearchChildFolder = "searchChildFolder";
    public const string SearchChildFolderDepth = "searchChildFolderDepth";
    public const string IgnoreFetchResult = "ignoreFetchResult";
    public const string RegexPattern = "regexPattern";
    public const string RegexIndex = "regexIndex";
    public const string RegexRemoveBorder = "regexRemoveBorder";
    public const string GameFolderMustContain = "gameFolderMustContain";
    public const string GameFolderShouldContain = "gameFolderShouldContain";
    public const string FaqLastUpdate = "faqLastUpdate";
    public const string FixHorizontalPicture = "fixHorizontalPictrue";
    public const string SaveBackupMetadata = "saveBackupMetadata";
    public const string Filters = "filters";
    public const string DisplayedUpdateVersion = "displayedUpdateVersion";
}
