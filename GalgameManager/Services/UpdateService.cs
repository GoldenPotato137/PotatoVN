using Windows.ApplicationModel;
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

    public event VoidDelegate? DownloadEvent;
    public event VoidDelegate? DownloadCompletedEvent;
    public event GenericDelegate<string>? DownloadFailedEvent;
    public event GenericDelegate<bool>? SettingBadgeEvent;
    private string FilePath => Path.Combine(_localFolder, FileName);

    public UpdateService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        var last = localSettingsService.ReadSettingAsync<string>(KeyValues.DisplayedUpdateVersion).Result ?? "";
        _firstUpdate = last != RuntimeHelper.GetVersion();
    }

    public async Task<bool> CheckUpdateAsync()
    {
        if (RuntimeHelper.IsMSIX == false) return false;
        // if (await _localSettingsService.ReadSettingAsync<DateTime>(KeyValues.LastUpdateCheckDate) 
        //         is var lastDate && lastDate.Date == DateTime.Now.Date)
        //     return await _localSettingsService.ReadSettingAsync<bool>(KeyValues.LastUpdateCheckResult);
        try
        {
            PackageUpdateAvailabilityResult tmp = await Package.Current.CheckUpdateAvailabilityAsync();
            var result = tmp.Availability is PackageUpdateAvailability.Available or PackageUpdateAvailability.Required;
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

    private async Task DownloadUpdateContentAsync()
    {
        DownloadEvent?.Invoke();
        try
        {
            HttpClient client = Utils.GetDefaultHttpClient();
            var local = ResourceExtensions.GetLocal();
            HttpResponseMessage response = await client.GetAsync(
                $"https://raw.gitmirror.com/GoldenPotato137/GalgameManager/main/docs/UpdateContent/{local}.md");
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