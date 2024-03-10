using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

/// <summary>
/// 消息及异常记录与通知服务<br/>
/// </summary>
public class InfoService : IInfoService
{
    public event Action<InfoBarSeverity, string?, string?, int>? OnInfo;
    public event Action<InfoBarSeverity, string?, string?>? OnEvent;
    public ObservableCollection<Info> Infos { get; } = new();
    private readonly IAppCenterService _appCenterService;
    private readonly ILocalSettingsService _localSettingsService;
    
    public InfoService(IAppCenterService appCenterService, ILocalSettingsService localSettingsService)
    {
        _appCenterService = appCenterService;
        _localSettingsService = localSettingsService;
    }

    public void Info(InfoBarSeverity infoBarSeverity, string? title = null, string? msg = null, int? displayTimeMs = 3000)
    {
        UiThreadInvokeHelper.Invoke(() => { OnInfo?.Invoke(infoBarSeverity, title, msg, displayTimeMs ?? 3000);});
    }

    public void Event(EventType type, InfoBarSeverity infoBarSeverity, string title, Exception? exception = null, string? msg = null)
    {
        UiThreadInvokeHelper.Invoke(async () =>
        {
            if (await ShouldNotifyEvent(type))
                OnEvent?.Invoke(infoBarSeverity, title, exception?.ToString() ?? msg);
            // 下面这句话有时会抛出System.Runtime.InteropServices.COMException (0x80004005)，但容器却能正常插入
            Infos.Insert(0, new Info(infoBarSeverity, title, exception?.ToString() ?? msg ?? string.Empty));
        });
        _appCenterService.UploadEvent(title, exception, msg);
    }

    private async Task<bool> ShouldNotifyEvent(EventType type)
    {
        switch (type)
        {
            case EventType.PvnSyncEvent:
                return await _localSettingsService.ReadSettingAsync<bool>(KeyValues.EventPvnSyncNotify);
            case EventType.PvnSyncEmptyEvent:
                return await _localSettingsService.ReadSettingAsync<bool>(KeyValues.EventPvnSyncEmptyNotify);
            default:
                return true;
        }
    }
}