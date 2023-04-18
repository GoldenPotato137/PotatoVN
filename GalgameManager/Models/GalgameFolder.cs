using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

using Windows.Storage;

using CommunityToolkit.WinUI;

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
        var games = await GalgameService.GetContentGridDataAsync();
        return new ObservableCollection<Galgame>(games.Where(g => g.Path.StartsWith(Path)).ToList());
    }

    /// <summary>
    /// 扫描文件夹下的所有游戏并添加到库
    /// </summary>
    public async Task GetGalgameInFolder()
    {
        if (!Directory.Exists(Path) || IsRunning) return;
        IsRunning = true;
        ProgressMax = Directory.GetDirectories(Path).Length;
        ProgressValue = 0;
        var cnt = 0;
        foreach (var subPath in Directory.GetDirectories(Path))
        {
            ProgressValue++;
            ProgressText = $"正在扫描路径:{subPath} , {ProgressValue}/{ProgressMax}";
            ProgressChangedEvent?.Invoke();
            var result = await GalgameService.TryAddGalgameAsync(subPath);
            if (result == GalgameCollectionService.AddGalgameResult.Success) cnt++;
        }

        ProgressText = $"扫描完成, 共添加了{cnt}个游戏";
        ProgressChangedEvent?.Invoke();
        await Task.Delay(3000);
        IsRunning = false;
        ProgressChangedEvent?.Invoke();
    }

    /// <summary>
    /// 从信息源更新库的所有游戏的信息
    /// </summary>
    public async Task GetInfoFromRss()
    {
        var galgames = await GetGalgameList();
        IsRunning = true;
        for (var i = 0;i<galgames.Count;i++)
        {
            var galgame = galgames[i];
            ProgressText = $"正在获取 {galgame.Name} 的信息, {i}/{galgames.Count}";
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
                if (deleteDirectory != string.Empty)
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
