using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace GalgameManager.Models.BgTasks;

/// <summary>
/// 从插件商店下载安装/更新插件的后台任务。
/// </summary>
public class InstallStorePluginTask : BgTaskBase
{
    /// <summary>
    /// 要下载的插件包地址。
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 插件最终存放目录（PluginService.PluginDir 下的某个子目录）。
    /// </summary>
    public string PluginFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// 仅用于显示的插件名称。
    /// </summary>
    public string PluginName { get; set; } = string.Empty;
    private Guid _pluginId = Guid.Empty;
    private Version _version = new();
    /// 发起这个任务的商店插件条目，安装成功后需要回写其状态，让界面上的角标/过滤立即更新。<br/>
    /// 仅在内存中传递（本任务未注册进BgTaskService的启动串恢复），从Json恢复时为null。
    private readonly StorePlugin? _storePlugin;

    public InstallStorePluginTask()
    {
    }

    public InstallStorePluginTask(StorePlugin plugin, StorePluginVersion version)
    {
        _storePlugin = plugin;
        PluginName = plugin.Name;
        DownloadUrl = version.DownloadUrl;

        IPluginService pluginService = App.GetService<IPluginService>();
        // 插件目录：pluginService.PluginDir/{pluginName}
        var folderName = FileHelper.RemoveInvalidFileNameChars(plugin.Name);
        PluginFolderPath = Path.Combine(pluginService.PluginDir.FullName, folderName);
        _version = version.Version;
        _pluginId = plugin.Id;
    }

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    public override string Title => "InstallStorePluginTask_Title".GetLocalized(PluginName);

    protected async override Task RunInternal()
    {
        if (string.IsNullOrWhiteSpace(DownloadUrl) || string.IsNullOrWhiteSpace(PluginFolderPath))
            throw new PvnException("InstallStorePluginTask invalid arguments");

        // 目标目录：先清理再创建，避免残留旧版本文件。
        if (Directory.Exists(PluginFolderPath))
            Directory.Delete(PluginFolderPath, true);
        Directory.CreateDirectory(PluginFolderPath);

        var zipPath = Path.Combine(PluginFolderPath, "plugin.zip");

        try
        {
            await DownloadPluginAsync(DownloadUrl, zipPath);
            await ExtractPluginAsync(zipPath, PluginFolderPath);

            IPluginService pluginService = App.GetService<IPluginService>();
            // 从商店安装的 plugin 永远非 dev 模式。
            // 已经安装过的插件（在数据库里）调用这个bgTask是升级插件，不需要再添加插件了，只覆盖文件和版本号，后面的加载步骤会把插件加载进来
            if (!pluginService.PluginInDb(_pluginId))
                await pluginService.AddPluginAsync(PluginFolderPath, false);
            pluginService.SetPluginVersion(_pluginId, _version); //插件版本由商店提供

            // 回写商店条目的状态，否则插件商店页面要重新进入才能看到"已安装"
            if (_storePlugin is not null)
                await UiThreadInvokeHelper.InvokeAsync(() => _storePlugin.UpdateStatus(_version));

            ChangeProgress(1, 1, "InstallStorePluginTask_Installed".GetLocalized(PluginName));
        }
        catch
        {
            // 任意一步失败都要清理目录。
            try
            {
                if (Directory.Exists(PluginFolderPath))
                    Directory.Delete(PluginFolderPath, true);
            }
            catch
            {
                // ignore
            }

            throw;
        }
        finally
        {
            try
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task DownloadPluginAsync(string url, string targetFilePath)
    {
        HttpClient httpClient = Utils.GetDefaultHttpClient();
        using HttpResponseMessage response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 1;
        var hasContentLength = response.Content.Headers.ContentLength.HasValue;

        await using Stream contentStream = await response.Content.ReadAsStreamAsync();
        await using FileStream fileStream = new(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        DateTime startTime = DateTime.UtcNow;

        while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            if (hasContentLength)
            {
                // 有 Content-Length：展示百分比，文案："正在下载插件 {name}..."
                ChangeProgress(totalRead, totalBytes, "InstallStorePluginTask_Downloading".GetLocalized(PluginName));
            }
            else
            {
                // 无 Content-Length：展示平均速度，文案："正在下载插件， xxx kB/s"
                var elapsedSeconds = (DateTime.UtcNow - startTime).TotalSeconds;
                if (elapsedSeconds <= 0) elapsedSeconds = 0.001;
                var speedKbPerSec = totalRead / 1024.0 / elapsedSeconds;
                var speedText = $"{speedKbPerSec:F1}";
                ChangeProgress(0, 1, "InstallStorePluginTask_Downloading_NoLength".GetLocalized(speedText));
            }
        }
    }

    private static Task ExtractPluginAsync(string zipPath, string targetDirectory)
    {
        return Task.Run(() =>
        {
            using IArchive archive = ArchiveFactory.OpenArchive(zipPath, ReaderOptions.ForFilePath);
            archive.WriteToDirectory(targetDirectory, new ExtractionOptions
            {
                ExtractFullPath = true,
                Overwrite = true,
            });
        });
    }
}
