using CommunityToolkit.WinUI.Collections;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Helpers.API.RepoFlow;
using Newtonsoft.Json;

namespace GalgameManager.Models.BgTasks;

public class GetStorePluginTask(AdvancedCollectionView pluginList) : QueueTaskBase<StorePlugin>
{
    protected override int MaxRunning() => 5;
    private readonly IRepoFlowApi _api = RepoFlowApi.GetApi();
    private readonly IInfoService _infoService = App.GetService<IInfoService>();
    private readonly IPluginService _pluginService = App.GetService<IPluginService>();
    private readonly HttpClient _httpClient = Utils.GetDefaultHttpClient();

    private const string DocPackageName = "doc";
    private const string PluginPackageName = "plugin";
    private const string InfoFileName = "plugin-info.json";
    private const string PluginFileName = "plugin.pvnplugin.zip";

    protected async override Task InitializeAsync()
    {
        await base.InitializeAsync();
        await UiThreadInvokeHelper.InvokeAsync(pluginList.Clear);
        List<Repository> tmp = await _api.GetRepositoriesAsync(RepoFlowApi.WorkspaceId);
        foreach (Repository rep in tmp)
            await UiThreadInvokeHelper.InvokeAsync(() =>
            {
                Queue.Enqueue(new StorePlugin
                {
                    RepoName = rep.Name,
                });
            });
    }

    public override string Title => "GetStorePluginTask_Title".GetLocalized();

    protected async override Task ProcessItemAsync(StorePlugin item)
    {
        List<Task> tasks =
        [
            GetPluginInfo(item),
            GetPluginVersionsInfo(item),
        ];
        Version? installedVersion;
        try
        {
            await Task.WhenAll(tasks);
            if (item.Versions.Count > 0)
                item.ReleaseDate = item.Versions[0].ReleaseDate;

            installedVersion = (await _pluginService.GetAllPluginsAsync())
                .FirstOrDefault(p => p.Info.Id == item.Id)?.Version;
        }
        catch (Exception e)
        {
            _infoService.DeveloperEvent(msg: $"获取插件{item.RepoName}信息失败", e: e);
            return;
        }

        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            item.UpdateStatus(installedVersion);
            pluginList.Add(item);
            pluginList.RefreshFilter();
        });
    }

    protected override string ProgressTitle() => "GetStorePluginTask_ProgressTitle";

    protected override string ProgressMsg(StorePlugin item) => string.Empty;

    protected override string ProgressWaitingMsg() => "GetStorePluginTask_ProgressWaitingMsg";

    private async Task GetPluginInfo(StorePlugin plugin)
    {
        Version docVer = (await _api.GetPackageMetaAsync(RepoFlowApi.WorkspaceName, plugin.RepoName, DocPackageName))
            .LatestVersion;
        PluginInfo? info = JsonConvert.DeserializeObject<PluginInfo>(await _httpClient.GetStringAsync(
            RepoFlowApi.GetDownloadUrl(plugin.RepoName, DocPackageName,
                docVer.ToString(), InfoFileName)));
        if (info is null) throw new PvnException("找不到插件信息文件或格式错误");
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            plugin.Id = info.Id;
            plugin.Name = info.Name;
            plugin.DescriptionShort = info.DescriptionShort;
            plugin.DescriptionDetailed = info.DescriptionDetailed;
            plugin.LogoUrl = RepoFlowApi.GetDownloadUrl(plugin.RepoName, DocPackageName,
                docVer.ToString(), info.IconFileName);
            plugin.Developer = info.Developer;
            plugin.DeveloperUrl = info.DeveloperUrl;
            plugin.ProjectUrl = info.ProjectUrl;
            plugin.Types = PluginTypeHelper.Separate(info.PluginType);
        });
    }

    private async Task GetPluginVersionsInfo(StorePlugin plugin)
    {
        List<PackageDetailVersion> tmp =
            await _api.GetPackageDetailVersionAsync(RepoFlowApi.WorkspaceName, plugin.RepoName, PluginPackageName);
        foreach (PackageDetailVersion version in tmp)
        {
            var downloadUrl = RepoFlowApi.GetDownloadUrl(plugin.RepoName, PluginPackageName,
                version.Version.ToString(), PluginFileName);
            plugin.Versions.Add(new StorePluginVersion
            {
                Version = version.Version,
                DownloadUrl = downloadUrl,
                ReleaseDate = version.CreatedAt,
            });
            plugin.Versions.Sort((a, b) => b.Version.CompareTo(a.Version));
        }
    }

    private class PluginInfo
    {
#pragma warning disable CS0649 // 忽略未赋值警告，JsonProperty会赋值
        [JsonProperty("guid")] public Guid Id;
        [JsonProperty("name")] public string Name = string.Empty;
        [JsonProperty("description_short")] public string DescriptionShort = string.Empty;
        [JsonProperty("description_detailed")] public string DescriptionDetailed = string.Empty;
        [JsonProperty("icon")] public string IconFileName = string.Empty;
        [JsonProperty("developer")] public string? Developer;
        [JsonProperty("developer_url")] public string? DeveloperUrl;
        [JsonProperty("project_url")] public string? ProjectUrl;
        [JsonProperty("types")] public int PluginType;
#pragma warning restore CS0649
    }
}
