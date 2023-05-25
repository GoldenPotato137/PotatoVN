using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;

namespace GalgameManager.Services;

public class UpdateService : IUpdateService
{
    private readonly bool _firstUpdate;
    private readonly ILocalSettingsService _localSettingsService;

    public UpdateService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        var last = localSettingsService.ReadSettingAsync<string>(KeyValues.DisplayedUpdateVersion).Result ?? "";
        _firstUpdate = last != RuntimeHelper.GetVersion();
    }

    public bool ShouldDisplayUpdateContent() => _firstUpdate;

    public async Task<string> GetUpdateContentAsync()
    {
        await Task.CompletedTask;
        //todo: 调试完成后取消注释
        // await _localSettingsService.SaveSettingAsync(KeyValues.DisplayedUpdateVersion, RuntimeHelper.GetVersion());
        return "#1.5.3\n这里应该填入更新内容\n#1.4\n这里应该填入更新内容";
    }
}