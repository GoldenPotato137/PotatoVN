using System.Collections.ObjectModel;
using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

/// <summary>
/// 消息及异常记录与通知服务<br/>
/// </summary>
public class InfoService : IInfoService
{
    public ObservableCollection<Info> Infos { get; } = new();
    private readonly IAppCenterService _appCenterService;
    
    public InfoService(IAppCenterService appCenterService)
    {
        _appCenterService = appCenterService;
    }

    public void Event(InfoBarSeverity infoBarSeverity, string title, Exception? exception = null, string? msg = null)
    {
        Infos.Insert(0, new Info(infoBarSeverity, title, exception?.ToString() ?? msg ?? string.Empty));
        _appCenterService.UploadEvent(title, exception, msg);
    }
}