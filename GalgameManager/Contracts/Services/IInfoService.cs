using System.Collections.ObjectModel;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Contracts.Services;

public interface IInfoService
{
    public ObservableCollection<Info> Infos { get; }

    /// <summary>
    /// 记录并通知事件
    /// </summary>
    /// <param name="infoBarSeverity">严重程度</param>
    /// <param name="title">事件名</param>
    /// <param name="exception">与之相关的异常，若不是异常则不填</param>
    /// <param name="msg">事件信息</param>
    public void Event(InfoBarSeverity infoBarSeverity, string title, Exception? exception = null, string? msg = null);
}