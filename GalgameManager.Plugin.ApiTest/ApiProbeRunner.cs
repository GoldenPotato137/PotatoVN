using System.Net;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Contracts.NavigationApi;
using GalgameManager.WinApp.Base.Models.Filters;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;

namespace GalgameManager.Plugin.ApiTest;

internal sealed class ApiProbeRunner(IPotatoVnApi api)
{
    private readonly string _runRoot = Path.Combine(Path.GetTempPath(), "PotatoVN.PluginApiProbe", Guid.NewGuid().ToString("N"));
    private readonly List<Galgame> _games = [];
    private readonly List<GalgameSourceBase> _sources = [];
    private CategoryGroup? _categoryGroup;
    private Category? _category;

    internal async Task RunAsync()
    {
        ProbeState.Reset();
        Directory.CreateDirectory(_runRoot);
        try
        {
            await ProbeContractAsync();
            await ProbeGamesAsync();
            await ProbeSourcesAndInstallationsAsync();
            await ProbeCategoriesAsync();
            await ProbeFiltersAsync();
            await ProbeStaffAsync();
            await ProbeDataAndMessagingAsync();
            await ProbeNotificationsAsync();
            await ProbeBackgroundTasksAsync();
            await ProbeHostAndUtilitiesAsync();
            await ProbeNavigationAndSidebarAsync();
        }
        finally
        {
            await CleanupAsync();
            ProbeState.Complete();
        }
    }

    private Task ProbeContractAsync() => ProbeAsync("IPotatoVnApi contract", () =>
    {
        Require(typeof(IPotatoVnApi).GetMethods().Length >= 80, "expanded API surface is incomplete");
        return Task.CompletedTask;
    });

    private async Task ProbeGamesAsync()
    {
        await ProbeAsync("AddVirtualGame(string)", async () =>
        {
            Galgame game = await api.AddVirtualGame(UniqueName("Virtual"), force: true, requireConfirm: false);
            _games.Add(game);
        });

        await ProbeAsync("AddVirtualGameAsync(Galgame)", async () =>
        {
            Galgame game = new(UniqueName("VirtualObject")) { RssType = ProbeParser.RssType };
            await api.AddVirtualGameAsync(game);
            _games.Add(game);
        });

        await ProbeAsync("AddGameInstallation", async () =>
        {
            string gamePath = CreateGameDirectory("InstalledGame");
            Galgame game = await api.AddGameInstallation(gamePath, force: true, requireConfirm: false);
            _games.Add(game);
        });

        await ProbeAsync("AddGame (obsolete)", async () =>
        {
            string gamePath = CreateGameDirectory("LegacyInstalledGame");
#pragma warning disable CS0618
            Galgame game = await api.AddGame(gamePath, force: true, requireConfirm: false);
#pragma warning restore CS0618
            _games.Add(game);
        });

        await ProbeAsync("GetAllGames", () =>
        {
            Require(api.GetAllGames().Count >= _games.Count, "created games are missing");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetGameByUuid", () =>
        {
            Require(ReferenceEquals(api.GetGameByUuid(PrimaryGame.Uuid), PrimaryGame), "UUID lookup mismatch");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetGameByUid", () =>
        {
            Require(api.GetGameByUid(PrimaryGame.Uid)?.Uuid == PrimaryGame.Uuid, "UID lookup mismatch");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetGameById", async () =>
        {
            PrimaryGame.Ids[(int)RssType.Bangumi] = "plugin-api-probe-bgm";
            await api.SaveGameAsync(PrimaryGame);
            Require(api.GetGameById("plugin-api-probe-bgm", RssType.Bangumi)?.Uuid == PrimaryGame.Uuid,
                "external ID lookup mismatch");
        });
        await ProbeAsync("GetGameByName", () =>
        {
            Require(api.GetGameByName(PrimaryGame.Name.Value)?.Uuid == PrimaryGame.Uuid, "name lookup mismatch");
            return Task.CompletedTask;
        });
        await ProbeAsync("SaveGameAsync", async () =>
        {
            PrimaryGame.Comment = "Saved by Plugin API Probe";
            await api.SaveGameAsync(PrimaryGame);
        });
        await ProbeAsync("SaveGameMetadataAsync", () => api.SaveGameMetadataAsync(PrimaryGame));
        await ProbeAsync("ParseGameInfoOnlyAsync", async () =>
        {
            Galgame parsed = await api.ParseGameInfoOnlyAsync(new Galgame("Probe Parse Only"), ProbeParser.RssType);
            Require(parsed.RssType == ProbeParser.RssType, "custom parser was not used");
        });
        await ProbeAsync("ParseGameAsync", async () =>
        {
            Galgame parsed = await api.ParseGameAsync(PrimaryGame, ProbeParser.RssType,
                requireConfirm: false, GameParseType.GameInfo);
            Require(parsed.RssType == ProbeParser.RssType, "full parser did not return custom RSS data");
        });
        await ProbeAsync("ParseGameCharacterAsync", async () =>
        {
            GalgameCharacter character = await api.ParseGameCharacterAsync(
                new GalgameCharacter { Name = "Probe Character" }, ProbeParser.RssType);
            Require(character.Summary == "Parsed by Plugin API Probe", "character parser result mismatch");
        });
        await ProbeAsync("ParseGameImagesAsync", async () =>
        {
            List<string> covers = await api.ParseGameImagesAsync(PrimaryGame, GameParseType.Image);
            List<string> headers = await api.ParseGameImagesAsync(PrimaryGame, GameParseType.HeaderImage);
            Require(covers.Contains("https://probe.invalid/cover.png") &&
                    headers.Contains("https://probe.invalid/header.png"),
                "image parser result mismatch");
        });
        await ProbeAsync("RemoveGameAsync", async () =>
        {
            Galgame target = _games[1];
            await api.RemoveGameAsync(target);
            Require(api.GetGameByUuid(target.Uuid) is null, "game still exists after removal");
            _games.Remove(target);
        });
    }

    private async Task ProbeSourcesAndInstallationsAsync()
    {
        string sourceRoot = CreateDirectory("SourceA");
        string sourceRoot2 = CreateDirectory("SourceB");
        string linkedGamePath = Path.Combine(sourceRoot, "LinkedGame");
        Directory.CreateDirectory(linkedGamePath);
        await File.WriteAllTextAsync(Path.Combine(linkedGamePath, "probe.txt"), "probe");

        GalgameSourceBase? source = null;
        GalgameSourceBase? source2 = null;
        await ProbeAsync("AddSourceAsync", async () =>
        {
            source = await api.AddSourceAsync(GalgameSourceType.LocalFolder, sourceRoot, scan: false);
            _sources.Add(source);
        });
        await ProbeAsync("GetAllSources", () =>
        {
            Require(api.GetAllSources().Any(item => item.Id == source!.Id), "source snapshot missing item");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetSourceById", () =>
        {
            Require(api.GetSourceById(source!.Id)?.Id == source.Id, "source ID lookup mismatch");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetSourceByUrl", () =>
        {
            Require(api.GetSourceByUrl(source!.Url)?.Id == source.Id, "source URL lookup mismatch");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetSource", () =>
        {
            Require(api.GetSource(GalgameSourceType.LocalFolder, sourceRoot)?.Id == source!.Id,
                "source type/path lookup mismatch");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetSourcePath", () =>
        {
            Require(Path.GetFullPath(api.GetSourcePath(GalgameSourceType.LocalFolder, linkedGamePath)) ==
                    Path.GetFullPath(sourceRoot), "source root calculation mismatch");
            return Task.CompletedTask;
        });
        await ProbeAsync("SaveSource", () =>
        {
            source!.Name = "Plugin API Probe Source A";
            api.SaveSource(source);
            return Task.CompletedTask;
        });
        await ProbeAsync("AddGameToSource", () =>
        {
            var info = api.AddGameToSource(source!, PrimaryGame, linkedGamePath,
                new LocalInstallationConfig { ExePath = Path.Combine(Environment.SystemDirectory, "cmd.exe") });
            Require(info is not null, "source entry was not created");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetGameInstallations", () =>
        {
            Require(api.GetGameInstallations(PrimaryGame).Any(item => item.SourceId == source!.Id),
                "installation snapshot missing source entry");
            return Task.CompletedTask;
        });

        Guid installationId = api.GetGameInstallations(PrimaryGame).Single(item => item.SourceId == source!.Id).EntryId;
        await ProbeAsync("GetGameInstallationConfiguration", () =>
        {
            LocalInstallationConfig config = api.GetGameInstallationConfiguration(PrimaryGame, installationId)
                ?? throw new InvalidOperationException("installation configuration is null");
            config.ProcessName = "must-not-leak";
            Require(api.GetGameInstallationConfiguration(PrimaryGame, installationId)?.ProcessName is null,
                "configuration read did not return a clone");
            return Task.CompletedTask;
        });
        await ProbeAsync("UpdateGameInstallationAsync", async () =>
        {
            await api.UpdateGameInstallationAsync(PrimaryGame, installationId, new LocalInstallationConfig
            {
                ExePath = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                ExeArguments = "/c",
                HighDpi = true,
            });
            Require(api.GetGameInstallationConfiguration(PrimaryGame, installationId)?.HighDpi == true,
                "installation configuration was not persisted");
        });
        await ProbeAsync("SetPreferredGameInstallationAsync", async () =>
        {
            await api.SetPreferredGameInstallationAsync(PrimaryGame, installationId);
            Require(api.GetGameInstallations(PrimaryGame).Single(item => item.EntryId == installationId).IsPreferred,
                "preferred installation was not updated");
        });
        await ProbeAsync("LaunchGameAsync", async () =>
        {
            await api.LaunchGameAsync(PrimaryGame, installationId);
            Require(api.GetGameInstallationConfiguration(PrimaryGame, installationId)?.LastSuccessfulLaunchTime >
                    DateTime.MinValue, "launch did not update installation state");
        });

        await ProbeAsync("MoveGame", async () =>
        {
            source2 = await api.AddSourceAsync(GalgameSourceType.LocalFolder, sourceRoot2, scan: false);
            _sources.Add(source2);
            string targetPath = Path.Combine(sourceRoot2, "LinkedGame");
            BgTaskBase moveTask = api.MoveGame(PrimaryGame, source2.Id, targetPath, source!.Id);
            await moveTask.Task;
            Require(!moveTask.Task.IsFaulted && Directory.Exists(targetPath), "physical move failed");
        });

        Guid movedInstallationId = api.GetGameInstallations(PrimaryGame)
            .Single(item => item.SourceId == source2!.Id).EntryId;
        string movedPath = api.GetGameInstallations(PrimaryGame)
            .Single(item => item.EntryId == movedInstallationId).Path;
        await ProbeAsync("SaveGameMetadataAsync(source)", async () =>
        {
            source2!.SaveMetaBackup = true;
            api.SaveSource(source2);
            await api.SaveGameMetadataAsync(PrimaryGame, source2.Id);
            await WaitUntilAsync(() => File.Exists(Path.Combine(movedPath, ".PotatoVN", "meta.json")),
                "meta backup was not written");
        });
        await ProbeAsync("RemoveGameInstallationAsync", async () =>
        {
            await api.RemoveGameInstallationAsync(PrimaryGame, movedInstallationId);
            Require(Directory.Exists(movedPath), "default installation removal deleted files");
        });

        await ProbeAsync("ScanSource", async () =>
        {
            api.ScanSource(source2!);
            await Task.Delay(200);
        });
        await ProbeAsync("ScanAllSources", async () =>
        {
            api.ScanAllSources();
            await Task.Delay(200);
        });

        await ProbeAsync("DeleteSourceAsync(source, removeGames)", async () =>
        {
            GalgameSourceBase sourceToDelete = source2 ?? throw new InvalidOperationException("source is null");
            await api.DeleteSourceAsync(sourceToDelete, removeGames: false);
            Require(api.GetSourceById(sourceToDelete.Id) is null, "source still exists");
            _sources.Remove(sourceToDelete);
        });

        GalgameSourceBase confirmSource = await api.AddSourceAsync(GalgameSourceType.LocalFolder,
            CreateDirectory("ConfirmedDeleteSource"), scan: false);
        _sources.Add(confirmSource);
        await ProbeAsync("DeleteSourceAsync(source)", async () =>
        {
            ProbeState.SetStatus("Awaiting DeleteSourceAsync confirmation");
            await api.DeleteSourceAsync(confirmSource);
            Require(api.GetSourceById(confirmSource.Id) is null, "confirmed source deletion failed");
            _sources.Remove(confirmSource);
        });
    }

    private async Task ProbeCategoriesAsync()
    {
        await ProbeAsync("GetCategoryGroupsAsync", async () =>
        {
            Require((await api.GetCategoryGroupsAsync()).Count >= 3, "system category groups are missing");
        });
        await ProbeAsync("StatusCategoryGroup", () => RequireNotNullAsync(api.StatusCategoryGroup));
        await ProbeAsync("DeveloperCategoryGroup", () => RequireNotNullAsync(api.DeveloperCategoryGroup));
        await ProbeAsync("EngineCategoryGroup", () => RequireNotNullAsync(api.EngineCategoryGroup));
        await ProbeAsync("GetCategoryGroup", () =>
        {
            Require(api.GetCategoryGroup(api.StatusCategoryGroup.Id) is not null, "category group lookup failed");
            return Task.CompletedTask;
        });
        await ProbeAsync("AddCategoryGroup", () =>
        {
            _categoryGroup = api.AddCategoryGroup(UniqueName("CategoryGroup"));
            return Task.CompletedTask;
        });
        await ProbeAsync("AddCategoryToGroup", () =>
        {
            _category = new Category(UniqueName("Category"));
            api.AddCategoryToGroup(_categoryGroup!, _category);
            return Task.CompletedTask;
        });
        await ProbeAsync("SaveCategory", () =>
        {
            _category!.Add(PrimaryGame);
            api.SaveCategory(_category);
            return Task.CompletedTask;
        });
        await ProbeAsync("SaveCategoryGroup", () =>
        {
            api.SaveCategoryGroup(_categoryGroup!);
            return Task.CompletedTask;
        });
        await ProbeAsync("GetCategory(Guid)", () =>
        {
            Require(api.GetCategory(_category!.Id)?.Id == _category.Id, "category ID lookup failed");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetCategory(string)", () =>
        {
            Require(api.GetCategory(_category!.Name)?.Id == _category.Id, "category name lookup failed");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetDeveloperCategory", () =>
        {
            _ = api.GetDeveloperCategory(PrimaryGame);
            return Task.CompletedTask;
        });
        await ProbeAsync("GetEngineCategory", () =>
        {
            _ = api.GetEngineCategory(PrimaryGame);
            return Task.CompletedTask;
        });
        await ProbeAsync("RefreshGameCategoriesAsync", () => api.RefreshGameCategoriesAsync());
        await ProbeAsync("RefreshCategory", () =>
        {
            api.RefreshCategory(_category!);
            return Task.CompletedTask;
        });
        await ProbeAsync("RemoveCategoryFromGroup", () =>
        {
            CategoryGroup group = _categoryGroup ?? throw new InvalidOperationException("category group is null");
            Category category = _category ?? throw new InvalidOperationException("category is null");
            api.RemoveCategoryFromGroup(group, category);
            Require(!group.Categories.Contains(category), "category was not removed from group");
            api.AddCategoryToGroup(group, category);
            return Task.CompletedTask;
        });
        await ProbeAsync("MergeCategories", () =>
        {
            Category source = new(UniqueName("MergeSource"));
            source.Add(PrimaryGame);
            api.AddCategoryToGroup(_categoryGroup!, source);
            api.MergeCategories(_category!, source);
            Require(api.GetCategory(source.Id) is null, "source category still exists after merge");
            return Task.CompletedTask;
        });
        await ProbeAsync("DeleteCategory", () =>
        {
            Category category = _category ?? throw new InvalidOperationException("category is null");
            api.DeleteCategory(category);
            Require(api.GetCategory(category.Id) is null, "category still exists after deletion");
            _category = null;
            return Task.CompletedTask;
        });
        await ProbeAsync("DeleteCategoryGroup", () =>
        {
            CategoryGroup group = _categoryGroup ?? throw new InvalidOperationException("category group is null");
            api.DeleteCategoryGroup(group);
            Require(api.GetCategoryGroup(group.Id) is null, "category group still exists after deletion");
            _categoryGroup = null;
            return Task.CompletedTask;
        });
    }

    private async Task ProbeFiltersAsync()
    {
        ProbeFilter filter = new(PrimaryGame.Name.Value ?? string.Empty);
        await ProbeAsync("AddFilter", async () =>
        {
            api.AddFilter(filter);
            await Task.Delay(100);
            Require((await api.GetFiltersAsync()).Any(item => item.Name == filter.Name), "filter was not added");
        });
        await ProbeAsync("GetFiltersAsync", async () =>
        {
            Require((await api.GetFiltersAsync()).Any(item => item.Name == filter.Name), "filter snapshot mismatch");
        });
        await ProbeAsync("ApplyFilters", () =>
        {
            Require(api.ApplyFilters(PrimaryGame), "matching game was filtered out");
            return Task.CompletedTask;
        });
        await ProbeAsync("SetFilter", async () =>
        {
            filter.Revert = true;
            api.SetFilter(filter);
            await Task.Delay(100);
            Require(!api.ApplyFilters(PrimaryGame), "updated filter state was not applied");
        });
        await ProbeAsync("SearchFiltersAsync", async () =>
        {
            Require(await api.SearchFiltersAsync("Probe") is not null, "filter search returned null");
        });
        await ProbeAsync("DeleteFilter", async () =>
        {
            api.DeleteFilter(filter);
            await Task.Delay(100);
            Require((await api.GetFiltersAsync()).All(item => item.Name != filter.Name), "filter was not deleted");
        });
        await ProbeAsync("ClearFilters", async () =>
        {
            api.AddFilter(new ProbeFilter("never-match"));
            await Task.Delay(100);
            api.ClearFilters();
            await Task.Delay(100);
            Require((await api.GetFiltersAsync()).All(item => item is not ProbeFilter), "custom filters remain");
        });
    }

    private async Task ProbeStaffAsync()
    {
        Staff staff = new() { EnglishName = "Plugin API Probe Staff" };
        staff.AddGame(PrimaryGame, []);
        await ProbeAsync("SaveStaff", () =>
        {
            api.SaveStaff(staff, sync: false);
            return Task.CompletedTask;
        });
        await ProbeAsync("GetStaff(Guid)", () =>
        {
            Require(api.GetStaff(staff.Id)?.Id == staff.Id, "staff ID lookup failed");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetStaff(StaffIdentifier)", () =>
        {
            Require(api.GetStaff(staff.GetIdentifier())?.Id == staff.Id, "staff identifier lookup failed");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetStaffs()", () =>
        {
            Require(api.GetStaffs().Any(item => item.Id == staff.Id), "staff snapshot missing item");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetStaffs(Galgame)", () =>
        {
            Require(api.GetStaffs(PrimaryGame).Any(item => item.Id == staff.Id), "game staff lookup failed");
            return Task.CompletedTask;
        });
        await ProbeAsync("ParseStaffAsync", async () =>
        {
            Staff parsed = await api.ParseStaffAsync(staff, ProbeParser.RssType);
            Require(parsed.JapaneseName == "Probe Parsed Staff", "staff parser result mismatch");
        });
        await ProbeAsync("ParseGameStaffAsync", () => api.ParseGameStaffAsync(PrimaryGame));
        await ProbeAsync("DeleteStaff", () =>
        {
            api.DeleteStaff(staff, sync: false);
            Require(api.GetStaff(staff.Id) is null, "staff still exists after deletion");
            return Task.CompletedTask;
        });
    }

    private async Task ProbeDataAndMessagingAsync()
    {
        await ProbeAsync("GetDataAsync", async () => _ = await api.GetDataAsync());
        await ProbeAsync("SaveDataAsync", async () =>
        {
            await api.SaveDataAsync("{\"probe\":true}");
            Require(await api.GetDataAsync() == "{\"probe\":true}", "plugin data round-trip failed");
        });
        await ProbeAsync("Messenger", () =>
        {
            Require(api.Messenger is not null, "messenger is null");
            return Task.CompletedTask;
        });
    }

    private async Task ProbeNotificationsAsync()
    {
        await ProbeAsync("Info", () =>
        {
            api.Info(InfoBarSeverity.Informational, "Plugin API Probe", "Info API reached");
            return Task.CompletedTask;
        });
        await ProbeAsync("Event", () =>
        {
            api.Event(InfoBarSeverity.Informational, "Plugin API Probe Event", msg: "Event API reached");
            return Task.CompletedTask;
        });
        await ProbeAsync("DeveloperEvent", () =>
        {
            api.DeveloperEvent(msg: "Plugin API Probe developer event");
            return Task.CompletedTask;
        });
        await ProbeAsync("Log", () =>
        {
            api.Log(InfoBarSeverity.Informational, "Plugin API Probe log");
            return Task.CompletedTask;
        });
    }

    private async Task ProbeBackgroundTasksAsync()
    {
        ProbeBgTask task = new();
        Task completion = Task.CompletedTask;
        await ProbeAsync("AddBgTask", async () =>
        {
            completion = api.AddBgTask(task);
            await task.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        });
        await ProbeAsync("GetBgTasks", () =>
        {
            Require(api.GetBgTasks().Contains(task), "running background task missing from snapshot");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetBgTask<T>", () =>
        {
            Require(ReferenceEquals(api.GetBgTask<ProbeBgTask>(ProbeBgTask.SearchKey), task),
                "generic background task lookup failed");
            return Task.CompletedTask;
        });
        task.Complete();
        await completion;
    }

    private async Task ProbeHostAndUtilitiesAsync()
    {
        await ProbeAsync("ActivationArgs", () =>
        {
            _ = api.ActivationArgs;
            return Task.CompletedTask;
        });
        await ProbeAsync("Language", () =>
        {
            _ = api.Language;
            return Task.CompletedTask;
        });
        await ProbeAsync("GetMainWindow", () =>
        {
            Require(api.GetMainWindow() is not null, "main window is null");
            return Task.CompletedTask;
        });
        await ProbeAsync("GetPluginPath", () =>
        {
            Require(Directory.Exists(api.GetPluginPath()), "plugin path does not exist");
            return Task.CompletedTask;
        });
        await ProbeAsync("InvokeOnMainThread", async () =>
        {
            var invoked = false;
            api.InvokeOnMainThread(() => invoked = true);
            await WaitUntilAsync(() => invoked, "main-thread action was not invoked");
        });
        await ProbeAsync("InvokeOnMainThreadAsync", async () =>
        {
            var invoked = false;
            await api.InvokeOnMainThreadAsync(async () =>
            {
                await Task.Yield();
                invoked = true;
            });
            Require(invoked, "async main-thread action was not invoked");
        });
        await ProbeAsync("DownloadImageAsync", async () =>
        {
            using HttpClient client = new(new ProbeImageHandler());
            string? path = await api.DownloadImageAsync("https://probe.invalid/image", "api-probe", client);
            Require(path is not null && File.Exists(path), "image download did not create a file");
        });
    }

    private async Task ProbeNavigationAndSidebarAsync()
    {
        await ProbeAsync("UnregisterSidebarButton", async () =>
        {
            api.UnregisterSidebarButton(ApiProbePlugin.SidebarButtonId);
            await Task.Delay(100);
        });
        await ProbeAsync("RegisterSidebarButton", async () =>
        {
            ApiProbePlugin.RegisterSidebarButton();
            await Task.Delay(100);
        });
        await ProbeAsync("NavigateTo(PageEnum)", async () =>
        {
            api.NavigateTo(PageEnum.HomePage);
            await Task.Delay(250);
        });
        await ProbeAsync("NavigateTo(Type)", async () =>
        {
            api.NavigateTo(typeof(ApiProbePage), "Plugin API Probe");
            await Task.Delay(250);
        });
    }

    private async Task CleanupAsync()
    {
        try
        {
            if (_category is not null) api.DeleteCategory(_category);
            if (_categoryGroup is not null) api.DeleteCategoryGroup(_categoryGroup);
            foreach (Galgame game in _games.ToList())
            {
                try
                {
                    if (api.GetGameByUuid(game.Uuid) is not null) await api.RemoveGameAsync(game);
                }
                catch
                {
                    // Preserve the probe result; the isolated test directory is discarded after process exit.
                }
            }
            foreach (GalgameSourceBase source in _sources.ToList())
            {
                try
                {
                    if (api.GetSourceById(source.Id) is not null) await api.DeleteSourceAsync(source, false);
                }
                catch
                {
                    // Preserve the probe result; the isolated test directory is discarded after process exit.
                }
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(_runRoot)) Directory.Delete(_runRoot, recursive: true);
            }
            catch
            {
                // The host may still be releasing a short-lived background task.
            }
        }
    }

    private Galgame PrimaryGame => _games[0];

    private string UniqueName(string prefix) => $"{prefix}-{Path.GetFileName(_runRoot)}";

    private string CreateDirectory(string name)
    {
        string path = Path.Combine(_runRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private string CreateGameDirectory(string name)
    {
        string root = CreateDirectory($"{name}Source");
        string path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "probe.txt"), "Plugin API Probe");
        return path;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, string message)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (predicate()) return;
            await Task.Delay(100);
        }
        throw new InvalidOperationException(message);
    }

    private static Task RequireNotNullAsync(object? value)
    {
        Require(value is not null, "value is null");
        return Task.CompletedTask;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static async Task ProbeAsync(string apiName, Func<Task> action)
    {
        ProbeState.SetStatus($"Running: {apiName}");
        try
        {
            await action();
            ProbeState.Add(new ProbeResult(apiName, true, "OK"));
        }
        catch (Exception exception)
        {
            ProbeState.Add(new ProbeResult(apiName, false,
                $"{exception.GetType().Name}: {exception.Message}"));
        }
    }
}

internal sealed class ProbeFilter(string query) : FilterBase
{
    public override bool Apply(Galgame game) =>
        game.Name.Value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

    public override string Name => $"PluginApiProbe:{query}";
    protected override string SuggestName => Name;
}

internal sealed class ProbeBgTask : BgTaskBase
{
    internal const string SearchKey = "plugin-api-probe";
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal TaskCompletionSource Started => _started;
    public override string Title => "Plugin API Probe background task";

    protected override Task RecoverFromJsonInternal() => Task.CompletedTask;

    protected override async Task RunInternal()
    {
        ChangeProgress(0, 1, "Running", notifyWhenSuccess: false);
        _started.TrySetResult();
        await _release.Task;
        ChangeProgress(1, 1, "Completed", notifyWhenSuccess: false);
    }

    public override bool OnSearch(string key) => key == SearchKey;
    internal void Complete() => _release.TrySetResult();
}

internal sealed class ProbeImageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
    });
}
