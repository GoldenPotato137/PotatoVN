using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using H.NotifyIcon.Core;

namespace GalgameManager.Models.BgTasks;

public class GetGalgameInFolderTask : BgTaskBase
{
    public string GalgameFolderPath = string.Empty;
    private GalgameFolder? _galgameFolder;

    public GetGalgameInFolderTask() { }

    public GetGalgameInFolderTask(GalgameFolder folder)
    {
        _galgameFolder = folder;
        GalgameFolderPath = folder.Path;
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _galgameFolder = (App.GetService<IDataCollectionService<GalgameFolder>>() as GalgameFolderCollectionService)?.
            GetGalgameFolderFromPath(GalgameFolderPath);
        return Task.CompletedTask;
    }

    public override Task Run()
    {
        if (_galgameFolder is null || Directory.Exists(GalgameFolderPath) == false || _galgameFolder.IsRunning)
            return Task.CompletedTask;
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        GalgameCollectionService galgameService = (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!;
        var log = string.Empty;
        
        return Task.Run((async Task () =>
        {
            log += $"{DateTime.Now}\n{GalgameFolderPath}\n\n";
            List<string> fileMustContain = new();
            List<string> fileShouldContain = new();
            var searchSubFolder = await localSettings.ReadSettingAsync<bool>(KeyValues.SearchChildFolder);
            var maxDepth = searchSubFolder ? await localSettings.ReadSettingAsync<int>(KeyValues.SearchChildFolderDepth) : 1;
            var tmp = await localSettings.ReadSettingAsync<string>(KeyValues.GameFolderMustContain);
            if (!string.IsNullOrEmpty(tmp))
                fileMustContain = tmp.Split('\r', '\n').ToList();
            tmp = await localSettings.ReadSettingAsync<string>(KeyValues.GameFolderShouldContain);
            if (!string.IsNullOrEmpty(tmp))
                fileShouldContain = tmp.Split('\r', '\n').ToList();
            var ignoreFetchResult = await localSettings.ReadSettingAsync<bool>(KeyValues.IgnoreFetchResult);

            log += "Params:\n" + $"searchSubFolder:{searchSubFolder}\n" + $"maxDepth:{maxDepth}\n" +
                   $"fileMustContain:{string.Join(",", fileMustContain)}\n" +
                   $"fileShouldContain:{string.Join(",", fileShouldContain)}\n" +
                   $"ignoreFetchResult:{ignoreFetchResult}\n\n\n";

            _galgameFolder.IsRunning = true;
            var cnt = 0;
            Queue<(string Path, int Depth)> pathToCheck = new();
            pathToCheck.Enqueue((GalgameFolderPath, 0));
            while (pathToCheck.Count > 0)
            {
                var (currentPath, currentDepth) = pathToCheck.Dequeue();
                log += $"\n{currentPath}: ";
                if (!HasPermission(currentPath))
                {
                    log += "No Permission";
                    continue;
                }
                ChangeProgress(0, 1, "GalgameFolder_GetGalInFolder_Progress".GetLocalized(currentPath));
                if (IsGameFolder(currentPath, fileMustContain, fileShouldContain))
                {
                    AddGalgameResult result = AddGalgameResult.Other;
                    await UiThreadInvokeHelper.InvokeAsync(async Task() =>
                    {
                        result = await galgameService.TryAddGalgameAsync(currentPath, ignoreFetchResult);
                        if (result == AddGalgameResult.Success ||
                            (ignoreFetchResult && result == AddGalgameResult.NotFoundInRss))
                            cnt++;
                    });
                    log += result;
                }
                else
                    log += "Not a game folder";

                if (currentDepth == maxDepth) continue;
                foreach (var subPath in Directory.GetDirectories(currentPath))
                    pathToCheck.Enqueue((subPath, currentDepth + 1));
            }
            ChangeProgress(0, 1, "GalgameFolder_GetGalInFolder_Saving".GetLocalized(cnt));
            FileHelper.SaveWithoutJson(_galgameFolder.GetLogName(), log, "Logs");
            await Task.Delay(1000); //等待文件保存
            
            ChangeProgress(1, 1, "GalgameFolder_GetGalInFolder_Done".GetLocalized(cnt));
            _galgameFolder.IsRunning = false;
            if (App.MainWindow is null && await localSettings.ReadSettingAsync<bool>(KeyValues.NotifyWhenGetGalgameInFolder))
            {
                App.SystemTray?.ShowNotification(nameof(NotificationIcon.Info), 
                    "GalgameFolder_GetGalInFolder_Done".GetLocalized(cnt));
            }
        })!);
    }

    public override bool OnSearch(string key) => GalgameFolderPath.Contains(key);
    
    public override string Title { get; } = "GetGalgameInFolderTask_Title".GetLocalized();

    /// <summary>
    /// 检查是否具有读取文件夹的权限
    /// </summary>
    private static bool HasPermission(string path)
    {
        try
        {
            Directory.GetFiles(path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    /// <summary>
    /// 判断文件夹是否是游戏文件夹
    /// </summary>
    /// <param name="path">文件夹路径</param>
    /// <param name="fileMustContain">必须包含的文件后缀</param>
    /// <param name="fileShouldContain">至少包含一个的文件后缀</param>
    /// <returns></returns>
    private static bool IsGameFolder(string path, List<string> fileMustContain, List<string> fileShouldContain)
    {
        foreach(var file in fileMustContain)
            if (!Directory.GetFiles(path).Any(f => f.ToLower().EndsWith(file)))
                return false;
        var shouldContain = false;
        foreach(var file in fileShouldContain)
            if (Directory.GetFiles(path).Any(f => f.ToLower().EndsWith(file)))
            {
                shouldContain = true;
                break;
            }
        return shouldContain;
    }
}