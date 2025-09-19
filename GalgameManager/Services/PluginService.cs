using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Exceptions;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Models;
using LiteDB;

namespace GalgameManager.Services;

public class PluginService(
    ILocalSettingsService settingService,
    IBgTaskService bgTaskService,
    IMessenger bus) : IPluginService
{
    private ObservableCollection<PluginX> _plugins = [];
    private ILiteCollection<PluginData> _pluginDataDbSet = null!;

    public async Task AddPluginAsync(string path)
    {
        await LoadPluginsAsync(path);
        List<string> pluginPaths = _plugins.Select(p => p.Path).ToList();
        await settingService.SaveSettingAsync(KeyValues.PluginPaths, pluginPaths);
    }

    public Task<ObservableCollection<PluginX>> GetAllPluginsAsync() => Task.FromResult(_plugins);

    public async Task InitAsync()
    {
        await Task.CompletedTask; //预留异步
        _pluginDataDbSet = settingService.Database.GetCollection<PluginData>("plugin_data");
        _ = bgTaskService.AddBgTask(new LoadPluginTask());
    }

    public async Task LoadPluginsAsync(string path)
    {
        if (!Directory.Exists(path)) throw new PvnPathNotExist(path);
        var pluginFile = Directory.GetFiles(path, "PotatoVN.App.PluginBase.dll").FirstOrDefault();
        if (pluginFile == null) throw new PvnException($"plugin dll of {path} not found");
        Assembly pluginAssembly = Assembly.LoadFrom(pluginFile);
        Type? pluginType = pluginAssembly.GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface);
        if (pluginType == null) throw new PvnException($"no valid plugin found in {path}");

        IPlugin plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
        PluginInfo info = plugin.Info;
        if (_plugins.Any(p => p.Plugin.Info.Id == info.Id))
            throw new PvnException($"plugin {info.Name} already loaded");
        await UiThreadInvokeHelper.InvokeAsync(() => { _plugins.Add(new PluginX(plugin, path)); });
        bus.Send(new PluginLoadArgs { Plugin = plugin });
    }

    public class PluginData
    {
        Guid Id { get; set; }
        string? Data { get; set; }
    }
}