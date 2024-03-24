using GalgameManager.Contracts.Models;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using H.NotifyIcon.Core;

namespace GalgameManager.Models.BgTasks;

public class GetGalgameInLocalZipTask : BgTaskBase
{
    private static string[] ZipEnds = { "zip", "rar", "7z" };
    public string GalgameSourceUrl = string.Empty;
    private GalgameZipSource? _galgameFolderSource;

    public GetGalgameInLocalZipTask() { }

    public GetGalgameInLocalZipTask(GalgameZipSource source)
    {
        _galgameFolderSource = source;
        GalgameSourceUrl = source.Url;
    }
    
    protected override Task RecoverFromJsonInternal()
    {
        _galgameFolderSource = (App.GetService<IDataCollectionService<GalgameSourceBase>>() as GalgameSourceCollectionService)?.
            GetGalgameSourceFromUrl(GalgameSourceUrl) as GalgameZipSource;
        return Task.CompletedTask;
    }

    public override Task Run()
    {
        //TODO
        if (_galgameFolderSource is null || Directory.Exists(GalgameSourceUrl) == false || _galgameFolderSource.IsRunning)
            return Task.CompletedTask;
        ILocalSettingsService localSettings = App.GetService<ILocalSettingsService>();
        GalgameCollectionService galgameService = (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!;
        var log = string.Empty;
        
        return Task.Run((async Task () =>
        {
            log += $"{DateTime.Now}\n{GalgameSourceUrl}\n\n";
            var ignoreFetchResult = await localSettings.ReadSettingAsync<bool>(KeyValues.IgnoreFetchResult);

            _galgameFolderSource.IsRunning = true;
            var cnt = 0;
            Queue<(string Path, int Depth)> pathToCheck = new();
            pathToCheck.Enqueue((_galgameFolderSource.Path, 0));
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
                foreach (var path in GetGameZip(currentPath))
                {
                    AddGalgameResult result = AddGalgameResult.Other;
                    await UiThreadInvokeHelper.InvokeAsync(async Task() =>
                    {
                        result = await galgameService.TryAddGalgameAsync(SourceType.LocalZip, path, ignoreFetchResult);
                        if (result == AddGalgameResult.Success ||
                            (ignoreFetchResult && result == AddGalgameResult.NotFoundInRss))
                            cnt++;
                    });
                    log += result;
                }

                foreach (var subPath in Directory.GetDirectories(currentPath))
                    pathToCheck.Enqueue((subPath, currentDepth + 1));
            }
            ChangeProgress(0, 1, "GalgameFolder_GetGalInFolder_Saving".GetLocalized(cnt));
            FileHelper.SaveWithoutJson(_galgameFolderSource.GetLogName(), log, "Logs");
            await Task.Delay(1000); //等待文件保存
            
            ChangeProgress(1, 1, "GalgameFolder_GetGalInFolder_Done".GetLocalized(cnt));
            _galgameFolderSource.IsRunning = false;
            if (App.MainWindow is null && await localSettings.ReadSettingAsync<bool>(KeyValues.NotifyWhenGetGalgameInFolder))
            {
                App.SystemTray?.ShowNotification(nameof(NotificationIcon.Info), 
                    "GalgameFolder_GetGalInFolder_Done".GetLocalized(cnt));
            }
        })!);
    }

    public override bool OnSearch(string key) => GalgameSourceUrl.Contains(key);
    
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
    
    private static IEnumerable<string> GetGameZip(string path)
    {
        foreach (var p in Directory.GetFiles(path).Where(f => ZipEnds.Contains(f.ToLower().Split(".")[^1])))
        {
            var parts = p.Split(".");
            if (parts.Length == 3 && parts[1] != "001")continue;
            yield return p;
        }
    }
}