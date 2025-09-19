using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.WinApp.Base.Contracts;

namespace GalgameManager.Models;

public partial class PluginX(IPlugin plugin, string path) : ObservableObject
{
    public IPlugin Plugin = plugin;
    [ObservableProperty] private string _path = path;
}