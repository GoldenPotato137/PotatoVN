using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Windows.ApplicationModel.Resources.Core;
using Windows.Storage;
using System.Collections;
using GalgameManager.Helpers;

namespace GalgameManager.Services;

internal static class PluginXamlHost
{
    // MSIX 的安装目录是只读的，热重载暂存目录必须放在可写的应用数据目录下（见 issue #693）
    public static string HotReloadRoot => Path.Combine(AppStoragePaths.LocalDataPath, "_PluginXamlHotReload");
    private static readonly object SyncRoot = new();
    private static readonly ConcurrentDictionary<string, PluginAssemblyRegistration> Registrations = new();

    private static Application? _application;
    private static PropertyInfo? _metadataProviderProperty;
    private static PropertyInfo? _typeInfoProviderProperty;
    private static PropertyInfo? _otherProvidersProperty;

    public static void Initialize(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        lock (SyncRoot)
        {
            if (_application is not null)
            {
                if (ReferenceEquals(_application, application))
                    return;
                throw new InvalidOperationException("PluginXamlHost has already been initialized.");
            }

            _application = application;
            Type appType = application.GetType();
            _metadataProviderProperty = appType.GetProperty("_AppProvider",
                                            BindingFlags.NonPublic | BindingFlags.Instance)
                                        ?? throw new InvalidOperationException(
                                            $"Failed to access XAML metadata provider on {appType.FullName}.");
            _typeInfoProviderProperty = _metadataProviderProperty.PropertyType.GetProperty("Provider",
                                            BindingFlags.NonPublic | BindingFlags.Instance)
                                        ?? throw new InvalidOperationException(
                                            $"Failed to access XAML type provider on {appType.FullName}.");
            _otherProvidersProperty = _typeInfoProviderProperty.PropertyType.GetProperty("OtherProviders",
                                            BindingFlags.NonPublic | BindingFlags.Instance)
                                      ?? throw new InvalidOperationException(
                                          $"Failed to access XAML metadata provider list on {appType.FullName}.");
            //清理旧的热重载残留
            try
            {
                if (Directory.Exists(HotReloadRoot)) Directory.Delete(HotReloadRoot, true);
            }
            catch (Exception)
            {
                //ignore
            }
        }
    }

    public static async Task<IDisposable> RegisterPluginAssemblyAsync(Assembly assembly, string pluginRootPath,
        bool enableHotReload)
    {
        if (_application is null) throw new InvalidOperationException("PluginXamlHost is not initialized."); //不应该发生
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginRootPath);

        var assemblyFullName = assembly.FullName
                               ?? throw new InvalidOperationException("Plugin assembly full name is missing.");
        var fullPluginRootPath = Path.GetFullPath(pluginRootPath);
        if (Registrations.TryGetValue(assemblyFullName, out PluginAssemblyRegistration? existingRegistration))
            existingRegistration.Dispose();

        PluginAssemblyRegistration registration = new(assembly, fullPluginRootPath, enableHotReload);
        await registration.InitializeAsync();
        Registrations[assemblyFullName] = registration;
        return registration;
    }

    public static string GetRuntimePath(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var assemblyFullName = assembly.FullName
                               ?? throw new InvalidOperationException("Plugin assembly full name is missing.");
        if (!Registrations.TryGetValue(assemblyFullName, out PluginAssemblyRegistration? registration))
            throw new InvalidOperationException($"Plugin assembly {assemblyFullName} is not registered for XAML loading.");
        return registration.RuntimePath;
    }

    /// 这个函数的功能见这个注释：<see cref="PluginInvokeHelper.EnterPluginXamlScope"/>
    public static IDisposable EnterScope(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var assemblyFullName = assembly.FullName
                               ?? throw new InvalidOperationException("Plugin assembly full name is missing.");

        if (!Registrations.TryGetValue(assemblyFullName, out PluginAssemblyRegistration? registration))
            return NoopDisposable.Instance;
        lock (SyncRoot)
        {
            ClearHostXamlTypeCaches();
            List<IXamlMetadataProvider> providers = GetOtherProviders();
            List<IXamlMetadataProvider> snapshot = [.. providers];

            foreach (PluginAssemblyRegistration item in Registrations.Values)
                item.RemoveProvidersFrom(providers);
            registration.AddProvidersTo(providers);

            return new ProviderScope(snapshot);
        }
    }

    private static IDisposable RegisterXamlMetadataProvider(IXamlMetadataProvider provider)
    {
        lock (SyncRoot)
        {
            GetOtherProviders().Add(provider);
        }

        return new MetadataProviderRegistration(provider);
    }

    private static void Unregister(PluginAssemblyRegistration registration)
    {
        if (Registrations.TryGetValue(registration.AssemblyFullName, out PluginAssemblyRegistration? current)
            && ReferenceEquals(current, registration))
            Registrations.TryRemove(registration.AssemblyFullName, out _);
    }

    private static List<IXamlMetadataProvider> GetOtherProviders()
    {
        var provider = GetHostTypeInfoProvider();
        return (_otherProvidersProperty?.GetValue(provider) as List<IXamlMetadataProvider>)
               ?? throw new InvalidOperationException("Failed to access WinUI XAML metadata provider list.");
    }

    private static object GetHostTypeInfoProvider()
    {
        Application application = _application ?? throw new InvalidOperationException("PluginXamlHost is not initialized.");
        var appProvider = _metadataProviderProperty?.GetValue(application)
                          ?? throw new InvalidOperationException("Failed to access WinUI app metadata provider.");
        return _typeInfoProviderProperty?.GetValue(appProvider)
               ?? throw new InvalidOperationException("Failed to access WinUI XAML type provider.");
    }

    private static void ClearHostXamlTypeCaches()
    {
        var provider = GetHostTypeInfoProvider();
        Type providerType = provider.GetType();
        ClearDictionaryField(providerType, provider, "_xamlTypeCacheByName");
        ClearDictionaryField(providerType, provider, "_xamlTypeCacheByType");
        ClearDictionaryField(providerType, provider, "_xamlMembers");
    }

    private static void ClearDictionaryField(Type providerType, object provider, string fieldName)
    {
        FieldInfo? field = providerType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(provider) is IDictionary dictionary)
            dictionary.Clear();
    }

    private sealed class MetadataProviderRegistration(IXamlMetadataProvider provider) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (SyncRoot)
            {
                GetOtherProviders().Remove(provider);
            }

            _disposed = true;
        }
    }

    private sealed class ProviderScope(List<IXamlMetadataProvider> snapshot) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (SyncRoot)
            {
                List<IXamlMetadataProvider> providers = GetOtherProviders();
                providers.Clear();
                providers.AddRange(snapshot);
                ClearHostXamlTypeCaches();
            }

            _disposed = true;
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class PluginAssemblyRegistration(Assembly assembly, string pluginRootPath, bool enableHotReload) : IDisposable
    {
        private readonly List<IDisposable> _providerRegistrations = [];
        private readonly List<IXamlMetadataProvider> _providers = [];
        private readonly List<StorageFile> _loadedPriFiles = [];
        private bool _disposed;
        //对于需要热重载的插件，我们需要把它挪到一个随时间变化的临时目录中。
        //如果不挪动使用插件自己的目录，XAML加载器会认为插件的资源文件没有变化，从而无法实现热重载效果。
        private string? _resourceRootPath;

        public string AssemblyFullName { get; } = assembly.FullName ?? throw new InvalidOperationException(
                                                       "Plugin assembly full name is missing.");
        private string AssemblySimpleName { get; } = assembly.GetName().Name ?? throw new InvalidOperationException(
            "Plugin assembly name is missing.");
        public string RuntimePath => _resourceRootPath ?? pluginRootPath;

        public async Task InitializeAsync()
        {
            _resourceRootPath = PrepareResourceRoot();
            await LoadPriFilesAsync();
            await UiThreadInvokeHelper.InvokeAsync(RegisterMetadataProviders);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            InvokeOnUiThread(() =>
            {
                if (_loadedPriFiles.Count > 0)
                {
                    ResourceManager.Current.UnloadPriFiles(_loadedPriFiles);
                    _loadedPriFiles.Clear();
                }

                foreach (IDisposable registration in _providerRegistrations)
                    registration.Dispose();
                _providerRegistrations.Clear();
            });

            Unregister(this);
            _disposed = true;
        }

        private string PrepareResourceRoot()
        {
            if (!enableHotReload) return pluginRootPath;

            var sourceResourceDir = Path.Combine(pluginRootPath, AssemblySimpleName);
            if (!Directory.Exists(sourceResourceDir))
                return pluginRootPath;

            Directory.CreateDirectory(HotReloadRoot);
            var token = $"{DateTime.UtcNow:yyyyMMddHHmmssfffffff}_{Guid.NewGuid():N}";
            var stageRoot = Path.Combine(HotReloadRoot, token);

            try
            {
                FolderOperations.Copy(pluginRootPath, stageRoot);
                // CopyDirectory(pluginRootPath, stageRoot);
                return stageRoot;
            }
            catch (Exception)
            {
                if (Directory.Exists(stageRoot)) Directory.Delete(stageRoot, true);
                return pluginRootPath;
            }
        }

        private async Task LoadPriFilesAsync()
        {
            var resourceRootPath = _resourceRootPath ?? pluginRootPath;
            HashSet<string> candidates =
            [
                Path.Combine(resourceRootPath, "resources.pri"),
                Path.Combine(resourceRootPath, $"{AssemblySimpleName}.pri"),
            ];
            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate))
                    continue;
                _loadedPriFiles.Add(await StorageFile.GetFileFromPathAsync(candidate));
            }

            if (_loadedPriFiles.Count == 0)
                return;

            try
            {
                await UiThreadInvokeHelper.InvokeAsync(() => ResourceManager.Current.LoadPriFiles(_loadedPriFiles));
            }
            catch (COMException ex) when ((uint)ex.HResult == 0x80004004)
            {
                await Task.Delay(500);
                await UiThreadInvokeHelper.InvokeAsync(() => ResourceManager.Current.LoadPriFiles(_loadedPriFiles));
            }
        }

        private void RegisterMetadataProviders()
        {
            foreach (Type type in GetLoadableTypes())
            {
                if (!typeof(IXamlMetadataProvider).IsAssignableFrom(type))
                    continue;
                if (type is { IsInterface: true } or { IsAbstract: true })
                    continue;
                if (Activator.CreateInstance(type) is IXamlMetadataProvider provider)
                {
                    _providers.Add(provider);
                    _providerRegistrations.Add(RegisterXamlMetadataProvider(provider));
                }
            }
        }

        public void RemoveProvidersFrom(List<IXamlMetadataProvider> providers)
        {
            foreach (IXamlMetadataProvider provider in _providers)
                providers.Remove(provider);
        }

        public void AddProvidersTo(List<IXamlMetadataProvider> providers)
        {
            foreach (IXamlMetadataProvider provider in _providers)
                providers.Add(provider);
        }

        private IEnumerable<Type> GetLoadableTypes()
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t is not null).Cast<Type>();
            }
        }

        private static void InvokeOnUiThread(Action action)
        {
            if (App.DispatcherQueue.HasThreadAccess)
            {
                action();
                return;
            }

            UiThreadInvokeHelper.InvokeAsync(action).GetAwaiter().GetResult();
        }
    }
}
