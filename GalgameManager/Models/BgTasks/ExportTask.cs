using System.IO.Compression;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;

namespace GalgameManager.Models.BgTasks;

public class ExportTask (string targetPath) : BgTaskBase
{
    public string TargetPath = targetPath; // 导出zip文件的文件夹路径
    private readonly ILocalSettingsService _settingService = App.GetService<ILocalSettingsService>();
    private readonly IGalgameCollectionService _gameService = App.GetService<IGalgameCollectionService>();
    private readonly IGalgameSourceCollectionService _sourceService = App.GetService<IGalgameSourceCollectionService>();
    private readonly ICategoryService _categoryService = App.GetService<ICategoryService>();
    private readonly IStaffService _staffService = App.GetService<IStaffService>();
    private readonly IFileService _fileService = App.GetService<IFileService>();
    private readonly string _fileName = CreateFileName(DateTime.Now);
    
    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;
    
    protected async override Task RunInternal()
    {
        if (!Utils.IsPathWritable(TargetPath))
            throw new UnauthorizedAccessException("ExportTask_PathNotWritable".GetLocalized(TargetPath));
        if (File.Exists(OutputFilePath))
            throw new InvalidOperationException("ExportTask_FileExist".GetLocalized(OutputFilePath));
        try
        {
            await (await _settingService.GetTmpExportFolder()).DeleteAsync(); // 防止某些情况下临时文件夹未被删除
            StorageFolder tmp = await _settingService.GetTmpExportFolder();
            
            // 导出游戏信息
            await _gameService.ExportAsync((msg, current, total) => { ChangeProgress(current, total, msg); });
            // 导出游戏库
            await _sourceService.ExportAsync((msg, current, total) => { ChangeProgress(current, total, msg); });
            // 导出分类（组）
            await _categoryService.ExportAsync((msg, current, total) => { ChangeProgress(current, total, msg); });
            // 导出staff库
            await _staffService.ExportAsync((msg, current, total) => { ChangeProgress(current, total, msg); });
            // 导出数据状态
            LocalSettingStatus status = await _settingService
                .ReadSettingAsync<LocalSettingStatus>(KeyValues.DataStatus, true) ?? new();
            status = status.Clone(); // 防止修改原数据
            status.SetToExport();
            await _settingService.AddToExportAsync(KeyValues.DataStatus, status);
            // 导出主页设置
            await _settingService.AddToExportDirectlyAsync(KeyValues.MultiStreamPageList);
            // 导出游戏列表页/库页设置（排序等）
            await ExportPageSettingsAsync();
            
            await _fileService.WaitForWriteFinishAsync();

            // 压缩
            ChangeProgress(0, 1, "ExportTask_Compressing".GetLocalized());
            ZipFile.CreateFromDirectory(tmp.Path, OutputFilePath, CompressionLevel.Optimal, false);
            await _settingService.SaveSettingAsync(KeyValues.LastExportTime, DateTime.Now);
        }
        finally
        {
            await (await _settingService.GetTmpExportFolder()).DeleteAsync();
        }
        
        ChangeProgress(1, 1, "ExportTask_Success".GetLocalized(OutputFilePath));
    }

    /// <summary>
    /// 收集游戏列表页/库页的设置（排序等），打包进导出文件；导入时由
    /// <see cref="Services.LocalSettingsService.ImportPageSettingsAsync"/> 读回
    /// </summary>
    private async Task ExportPageSettingsAsync()
    {
        PageSettings pageSettings = new()
        {
            PrimarySortKey = await _settingService.ReadSettingAsync<int>(KeyValues.PrimarySortKey),
            PrimarySortDescending = await _settingService.ReadSettingAsync<bool>(KeyValues.PrimarySortDescending),
            SecondarySortKey = await _settingService.ReadSettingAsync<int>(KeyValues.SecondarySortKey),
            SecondarySortDescending = await _settingService.ReadSettingAsync<bool>(KeyValues.SecondarySortDescending),
            CustomSortOrder = await _settingService.ReadSettingAsync<List<string>>(KeyValues.CustomSortOrder, true),
            LibrarySortKey = await _settingService.ReadSettingAsync<int>(KeyValues.LibrarySortKey),
            LibraryGameSortDescending = await _settingService.ReadSettingAsync<bool>(KeyValues.LibraryGameSortDescending),
            LibraryFolderSortKey = await _settingService.ReadSettingAsync<int>(KeyValues.LibraryFolderSortKey),
            LibraryFolderSortDescending = await _settingService.ReadSettingAsync<bool>(KeyValues.LibraryFolderSortDescending),
        };
        await _settingService.AddToExportAsync(KeyValues.PageSettings, pageSettings);
    }

    public override string Title => "ExportTask_Title".GetLocalized();
    
    public override bool OnSearch(string key) => true;

    private static string CreateFileName(DateTime timestamp) =>
        $"PotatoVN_{timestamp:yy-MM-dd_HH-mm-ss}.pvnExport.zip";
    
    private string OutputFilePath => $"{TargetPath}\\{_fileName}";
}
