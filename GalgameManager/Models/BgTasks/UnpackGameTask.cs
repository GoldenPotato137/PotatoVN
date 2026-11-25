using System.Reflection;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using Newtonsoft.Json;
using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;


namespace GalgameManager.Models.BgTasks;

public class UnpackGameTask : BgTaskBase, IMoveInTask
{
    public string PackPath = string.Empty;
    public string? Password = string.Empty;
    public string GameName = string.Empty;
    public string TargetPath = null!;
    /// 解压完成后是否把游戏添加到库中；移动游戏时为false，由SourceMoveTask把解压结果登记到原游戏上
    public bool AddToLibrary = true;
    /// 临时解压文件夹，序列化用，防止启动任务后最小化到后台后重新生成任务时再次生成新的临时文件夹
    public string TmpDirName = Path.GetRandomFileName();

    [JsonIgnore] public string ResultPath => Path.Combine(TargetPath, GameName);

    private static bool _7ZInit;

    public UnpackGameTask() { }

    /// <param name="pack">压缩包</param>
    /// <param name="targetPath">解压位置，最终会解压到：targetPath/gameName</param>
    /// <param name="gameName"></param>
    /// <param name="password">压缩包密码，若没有可不填</param>
    public UnpackGameTask(StorageFile pack, string targetPath, string gameName, string? password = null)
        : this(pack.Path, targetPath, gameName, password)
    {
    }

    /// <param name="packPath">压缩包（分卷压缩包为第一卷）路径</param>
    /// <param name="targetPath">解压位置，最终会解压到：targetPath/gameName</param>
    /// <param name="gameName"></param>
    /// <param name="password">压缩包密码，若没有可不填</param>
    public UnpackGameTask(string packPath, string targetPath, string gameName, string? password = null)
    {
        PackPath = packPath;
        TargetPath = targetPath;
        GameName = gameName;
        Password = password;
    }

    protected override Task RecoverFromJsonInternal()
    {
        try
        {
            if (Directory.Exists(Path.Combine(TargetPath, TmpDirName)))
                Directory.Delete(Path.Combine(TargetPath, TmpDirName), true);
        }
        catch
        {
            //ignore
        }
        return Task.CompletedTask;
    }

    /// 注意：必须为async方法，前置检查的异常才会被捕获进返回的Task（BgTaskBase.Task），
    /// 否则同步抛出的异常会导致Task属性停留在默认的CompletedTask，调用方无法感知失败
    protected override async Task RunInternal()
    {
        if (!File.Exists(PackPath) || GameName == string.Empty)
        {
            if (AddToLibrary) return;
            throw new PvnException("GalgameFolder_UnpackGame_PackNotExist".GetLocalized(PackPath));
        }
        if (!Directory.Exists(TargetPath))
            throw new PvnException("GalgameFolder_UnpackGame_PathNotExist".GetLocalized(TargetPath));
        var saveDirectory = Path.Combine(TargetPath, GameName); // 游戏保存路径
        if (Directory.Exists(saveDirectory))
            throw new PvnException("GalgameFolder_UnpackGame_PathNotEmpty".GetLocalized(saveDirectory));

        var tmpDir = Path.Combine(TargetPath, TmpDirName);
        Directory.CreateDirectory(tmpDir); // 临时解压路径，若没有权限则在此步抛异常报错

        await Task.Run((async Task() =>
        {
            try
            {
                ChangeProgress(0, 1, "GalgameFolder_UnpackGame_Start".GetLocalized());
                // SharpCompress解压固实压缩的包（7z）时有严重性能问题，使用另一种方案解压
                var extension = Path.GetExtension(PackPath).ToLowerInvariant();
                if (extension is ".7z" or ".001")
                    await Unzip7Z(tmpDir);
                else
                    await UnZip(tmpDir);

                // 压缩包内只有一个文件夹（没有零散文件）时视为完整的游戏文件夹，否则整体作为游戏文件夹
                if (Directory.GetDirectories(tmpDir).Length == 1 && Directory.GetFiles(tmpDir).Length == 0)
                    Directory.Move(Directory.GetDirectories(tmpDir)[0], saveDirectory);
                else
                    Directory.Move(tmpDir, saveDirectory);

                ChangeProgress(0, 1, "GalgameFolder_UnpackGame_Done".GetLocalized());
                if (AddToLibrary)
                {
                    await UiThreadInvokeHelper.InvokeAsync(async Task () =>
                    {
                        // 新游戏会走正常的元数据获取流程；从压缩库移动时AddToLibrary为false，由SourceMoveTask登记到原游戏
                        await App.GetService<IGalgameCollectionService>()
                            .AddGameAsync(GalgameSourceType.LocalFolder, saveDirectory, true);
                    });
                    ChangeProgress(1, 1, string.Empty);
                }
                else
                    ChangeProgress(1, 1, "UnpackGameTask_UnpackedTo".GetLocalized(saveDirectory));

                if (StartFromBg && await App.GetService<ILocalSettingsService>()
                                    .ReadSettingAsync<bool>(KeyValues.NotifyWhenUnpackGame)
                                && App.MainWindow is null)
                {
                    App.SystemTray?.ShowNotification("UnpackGameTask_Done_Title".GetLocalized(),
                        "UnpackGameTask_Done_Msg".GetLocalized(GameName, saveDirectory));
                }
            }
            catch (Exception e) //密码错误或压缩包损坏
            {
                ChangeProgress(-1, 1, string.Empty);
                // 移动游戏时调用方需要感知失败，以免把不存在的路径登记到游戏上
                if (!AddToLibrary)
                    throw new PvnException("UnpackGameTask_Failed".GetLocalized(e.Message));
            }
            finally
            {
                if (Directory.Exists(tmpDir)) // 删除解压失败的文件夹
                    Directory.Delete(tmpDir, true);
            }
        })!);
    }
    private async Task Unzip7Z(string tmpDir)
    {
        Init();
        using SevenZipExtractor extractor = new(PackPath, Password);
        if (!extractor.Check()) throw new PvnException("password incorrect or pack broken");
        var totalFileCnt = extractor.ArchiveFileData.Count;
        var finishedCnt = 0;
        var finished = false;
            
            
        extractor.FileExtractionStarted += (_, args) => 
            ChangeProgress(finishedCnt, totalFileCnt, $"{args.FileInfo.FileName}");
        extractor.FileExtractionFinished += (_, _) => Interlocked.Increment(ref finishedCnt);
        extractor.ExtractionFinished += (_, _) => finished = true;
        extractor.EventSynchronization = EventSynchronizationStrategy.AlwaysSynchronous;
                    
        extractor.BeginExtractArchive(tmpDir);
                    
        while (finished == false)
            await Task.Delay(1000);
        return;

        static void Init()
        {
            if (_7ZInit) return;
            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, 
                "Assets", "Libs", Environment.Is64BitProcess ? "x64" : "x86", "7za.dll");
            SevenZipBase.SetLibraryPath(path);
            _7ZInit = true;
        }
    }

    private async Task UnZip(string tmpDir)
    {
        await Task.CompletedTask;
        var finishedCnt = 0;
        // 必须显式关闭底层文件流（ReaderOptions默认LeaveStreamOpen），否则压缩包会一直被占用，无法在移动后删除
        using IArchive archive = ArchiveFactory.OpenArchive(PackPath,
            new ReaderOptions { Password = Password, LeaveStreamOpen = false });
        List<IArchiveEntry> entries = archive.Entries.Where(entry => !entry.IsDirectory).ToList();
        var totalFileCnt = entries.Count;
        ExtractionOptions options = new()
        {
            ExtractFullPath = true,
            Overwrite = true,
        };

        // 解压文件并更新进度
        foreach (IArchiveEntry entry in entries)
        {
            ChangeProgress(finishedCnt, totalFileCnt, $"{entry.Key}");
            entry.WriteToDirectory(tmpDir, options);
            Interlocked.Increment(ref finishedCnt);
        }
    }

    public override bool OnSearch(string key) => Utils.ArePathsEqual(key, TargetPath);

    public override string Title { get; } = "UnpackGameTask_Title".GetLocalized();
}