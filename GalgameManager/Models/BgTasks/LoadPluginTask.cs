using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Services;
using LiteDB;

namespace GalgameManager.Models.BgTasks;

public class LoadPluginTask : BgTaskBase
{
    private readonly IPluginService _pluginService = App.GetService<IPluginService>();
    private readonly ILocalSettingsService _settingService = App.GetService<ILocalSettingsService>();
    
    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected override Task RunInternal() => Task.Run(async () =>
    {
        ILiteCollection<PluginService.PluginData> dataDb =
            _settingService.Database.GetCollection<PluginService.PluginData>("plugin_data");
        List<string> pluginPaths = await _settingService.ReadSettingAsync<List<string>>(KeyValues.PluginPaths) ?? [];
        for (var i = 0; i < pluginPaths.Count; i++)
        {
            var path = pluginPaths[i];
            ChangeProgress(i, pluginPaths.Count, "LoadPluginTask_Loading".GetLocalized(path));
            try
            {
                await _pluginService.LoadPluginsAsync(path);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        ChangeProgress(1, 1, string.Empty, false);
    });

    public override string Title => "LoadPluginTask_Title".GetLocalized();
}