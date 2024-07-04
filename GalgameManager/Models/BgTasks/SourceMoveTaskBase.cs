using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Models.BgTasks;

public abstract class SourceMoveInBase : BgTaskBase
{
    public string TargetPath { get; init; }

    /// 要移动的游戏，在RunIternalAsync时一定不是null
    protected Galgame? Game;
    /// 目标路径，在RunIternalAsync时一定不是null
    protected GalgameFolderSource? TargetSource;

    protected readonly GalgameCollectionService GameService = (App.GetService<IDataCollectionService<Galgame>>()
        as GalgameCollectionService)!;

    protected readonly IGalgameSourceCollectionService SourceCollectionService =
        App.GetService<IGalgameSourceCollectionService>();

    protected readonly IInfoService InfoService = App.GetService<IInfoService>();

    /// <summary>
    /// 错误事件的标题
    /// </summary>
    protected abstract string ErrorEventTitle { get; init; }

    /// <summary>
    /// 成功搬入源的事件的信息
    /// </summary>
    protected abstract string SuccessMsg();

    protected SourceMoveInBase(Galgame game, GalgameFolderSource targetSource, string targetPath)
    {
        TargetPath = targetPath;
        Game = game;
        TargetSource = targetSource;
    }

    // SourceMoveInTaskBase派生出来的后台任务不需要手动还原，会由SourceMoveTask统一还原，理论上来说这个函数是用不着的
    protected override Task RecoverFromJsonInternal()
    {
        return Task.CompletedTask;
    }

    protected async override Task RunInternal()
    {
        try
        {
            if (Game is null || TargetSource is null)
                throw new ArgumentException("_game is null or target source is null");
            await RunIternal2Async();
        }
        catch (Exception e)
        {
            InfoService.Event(EventType.BgTaskFailEvent, InfoBarSeverity.Error, ErrorEventTitle, e);
            ChangeProgress(-1, 1, ErrorEventTitle);
            return;
        }
        TargetSource.AddGalgame(Game, TargetPath);
        InfoService.Event(EventType.BgTaskFailEvent, InfoBarSeverity.Success, 
            "SourceMoveInBase_Success".GetLocalized(), msg: SuccessMsg());
        ChangeProgress(1, 1, "SourceMoveInBase_Success".GetLocalized());
    }

    /// <summary>
    /// 任务操作主体，所有异常均会被捕获并调用InfoService发送错误事件
    /// </summary>
    /// <returns></returns>
    protected abstract Task RunIternal2Async();
}