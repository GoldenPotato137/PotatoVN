using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Loader;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Exceptions;
using GalgameManager.WinApp.Base.Contracts;
using LiteDB;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;



public partial class PluginService(
    ILocalSettingsService settingService,
    IBgTaskService bgTaskService,
    IInfoService infoService,
    IMessenger bus) : IPluginService
{
    private ObservableCollection<PluginX> _plugins = [];
    private ILiteCollection<PluginX> _pluginsDb = null!;
    private ILiteCollection<PluginData> _pluginDataDb = null!;

    public bool PluginOffloadInProgress { get; private set; }

    public void PluginSetData(PluginX plugin, string? data)
    {
        PluginData newData = new()
        {
            PluginId = plugin.Id,
            Data = data,
        };
        _pluginDataDb.Upsert(newData);
    }

    public void PluginDeleteData(PluginX plugin) => _pluginDataDb.Delete(plugin.Info.Id);

    public async Task AddPluginAsync(string path, bool isDev)
    {
        // 如果删除过正常插件，即使是 Dev 模式也会禁止加载新的插件。
        if (PluginOffloadInProgress)
            throw new PvnException("PluginService_PluginOffloadInProgress".GetLocalized());
        if (_plugins.Any(p => Utils.ArePathsEqual(path, p.Path)))
            throw new PvnException($"plugin in {path} already initialized");
        (IPlugin plugin, PluginLoadContext contex) tmp = await LoadPluginInternalAsync(path, isDev);
        PluginX plugin = new(tmp.plugin, path, tmp.contex)
        {
            Enable = true,
            IsDevMode = isDev,
        };
        // 开发版插件：加载了同一个插件时先卸载旧插件
        if (_plugins.FirstOrDefault(p => p.Info.Id == plugin.Info.Id && p.IsDevMode) is { } existPlugin)
            await DeletePluginAsync(existPlugin, false);
        await LoadPluginAsync(plugin, true);
        // 使用 Insert 来做最后的判重，防止重复加载 Dev 插件。
        _pluginsDb.Insert(plugin);
    }

    public async Task InvokePluginOnUninstall(PluginX plugin)
    {
        try
        {
            await plugin.ExecuteUninstallWithTimeoutAsync();
        }
        catch (OperationCanceledException)
        {
            /* 插件内部中止操作 */
            App.GetService<IInfoService>().Event(EventType.PluginError, InfoBarSeverity.Warning,
                $"Abort",
                msg: $"Dev plugin {plugin.Info.Name} uninstalled internal abort"
            );
        }
        catch (TimeoutException)
        {
            /* 卸载超时：硬超时 */
            App.GetService<IInfoService>().Event(EventType.PluginError, InfoBarSeverity.Warning,
                $"Timeout",
                msg: $"Dev plugin {plugin.Info.Name} uninstalled timeout"
            );
        }
        catch (Exception e)
        {
            /* 卸载错误 */
            App.GetService<IInfoService>().Event(EventType.PluginError, InfoBarSeverity.Warning,
                $"Dev plugin {plugin.Info.Name} uninstalled with errors",
                msg: $"Dev plugin {plugin.Info.Name} uninstalled with errors...\n${e}"
            );
        }
    }

    public async Task DeletePluginAsync(PluginX plugin, bool deleteData)
    {
        plugin.ToDelete = true;
        plugin.ToDeleteData = deleteData;
        if (!plugin.IsDevMode)
        {
            // 对于 Dev Plugin，我们立即删除对应的 Plugin，同时不进入 Offload State
            PluginOffloadInProgress = true;
        }

        if (plugin.IsDevMode)
        {
            await InvokePluginOnUninstall(plugin);
        }

        // 先移除内存中的 Plugin，触发 UI 更新，确保 UI 释放对 Plugin 程序集中类型的引用
        await UiThreadInvokeHelper.InvokeAsync(() => _plugins.Remove(plugin));

        // 非 UI 操作到后台线程，避免阻塞 UI，同时给予 UI 线程处理可视树更新的时间
        await Task.Run(() =>
        {
            if (plugin.IsDevMode)
            {
                _pluginsDb.Delete(plugin.Info.Id);
                if (deleteData)
                    PluginDeleteData(plugin);
                plugin.ForceUnload();
                // 对于使用安装包安装的测试版插件（会装在软件数据目录下），应该在插件卸载时删掉目录
                if (new DirectoryInfo(plugin.Path).IsChildOf(PluginDir))
                {
                    try
                    {
                        Directory.Delete(plugin.Path, true);
                    }
                    catch (Exception e)
                    {
                        infoService.Event(EventType.PluginError, InfoBarSeverity.Warning,
                            "PluginService_DeleteDevPluginFailed".GetLocalized(plugin.Info.Name),
                            msg: $"Failed to delete dev plugin directory {plugin.Path}. Please check the directory and delete it manually.\n{e}");
                    }
                }
            }
            else
            {
                SavePlugin(plugin);
            }
        });
    }

    public Task<ObservableCollection<PluginX>> GetAllPluginsAsync() => Task.FromResult(_plugins);

    public async Task InitAsync()
    {
        _pluginsDb = settingService.Database.GetCollection<PluginX>("plugin");
        _pluginDataDb = settingService.Database.GetCollection<PluginData>("plugin_data");
        PluginDir = new DirectoryInfo((await FileHelper.GetFolderAsync(FileHelper.FolderType.Plugins)).Path);
        if (!PluginDir.Exists) PluginDir.Create();

        // 先完成插件升级再加载插件
        try
        {
            List<ToInstallStorePlugin> list =
                await settingService.ReadSettingAsync<List<ToInstallStorePlugin>>(KeyValues.ToUpgradePlugin) ?? [];
            List<Task> tasks = [];
            foreach (ToInstallStorePlugin item in list)
                tasks.Add(bgTaskService.AddBgTask(new InstallStorePluginTask(item.Plugin, item.Version)));
            await Task.WhenAll(tasks);
        }
        catch (Exception e)
        {
            infoService.Event(EventType.PluginError, InfoBarSeverity.Warning,
                "PluginService_UpgradePluginFailed".GetLocalized(), exception: e);
        }
        finally
        {
            await settingService.RemoveSettingAsync(KeyValues.ToUpgradePlugin);
        }

        // 确保启动阶段所有已启用的插件（如网络代理/拦截插件）完成加载与初始化，避免后续服务在未就绪状态下发起请求
        await bgTaskService.AddBgTask(new LoadPluginTask());
    }

    public async Task LoadPluginAsync(PluginX plugin, bool load)
    {
        if (plugin.IsLoaded) return;
        if (load)
        {
            if (plugin.Plugin is null)
            {
                (IPlugin plugin, PluginLoadContext contex) tmp = await LoadPluginInternalAsync(plugin.Path, plugin.IsDevMode);
                plugin.Plugin = tmp.plugin;
                plugin.LoadContext = tmp.contex;
            }
            plugin.XamlRegistration?.Dispose();
            plugin.XamlRegistration = await PluginXamlHost.RegisterPluginAssemblyAsync(
                plugin.Plugin.GetType().Assembly, plugin.Path, plugin.IsDevMode);
            try
            {
                plugin.Info = plugin.Plugin.Info;
                await plugin.Plugin.InitializeAsync(new PotatoVnApiHost(plugin));
                plugin.IsLoaded = true;
            }
            catch
            {
                plugin.XamlRegistration?.Dispose();
                plugin.XamlRegistration = null;
                throw;
            }
        }
        else
        {
            App.GetService<ISidebarService>().UnregisterAllPluginButtons(plugin.Id);
        }
        if (_plugins.All(p => p.Info.Id != plugin.Info.Id))
            await UiThreadInvokeHelper.InvokeAsync(() => _plugins.Add(plugin));
        plugin.PropertyChanged -= OnPluginOnPropertyChanged;
        plugin.PropertyChanged += OnPluginOnPropertyChanged;
        if (load) bus.Send(new PluginLoadArgs { Plugin = plugin.Plugin! });
        return;

        async void OnPluginOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            try
            {
                if (args.PropertyName != nameof(PluginX.Enable)) return;
                if (plugin.Enable)
                    await LoadPluginAsync(plugin, true);
                else
                {
                    App.GetService<ISidebarService>().UnregisterAllPluginButtons(plugin.Id);
                    infoService.Info(InfoBarSeverity.Success, msg: "PluginService_PluginUnloaded".GetLocalized());
                    PluginOffloadInProgress = true;
                }
                _pluginsDb.Update(plugin);
            }
            catch (Exception e)
            {
                infoService.Event(EventType.AppError, InfoBarSeverity.Warning,
                    "PluginService_PluginStatusChangedFailed".GetLocalized(), exception: e);
            }
        }
    }

    public Task LoadFailedPluginAsync(PluginX pluginX)
    {
        _plugins.Add(pluginX);
        return Task.CompletedTask;
    }

    public DirectoryInfo PluginDir { get; private set; } = null!;

    public bool PluginInDb(Guid pluginId) => _pluginsDb.FindById(pluginId) != null;

    public void SetPluginVersion(Guid pluginId, Version version)
    {
        PluginX? plugin = _pluginsDb.FindById(pluginId);
        if (plugin is null) return; //不应该发生
        plugin.Version = version;
        _pluginsDb.Update(plugin);
    }

    public void ThrowPluginExceptionEvent(PluginX plugin, Exception e, string msgHeader)
    {
        infoService.Event(EventType.PluginError, InfoBarSeverity.Warning,
            "PluginService_PluginError".GetLocalized(plugin.Info.Name),
            msg: msgHeader + "PluginService_PluginError_Msg".GetLocalized(e.ToString()));
    }

    private static async Task<(IPlugin plugin, PluginLoadContext contex)> LoadPluginInternalAsync(string path, bool isDev)
    {
        await Task.CompletedTask; //预留异步
        if (!Directory.Exists(path)) throw new PvnPathNotExist(path);

        var pluginFile = await FindPluginAssemblyPathAsync();
        PluginLoadContext loadContext = new(pluginFile, isDev);

        Assembly pluginAssembly = await LoadAssemblyAsync(loadContext, pluginFile, isDev);
        Type? pluginType = FindPluginType(pluginAssembly);
        if (pluginType == null) throw new PvnException($"no valid plugin found in {path}");

        var pluginInstance = Activator.CreateInstance(pluginType)!;
        if (pluginInstance is not IPlugin plugin)
            throw new PvnException($"plugin type {pluginType.FullName} in {path} is incompatible with current host api");

        return (plugin, loadContext);

        async Task<string> FindPluginAssemblyPathAsync()
        {
            var directoryName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            List<string> candidates = Directory.GetFiles(path, "*.dll")
                .OrderByDescending(GetCandidatePriority)
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var candidate in candidates)
            {
                PluginLoadContext? tmpContext = null;
                try
                {
                    tmpContext = new PluginLoadContext(candidate, isDev);
                    Assembly assembly = await LoadAssemblyAsync(tmpContext, candidate, isDev);
                    if(FindPluginType(assembly) is not null) return candidate;
                }
                catch { /*ignore*/ }
                finally
                {
                    tmpContext?.Unload();
                }
            }
            throw new PvnException($"plugin dll of {path} not found");

            int GetCandidatePriority(string assemblyPath)
            {
                var fileName = Path.GetFileName(assemblyPath);
                if (fileName.Equals($"{directoryName}.dll", StringComparison.OrdinalIgnoreCase)) return 300;
                if (fileName.Equals("PotatoVN.App.PluginBase.dll", StringComparison.OrdinalIgnoreCase)) return 250;
                if (fileName.Contains("Plugin", StringComparison.OrdinalIgnoreCase)) return 200;
                if (fileName.StartsWith("GalgameManager.WinApp.Base", StringComparison.OrdinalIgnoreCase)) return -100;
                if (fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase)) return -100;
                if (fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)) return -100;
                if (fileName.StartsWith("CommunityToolkit.", StringComparison.OrdinalIgnoreCase)) return -100;
                if (fileName.StartsWith("WinRT.", StringComparison.OrdinalIgnoreCase)) return -100;
                return 0;
            }
        }

        Type? FindPluginType(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes().FirstOrDefault(IsPluginType);
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null).FirstOrDefault(t => IsPluginType(t!));
            }

            bool IsPluginType(Type type)
            {
                if (type.IsInterface || type.IsAbstract) return false;
                if (typeof(IPlugin).IsAssignableFrom(type)) return true;
                return type.GetInterfaces().Any(i => i.FullName == typeof(IPlugin).FullName);
            }
        }
    }

    private static async Task<Assembly> LoadAssemblyAsync(PluginLoadContext loadContext, string assemblyPath, bool isDev)
    {
        if (isDev)
        {
            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            FileStream? pdbStream = File.Exists(pdbPath)
                ? new FileStream(pdbPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                : null;
            await using (pdbStream)
            {
                await using FileStream fs = new(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return loadContext.LoadFromStream(fs, pdbStream);
            }
        }

        return loadContext.LoadFromAssemblyPath(assemblyPath);
    }

    public void SavePlugin(PluginX plugin) => _pluginsDb.Update(plugin);

    public class PluginData
    {
        [BsonId] public Guid PluginId { get; set; }
        public string? Data { get; set; }
    }
}

public class PluginLoadContext(string pluginPath, bool isDev = false) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);
    private readonly HashSet<string> _sharedAssembliesBlacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        // Host plugin api
        "GalgameManager.WinApp.Base",
        // WinAppSDK & WinUI
        "Microsoft.WinUI",
        "Microsoft.WindowsAppRuntime.Bootstrap.Net",
        "Microsoft.Windows.SDK.NET",
        "WinRT.Runtime",
        "Microsoft.Graphics.Imaging.Projection",
        "Microsoft.Web.WebView2.Core",
        "Microsoft.Windows.AppLifecycle.Projection",
        "Microsoft.Windows.AppNotifications.Projection",
        "Microsoft.Windows.System.Projection",
        // CommunityToolkit
        "CommunityToolkit.Common",
        "CommunityToolkit.Mvvm",
        "CommunityToolkit.WinUI.Collections",
        "CommunityToolkit.WinUI.Extensions",
        "CommunityToolkit.WinUI.Helpers",
    };

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (_sharedAssembliesBlacklist.Contains(assemblyName.Name!)) return TryLoadSharedAssembly(assemblyName);
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            if (isDev)
            {
                using var fs = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
                if (File.Exists(pdbPath))
                {
                    using var pdbFs = new FileStream(pdbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return LoadFromStream(fs, pdbFs);
                }
                return LoadFromStream(fs);
            }
            return LoadFromAssemblyPath(assemblyPath);
        }
        // dll不存在
        return null;

        Assembly? TryLoadSharedAssembly(AssemblyName asName)
        {
            Assembly? loadedAssembly =
                Default.Assemblies.FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), asName));
            if (loadedAssembly != null) return loadedAssembly;
            try
            {
                return Default.LoadFromAssemblyName(asName);
            }
            catch
            {
                return null;
            }
        }
    }
}
