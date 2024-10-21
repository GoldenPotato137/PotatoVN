using System.Reflection;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models.Sources;
using SevenZip;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;


namespace GalgameManager.Models.BgTasks;

public class UnpackGameTask : BgTaskBase
{
    public string PackPath = string.Empty;
    public string? Password = string.Empty;
    public string GameName = string.Empty;
    public string TargetPath = null!;
    /// 临时解压文件夹，序列化用，防止启动任务后最小化到后台后重新生成任务时再次生成新的临时文件夹
    public string TmpDirName = Path.GetRandomFileName(); 
    
    private StorageFile? _pack;
    private static bool _7ZInit;

    public UnpackGameTask() { }
    
    /// <param name="pack">压缩包</param>
    /// <param name="targetPath">解压位置，最终会解压到：targetPath/gameName</param>
    /// <param name="gameName"></param>
    /// <param name="password">压缩包密码，若没有可不填</param>
    public UnpackGameTask(StorageFile pack, string targetPath,string gameName, string? password = null)
    {
        _pack = pack;
        TargetPath = targetPath;
        PackPath = pack.Path;
        GameName = gameName;
        Password = password;
    }
    
    protected async override Task RecoverFromJsonInternal()
    {
        try
        {
            _pack = await StorageFile.GetFileFromPathAsync(PackPath);
            if (Directory.Exists(Path.Combine(TargetPath, TmpDirName)))
                Directory.Delete(Path.Combine(TargetPath, TmpDirName), true);
        }
        catch
        {
            //ignore
        }
    }

    protected override Task RunInternal()
    {
        if(_pack is null || GameName == string.Empty) return Task.CompletedTask;
        if (!Directory.Exists(TargetPath))
            throw new PvnException("GalgameFolder_UnpackGame_PathNotExist".GetLocalized(TargetPath));
        var saveDirectory = Path.Combine(TargetPath, GameName); // 游戏保存路径
        if (Directory.Exists(saveDirectory))
            throw new PvnException("GalgameFolder_UnpackGame_PathNotEmpty".GetLocalized(saveDirectory));
        
        var tmpDir = Path.Combine(TargetPath, TmpDirName);
        Directory.CreateDirectory(tmpDir); // 临时解压路径，若没有权限则在此步抛异常报错

        return Task.Run((async Task() =>
        {
            try
            {
                ChangeProgress(0, 1, "GalgameFolder_UnpackGame_Start".GetLocalized());
                if (_pack.FileType == ".7z" || _pack.FileType == ".001")
                    await Unzip7Z(tmpDir);
                else
                    await UnZip(tmpDir);

                // SharpCompress解压固实压缩的包（7z）时有严重性能问题，使用另一种方案解压
                if (Directory.GetDirectories(tmpDir).Length == 1) // 压缩包内为完整的文件夹
                    Directory.Move(Directory.GetDirectories(tmpDir)[0], saveDirectory);
                else // 压缩包内为零散文件
                    Directory.Move(tmpDir, saveDirectory);

                ChangeProgress(0, 1, "GalgameFolder_UnpackGame_Done".GetLocalized());
                await UiThreadInvokeHelper.InvokeAsync(async Task () =>
                {
                    await App.GetService<IGalgameCollectionService>()
                        .AddGameAsync(GalgameSourceType.LocalFolder, saveDirectory, true);
                });
                ChangeProgress(1, 1, string.Empty);

                if (StartFromBg && await App.GetService<ILocalSettingsService>()
                                    .ReadSettingAsync<bool>(KeyValues.NotifyWhenUnpackGame)
                                && App.MainWindow is null)
                {
                    App.SystemTray?.ShowNotification("UnpackGameTask_Done_Title".GetLocalized(),
                        "UnpackGameTask_Done_Msg".GetLocalized(GameName, saveDirectory));
                }
            }
            catch (Exception) //密码错误或压缩包损坏
            {
                ChangeProgress(-1, 1, string.Empty);
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
        using SevenZipExtractor extractor = new(_pack!.Path, Password);
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
        using IArchive archive = ArchiveFactory.Open(_pack!.Path, new ReaderOptions { Password = Password });
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