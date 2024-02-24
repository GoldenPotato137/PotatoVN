using System.Reflection;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using SevenZip;

namespace GalgameManager.Models.BgTasks;

public class UnpackGameTask : BgTaskBase
{
    public string GalgameFolderPath = string.Empty;
    public string PackPath = string.Empty;
    public string? Password = string.Empty;
    public string GameName = string.Empty;
    
    private GalgameFolder? _galgameFolder;
    private StorageFile? _pack;
    private static bool _init;

    public UnpackGameTask() { }

    public UnpackGameTask(StorageFile pack, GalgameFolder galgameFolder,string gameName, string? password = null)
    {
        _pack = pack;
        _galgameFolder = galgameFolder;
        GalgameFolderPath = galgameFolder.Path;
        PackPath = pack.Path;
        GameName = gameName;
        Password = password;
    }
    
    protected async override Task RecoverFromJsonInternal()
    {
        _galgameFolder = (App.GetService<IDataCollectionService<GalgameFolder>>() as GalgameFolderCollectionService)?.
            GetGalgameFolderFromPath(GalgameFolderPath);
        try
        {
            _pack = await StorageFile.GetFileFromPathAsync(PackPath);
        }
        catch
        {
            //ignore
        }
    }

    public override Task Run()
    {
        if(_galgameFolder is null || _pack is null || GameName == string.Empty) return Task.CompletedTask;

        Init();
        var outputDirectory = _galgameFolder.Path; // 解压路径
        var saveDirectory = Path.Combine(outputDirectory, GameName); // 游戏保存路径
        return Task.Run((async Task() =>
        {
            try
            {
                _galgameFolder.IsUnpacking = true;
                ChangeProgress(0, 1, "GalgameFolder_UnpackGame_Start".GetLocalized());

                var folderName = string.Empty;
                using (SevenZipExtractor extractor = new(_pack.Path, Password))
                {
                    var totalFileCnt = extractor.ArchiveFileData.Count;
                    var finishedCnt = 0;
                    var finished = false;

                    foreach(var name in extractor.ArchiveFileNames)
                    {
                        if (extractor.ArchiveFileNames.All(name2 => name2.StartsWith(name))) //压缩包内是文件夹
                        {
                            folderName = name;
                            break;
                        }
                    }
                    if(folderName == string.Empty) //压缩包内直接是零散文件
                        Directory.CreateDirectory(outputDirectory = saveDirectory);
                    
                    extractor.FileExtractionStarted += (_, args) => 
                        ChangeProgress(finishedCnt, totalFileCnt, $"{args.FileInfo.FileName}");
                    extractor.FileExtractionFinished += (_, _) => Interlocked.Increment(ref finishedCnt);
                    extractor.ExtractionFinished += (_, _) => finished = true;
                    extractor.EventSynchronization = EventSynchronizationStrategy.AlwaysSynchronous;
                    
                    extractor.BeginExtractArchive(outputDirectory);
                    
                    while (finished == false)
                        await Task.Delay(1000);
                }
                if(folderName != string.Empty && Path.Combine(outputDirectory, folderName) != saveDirectory) //压缩包内是文件夹，改名为游戏名
                    Directory.Move(Path.Combine(outputDirectory, folderName), saveDirectory);
                
                ChangeProgress(1, 1, "GalgameFolder_UnpackGame_Done".GetLocalized());
                await UiThreadInvokeHelper.InvokeAsync(async Task () =>
                {
                    await (App.GetService<IDataCollectionService<Galgame>>() as GalgameCollectionService)!
                        .TryAddGalgameAsync(saveDirectory, true);
                });
                _galgameFolder.IsUnpacking = false;
                ChangeProgress(1, 1, string.Empty);

                if (StartFromBg && await App.GetService<ILocalSettingsService>().ReadSettingAsync<bool>(KeyValues.NotifyWhenUnpackGame)
                    && App.MainWindow is null)
                {
                    App.SystemTray?.ShowNotification("UnpackGameTask_Done_Title".GetLocalized(), 
                        "UnpackGameTask_Done_Msg".GetLocalized(GameName, saveDirectory));
                }
            }
            catch (Exception) //密码错误或压缩包损坏
            {
                _galgameFolder.IsUnpacking = false;
                ChangeProgress(-1, 1, string.Empty);
                if (saveDirectory != string.Empty && saveDirectory != _galgameFolder.Path)
                    Directory.Delete(saveDirectory, true); // 删除解压失败的文件夹
            }
        })!);
    }

    public override bool OnSearch(string key) => GalgameFolderPath == key;

    public override string Title { get; } = "UnpackGameTask_Title".GetLocalized();

    private static void Init()
    {
        if (_init) return;
        var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, 
            "Assets", "Libs", Environment.Is64BitProcess ? "x64" : "x86", "7za.dll");
        SevenZipBase.SetLibraryPath(path);
        _init = true;
    }
}