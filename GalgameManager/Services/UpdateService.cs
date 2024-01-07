using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;

namespace GalgameManager.Services;

public class UpdateService : IUpdateService
{
    private readonly bool _firstUpdate;
    private readonly ILocalSettingsService _localSettingsService;
    private const string FileName = "update.md";
    private readonly string _localFolder = ApplicationData.Current.LocalFolder.Path;

    public event Action? DownloadEvent;
    public event Action? DownloadCompletedEvent;
    public event Action<string>? DownloadFailedEvent;
    public event Action<bool>? SettingBadgeEvent;
    private string FilePath => Path.Combine(_localFolder, FileName);

    public UpdateService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        var last = localSettingsService.ReadSettingAsync<string>(KeyValues.DisplayedUpdateVersion).Result ?? "";
        _firstUpdate = last != RuntimeHelper.GetVersion();
    }

    public async Task<bool> CheckUpdateAsync()
    {
        if (await _localSettingsService.ReadSettingAsync<DateTime>(KeyValues.LastUpdateCheckDate) is var lastDate 
            && lastDate.Date == DateTime.Now.Date 
            && await _localSettingsService.ReadSettingAsync<bool>(KeyValues.LastUpdateCheckResult) == false)
        {
            return false;
        }
        
        try
        {
            HttpClient client = Utils.GetDefaultHttpClient();
            HttpResponseMessage response = await client.GetAsync(
                "https://raw.gitmirror.com/GoldenPotato137/GalgameManager/main/docs/version");
            var newestVersion = (await response.Content.ReadAsStringAsync())
                .Replace("\n", "").Replace("\r","");
            var result = newestVersion != RuntimeHelper.GetVersion();
            await _localSettingsService.SaveSettingAsync(KeyValues.LastUpdateCheckDate, DateTime.Now.Date);
            await _localSettingsService.SaveSettingAsync(KeyValues.LastUpdateCheckResult, result);
            return result;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task UpdateSettingsBadgeAsync()
    {
        if (await _localSettingsService.ReadSettingAsync<string>(KeyValues.LastNoticeUpdateVersion) !=
            RuntimeHelper.GetVersion() && await CheckUpdateAsync())
            SettingBadgeEvent?.Invoke(true);
        else
            SettingBadgeEvent?.Invoke(false);
    }

    public bool ShouldDisplayUpdateContent() => _firstUpdate;

    public async Task<string> GetUpdateContentAsync(bool download = false)
    {
        if (_firstUpdate || File.Exists(FilePath) == false || download)
            await DownloadUpdateContentAsync();
        await _localSettingsService.SaveSettingAsync(KeyValues.DisplayedUpdateVersion, RuntimeHelper.GetVersion());
        var result = string.Empty;
        if (File.Exists(FilePath))
            result = await File.ReadAllTextAsync(FilePath);
        return result;
    }

    private async Task DownloadUpdateContentAsync(string? targetLocal = null)
    {
        DownloadEvent?.Invoke();
        try
        {
            HttpClient client = Utils.GetDefaultHttpClient();
            var local = targetLocal ?? ResourceExtensions.GetLocal();
            HttpResponseMessage response = await client.GetAsync(
                $"https://raw.gitmirror.com/GoldenPotato137/GalgameManager/main/docs/UpdateContent/{local}.md");
            if (response.IsSuccessStatusCode == false && targetLocal is null)
            {
               await DownloadUpdateContentAsync("zh-CN"); //对应的语言不存在时，使用中文
               return;
            }

            var content = await response.Content.ReadAsStringAsync();
            await File.WriteAllTextAsync(FilePath, content);
        }
        catch (Exception e)
        {
            DownloadFailedEvent?.Invoke(e.Message);
        }

        DownloadCompletedEvent?.Invoke();
    }
}