using System.Collections.ObjectModel;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Services;

public interface IPluginService
{
    /// <summary>
    /// 新增插件，注意捕获异常
    /// </summary>
    /// <param name="path"></param>
    /// <exception cref="PvnException">已知错误</exception>
    /// <returns></returns>
    public Task AddPluginAsync(string path);
    
    public Task<ObservableCollection<PluginX>> GetAllPluginsAsync();
    
    public Task InitAsync();
    
    /// <summary>
    /// 加载插件，注意捕获异常
    /// </summary>
    /// <param name="path"></param>
    /// <exception cref="PvnException">已知错误</exception>
    /// <returns></returns>
    public Task LoadPluginsAsync(string path);
}