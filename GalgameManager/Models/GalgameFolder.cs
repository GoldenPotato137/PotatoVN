using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Windows.Storage;
using CommunityToolkit.WinUI;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Services;
using Microsoft.UI.Dispatching;
using Newtonsoft.Json;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace GalgameManager.Models;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public class GalgameFolder
{
    [JsonIgnore] public GalgameCollectionService GalgameService;

    [JsonIgnore] public bool IsRunning;
    [JsonIgnore] public bool IsUnpacking;
    [JsonIgnore] public int ProgressValue;
    [JsonIgnore] public int ProgressMax;
    [JsonIgnore] public string ProgressText = string.Empty;
    private readonly List<Galgame> _galgames = new();
    public event VoidDelegate? ProgressChangedEvent;

    public string Path
    {
        get;
        set;
    }

    public GalgameFolder(string path, IDataCollectionService<Galgame> service)
    {
        Path = path;
        GalgameService = ((GalgameCollectionService?)service)!;
    }

    public async Task<ObservableCollection<Galgame>> GetGalgameList()
    {
        await Task.CompletedTask;
        return new ObservableCollection<Galgame>(_galgames);
    }

    /// <summary>
    /// 向库中新增一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    public void AddGalgame(Galgame galgame)
    {
        _galgames.Add(galgame);
    }

    /// <summary>
    /// 从库中删除一个游戏
    /// </summary>
    /// <param name="galgame">游戏</param>
    public void DeleteGalgame(Galgame galgame)
    {
        _galgames.Remove(galgame);
    }

    /// <summary>
    /// 检查该游戏是否应该在这个库中
    /// </summary>
    /// <param name="galgame">游戏</param>
    /// <returns></returns>
    public bool IsInFolder(Galgame galgame)
    {
        return IsInFolder(galgame.Path);
    }

    /// <summary>
    /// 检查这个路径的游戏是否应该这个库中
    /// </summary>
    /// <param name="path">路径</param>
    /// <returns></returns>
    public bool IsInFolder(string path)
    {
        return path[..path.LastIndexOf('\\')] == Path ;
    }

    /// <summary>
    /// 扫描文件夹下的所有游戏并添加到库
    /// <param name="localSettingsService">设置服务</param>
    /// </summary>
    public async Task GetGalgameInFolder(ILocalSettingsService? localSettingsService=null)
    {
        if (!Directory.Exists(Path) || IsRunning) return;
        var maxDepth = 1;
        var ignoreFetchResult = false;
        List<string> fileMustContain = new();
        List<string> fileShouldContain = new();
        if (localSettingsService != null)
        {
            var searchSubFolder = await localSettingsService.ReadSettingAsync<bool>(KeyValues.SearchChildFolder);
            maxDepth = searchSubFolder ? await localSettingsService.ReadSettingAsync<int>(KeyValues.SearchChildFolderDepth) : 1;
            var tmp = await localSettingsService.ReadSettingAsync<string>(KeyValues.GameFolderMustContain);
            if (!string.IsNullOrEmpty(tmp))
                fileMustContain = tmp.Split('\r', '\n').ToList();
            tmp = await localSettingsService.ReadSettingAsync<string>(KeyValues.GameFolderShouldContain);
            if (!string.IsNullOrEmpty(tmp))
                fileShouldContain = tmp.Split('\r', '\n').ToList();
            ignoreFetchResult = await localSettingsService.ReadSettingAsync<bool>(KeyValues.IgnoreFetchResult);
        }
        IsRunning = true;
        var cnt = 0;
        Queue<(string Path, int Depth)> pathToCheck = new();
        pathToCheck.Enqueue((Path, 0));
        while (pathToCheck.Count>0)
        {
            var (currentPath, currentDepth) = pathToCheck.Dequeue();
            ProgressText = $"正在扫描路径:{currentPath}";
            ProgressChangedEvent?.Invoke();
            if (IsGameFolder(currentPath, fileMustContain, fileShouldContain))
            {
                GalgameCollectionService.AddGalgameResult result = await GalgameService.TryAddGalgameAsync(currentPath, ignoreFetchResult);
                if (result == GalgameCollectionService.AddGalgameResult.Success || 
                    (ignoreFetchResult && result == GalgameCollectionService.AddGalgameResult.NotFoundInRss)) 
                    cnt++;
            }
            if (currentDepth == maxDepth) continue;
            foreach (var subPath in Directory.GetDirectories(currentPath))
                pathToCheck.Enqueue((subPath, currentDepth + 1));
        }
        ProgressText = $"扫描完成, 共添加了{cnt}个游戏";
        ProgressChangedEvent?.Invoke();
        await Task.Delay(3000);
        IsRunning = false;
        ProgressChangedEvent?.Invoke();
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

    /// <summary>
    /// 从信息源更新库的所有游戏的信息
    /// <param name="toUpdate">要更新的游戏列表，null则为全部更新</param>
    /// </summary>
    public async Task GetInfoFromRss(List<Galgame>? toUpdate = null)
    {
        List<Galgame> galgames = toUpdate ?? (await GetGalgameList()).ToList();
        IsRunning = true;
        for (var i = 0;i<galgames.Count;i++)
        {
            Galgame galgame = galgames[i];
            ProgressText = $"正在获取 {galgame.Name.Value} 的信息, {i}/{galgames.Count}";
            ProgressChangedEvent?.Invoke();
            await GalgameService.PhraseGalInfoAsync(galgame);
        }

        IsRunning = false;
        ProgressChangedEvent?.Invoke();
    }

    /// <summary>
    /// 试图解压压缩包并添加到库
    /// </summary>
    /// <param name="pack">压缩包</param>
    /// <param name="passwd">解压密码</param>
    /// <returns>解压后游戏目录（无法解压则为null)</returns>
    public async Task<string?> UnpackGame(StorageFile pack, string? passwd)
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        var deleteDirectory = string.Empty;
        try
        {
            await using var archiveStream = await pack.OpenStreamForReadAsync();
            using var archive = ArchiveFactory.Open(archiveStream, new ReaderOptions { Password = passwd });
            // 解压文件到指定目录
            var outputDirectory = Path;
            // 检查压缩包中的内容，以确定是否需要创建一个新的文件夹
            var shouldCreateNewFolder = archive.Entries.All(entry => !entry.IsDirectory);
            if (shouldCreateNewFolder)
            {
                outputDirectory = Path + "\\" + pack.Name[..pack.Name.LastIndexOf('.')];
                Directory.CreateDirectory(outputDirectory);
            }

            deleteDirectory = outputDirectory;
            string? result = null;
            await Task.Run(async () =>
            {
                await dispatcherQueue.EnqueueAsync(() =>
                {
                    IsUnpacking = true;
                    ProgressMax = archive.Entries.Count();
                    ProgressValue = 1;
                    ProgressChangedEvent?.Invoke();
                });

                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory)
                    {
                        if (!shouldCreateNewFolder && deleteDirectory == outputDirectory)
                            deleteDirectory += "\\" + entry.Key;

                        continue;
                    }

                    // 更新解压进度
                    await dispatcherQueue.EnqueueAsync(() =>
                    {
                        ProgressText = $"正在解压到 {Path + entry.Key}";
                        ProgressValue = int.Min(ProgressValue + 1, ProgressMax);
                        ProgressChangedEvent?.Invoke();
                    });

                    entry.WriteToDirectory(outputDirectory, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }

                result = deleteDirectory;
                await dispatcherQueue.EnqueueAsync(() =>
                {
                    IsUnpacking = false;
                    ProgressChangedEvent?.Invoke();
                });
            });

            if (result != null && (result[^1] == '\\' || result[^1] == '/')) // 删除最后的反斜杠
                result = result[..^1];
            return result;
        }
        catch (Exception) //密码错误或压缩包损坏
        {
            await dispatcherQueue.EnqueueAsync(() =>
            {
                IsUnpacking = false;
                ProgressChangedEvent?.Invoke();
                if (deleteDirectory != string.Empty && deleteDirectory!=Path)
                    DeleteDirectory(deleteDirectory); // 删除解压失败的文件夹
            });
            return null;
        }
    }

    private static void DeleteDirectory(string directoryPath)
    {
        var directoryInfo = new DirectoryInfo(directoryPath);
        foreach (var file in directoryInfo.GetFiles())
        {
            file.Delete();
        }

        foreach (var subDirectory in directoryInfo.GetDirectories())
        {
            DeleteDirectory(subDirectory.FullName);
        }

        directoryInfo.Delete();
    }
}
