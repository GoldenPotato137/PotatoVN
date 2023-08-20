using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using IAppCenterService = GalgameManager.Contracts.Services.IAppCenterService;

namespace GalgameManager.Services;

public class AppCenterService : IAppCenterService
{
    private readonly ILocalSettingsService _localSettingsService;
    private const string Key = "27b36134-9654-427e-8484-b51e07bfb02b"; // 软件识别码
    private bool _isStarted;

    public AppCenterService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        _localSettingsService.OnSettingChanged += OnSettingChanged;
    }

    private async void OnSettingChanged(string key, object value)
    {
        if (key == KeyValues.UploadData && value is true)
            await StartAsync();
    }

    public async Task StartAsync()
    {
        if (_isStarted) return;
        if (await _localSettingsService.ReadSettingAsync<bool>(KeyValues.UploadData) == false) return;

        try
        {
            AppCenter.Start(Key, typeof(Analytics), typeof(Crashes));
            _isStarted = true;
        }
        catch
        {
            // ignored
        }
    }
}