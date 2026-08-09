using GalgameManager.Contracts.Phrase;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Models;
using GalgameManager.WinApp.Base.Models.Plugin;

namespace GalgameManager.Plugin.ApiTest;

public sealed class ApiProbePlugin : IPlugin, IParserProvider
{
    internal const int ParserTypeValue = 137;
    internal const string SidebarButtonId = "api-probe";
    internal static readonly Guid PluginId = Guid.Parse("f73eb558-cb4b-4d10-b271-2d44ba91af4d");

    internal static IPotatoVnApi HostApi { get; private set; } = null!;

    public PluginInfo Info { get; } = new()
    {
        Id = PluginId,
        Name = "Plugin API Probe",
        Description = "Exercises the PotatoVN host API through a real dynamically loaded plugin.",
    };

    public string ParserName => "Plugin API Probe Parser";

    public IGalInfoPhraser GetPhraser() => ProbeParser.Instance;

    public Task InitializeAsync(IPotatoVnApi hostApi)
    {
        HostApi = hostApi;
        RegisterSidebarButton();
        return Task.CompletedTask;
    }

    public Task OnUninstallAsync(bool deleteData, Action<TimeSpan> extendWaitHandler, CancellationToken cts)
    {
        if (!cts.IsCancellationRequested) HostApi.UnregisterSidebarButton(SidebarButtonId);
        return Task.CompletedTask;
    }

    internal static void RegisterSidebarButton()
    {
        HostApi.RegisterSidebarButton(new SidebarButtonInfo
        {
            Id = SidebarButtonId,
            Text = "Plugin API Probe",
            Placement = SidebarButtonPlacement.Menu,
            FallbackGlyph = "&#xE9D9;",
            FluentGlyph = "&#xE9D9;",
        }, () =>
        {
            HostApi.NavigateTo(typeof(ApiProbePage), "Plugin API Probe");
            return Task.CompletedTask;
        });
    }
}
