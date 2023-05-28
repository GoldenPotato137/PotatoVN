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
        try
        {
            PackageUpdateAvailabilityResult result = await Package.Current.CheckUpdateAvailabilityAsync();
            return result.Availability is PackageUpdateAvailability.Available or PackageUpdateAvailability.Required;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool ShouldDisplayUpdateContent() => _firstUpdate;

    public async Task<string> GetUpdateContentAsync()
    {
        if (_firstUpdate || File.Exists(FilePath) == false)
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
            HttpClient client = new();
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