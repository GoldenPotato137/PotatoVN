using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Views.Dialog;
using GalgameManager.WinApp.Base.Models.Msgs;
using ValveKeyValue;
using Windows.System;

namespace GalgameManager.Services;

public sealed class GameLaunchService(
    IGalgameCollectionService gameCollectionService,
    IGalgameSourceCollectionService sourceCollectionService,
    ILocalSettingsService localSettingsService,
    IJumpListService jumpListService,
    IBgTaskService bgTaskService,
    IInfoService infoService,
    IMessenger messenger)
    : IGameLaunchService
{
    private static readonly TimeSpan ProcessWaitTimeout = TimeSpan.FromSeconds(60); // 等待目标游戏进程出现的最长时间
    private readonly object _launchStateLock = new();
    private readonly HashSet<Guid> _launchesInProgress = [];
    private readonly GalgameCollectionService _gameService =
        (GalgameCollectionService)gameCollectionService; // 逻辑游戏持久化与可执行文件选择服务

    /// <inheritdoc />
    public async Task LaunchAsync(Galgame game, GalgameAndPath installation)
    {
        if (installation.Galgame != game || !installation.IsLocalInstallation)
            throw new ArgumentException("The installation does not belong to the game.", nameof(installation));

        if (!TryEnterLaunch(game.Uuid))
        {
            ReportDuplicateLaunch(game, "launch request already in progress");
            return;
        }

        try
        {
            await LaunchCoreAsync(game, installation);
        }
        finally
        {
            lock (_launchStateLock)
            {
                _launchesInProgress.Remove(game.Uuid);
            }
        }
    }

    private async Task LaunchCoreAsync(Galgame game, GalgameAndPath installation)
    {
        string gameKey = game.Uuid.ToString("D");
        if (bgTaskService.GetBgTask<RecordPlayTimeTask>(gameKey) is not null)
        {
            ReportDuplicateLaunch(game, "play-time task already active");
            return;
        }

        if (!Directory.Exists(installation.Path))
        {
            infoService.Info(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error,
                "MultiInstall_PathUnavailable".GetLocalized(installation.Path));
            return;
        }

        LocalInstallationConfig config = installation.LocalConfig ??= new LocalInstallationConfig();
        bool isSteam = installation.Source?.SourceType == GalgameSourceType.Steam;
        if (!string.IsNullOrEmpty(config.ExePath) && !File.Exists(config.ExePath))
            config.ExePath = null;

        if (isSteam && game.GetId(RssType.Steam) == -1)
        {
            game.Ids[(int)RssType.Steam] = await TryGetSteamIdAsync(installation);
            if (string.IsNullOrEmpty(game.Ids[(int)RssType.Steam]))
            {
                infoService.Info(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
                    msg: "GalgamePage_Play_NoSteamId".GetLocalized());
                return;
            }
        }

        if (!isSteam && string.IsNullOrEmpty(config.ExePath))
        {
            await _gameService.GetGalgameExeAsync(game, installation);
            if (string.IsNullOrEmpty(config.ExePath)) return;
        }

        GameRuntimeProcessRelay processRelay = new();
        Process? process = null;
        IReadOnlyCollection<int> preExistingProcessIds = [];
        GameLaunchWindowTracker? launchWindowTracker = null;
        try
        {
            if (isSteam)
            {
                preExistingProcessIds = GameProcessDetector.GetProcessIdsInDirectory(
                    GameProcessDetector.GetDirectoryPrefix(installation.Path));
                launchWindowTracker = StartLaunchWindowTracker(config, installation.Path, preExistingProcessIds);
                Uri steamUri = new($"steam://run/{game.Ids[(int)RssType.Steam]}");
                infoService.Info(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
                    msg: "GalgamePage_Play_StartingSteam".GetLocalized());
                if (!await Launcher.LaunchUriAsync(steamUri))
                {
                    infoService.Info(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error,
                        "GalgamePage_Play_SteamLaunchError".GetLocalized());
                    return;
                }

                if (string.IsNullOrEmpty(config.ProcessName))
                {
                    // 优先在游戏安装目录内自动探测游戏进程，失败再回退到手动选择
                    process = await GameProcessDetector.WaitForProcessInDirectoryAsync(installation.Path,
                        ProcessWaitTimeout);
                    if (process is null)
                    {
                        if (!await DisplaySteamMessageAsync()) return;
                        if (!await SelectProcessAsync(config)) return;
                        process = await WaitForProcessStartAsync(config.ProcessName!, preExistingProcessIds);
                    }
                }
                else
                    process = await WaitForProcessStartAsync(config.ProcessName, preExistingProcessIds);
            }
            else
            {
                string? executable = config.ExePath;
                string? arguments = config.ExeArguments;
                if (config.RunInLocaleEmulator)
                {
                    string? localeEmulatorPath =
                        await localSettingsService.ReadSettingAsync<string>(KeyValues.LocaleEmulatorPath);
                    if (string.IsNullOrEmpty(localeEmulatorPath) || !File.Exists(localeEmulatorPath))
                    {
                        infoService.Info(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
                            msg: "GalgamePage_InvalidLocaleEmulatorPath".GetLocalized());
                        return;
                    }
                    executable = localeEmulatorPath;
                    arguments = config.ExePath;
                }

                ProcessStartInfo startInfo = new()
                {
                    FileName = executable,
                    CreateNoWindow = !string.IsNullOrEmpty(arguments),
                    WorkingDirectory = installation.Path,
                    UseShellExecute = config.RunAsAdmin ||
                                      config.ExePath!.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase),
                    Verb = config.RunAsAdmin ? "runas" : null,
                };
                if (arguments is not null) startInfo.ArgumentList.Add(arguments);
                preExistingProcessIds = GameProcessDetector.GetProcessIdsInDirectory(
                    GameProcessDetector.GetDirectoryPrefix(installation.Path));
                launchWindowTracker = StartLaunchWindowTracker(config, installation.Path, preExistingProcessIds);
                process = new Process { StartInfo = startInfo };
                process.Start();

                if (!string.IsNullOrEmpty(config.ProcessName) &&
                    !ProcessMatchesConfiguredName(process, config.ProcessName))
                {
                    process = await WaitForProcessStartAsync(config.ProcessName, preExistingProcessIds) ?? process;
                }
                // 未指定进程名时直接跟踪启动的进程；若是启动器，
                // RecordPlayTimeTask会在其退出后探测安装目录内新出现的进程并重新附着
            }

            if (process is null)
            {
                infoService.Info(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
                    msg: "MultiInstall_ProcessNotFound".GetLocalized());
                return;
            }

            game.LastPlayTime = DateTime.Now;
            config.LastSuccessfulLaunchTime = DateTime.Now;
            game.SetPreferredInstallation(installation);
            if (installation.Source is not null) sourceCollectionService.Save(installation.Source);
            await _gameService.SaveGalgameAsync(game);

            GameWindowSnapshot? initialWindowSnapshot = GameProcessDetector.TryGetPrimaryWindowSnapshot(process);
            _ = bgTaskService.AddBgTask(new RecordPlayTimeTask(game, process, installation.EntryId,
                preExistingProcessIds, processRelay, initialWindowSnapshot, launchWindowTracker));
            // 计时任务接管启动窗口追踪器；启动服务提前结束时不能停止仍在等待接力的观察。
            launchWindowTracker = null;
            // 即使当前没有生效规则，也保留轻量运行时任务，
            // 确保游戏运行期间首次启用或新增映射时可以立即生效。
            _ = bgTaskService.AddBgTask(new KeyMappingTask(game, process, game.KeyMappings,
                preExistingProcessIds, processRelay));
            await jumpListService.AddToJumpListAsync(game);
            messenger.Send(new GalgamePlayedMessage(game));

            await Task.Delay(1000);
            if (await localSettingsService.ReadSettingAsync<bool>(KeyValues.AlwaysEnableMagpie) || game.EnableMagpie)
                _ = bgTaskService.AddBgTask(new CallMagpieTask(game, process, processRelay));
            if (await localSettingsService.ReadSettingAsync<bool>(KeyValues.AlwaysMuteInBackground) ||
                game.MuteInBackground)
                _ = bgTaskService.AddBgTask(new GameMuteTask(game, process, processRelay));
            if (await localSettingsService.ReadSettingAsync<bool>(KeyValues.AutoDetectSavePath) &&
                config.DetectedSavePath is null)
                _ = bgTaskService.AddBgTask(new GameSaveDetectorTask(game, installation));
            if (GameProcessDetector.IsAlive(process))
                App.SetWindowMode(
                    await localSettingsService.ReadSettingAsync<WindowMode>(KeyValues.PlayingWindowMode));

            await WaitForGameSessionEndAsync(process, processRelay);
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            infoService.Info(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning,
                "GalgamePage_Play_CancelledByUser".GetLocalized());
        }
        catch (Exception e)
        {
            infoService.Event(EventType.GalgameEvent, Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error,
                "GalgamePage_Play_Error".GetLocalized() + e.Message, e);
        }
        finally
        {
            launchWindowTracker?.Stop();
        }
    }

    private static GameLaunchWindowTracker? StartLaunchWindowTracker(LocalInstallationConfig config,
        string installationPath, IReadOnlyCollection<int> preExistingProcessIds)
    {
        if (!config.DelayPlayTimeUntilMainWindow) return null;
        GameLaunchWindowTracker tracker = new();
        tracker.Start(GameProcessDetector.GetDirectoryPrefix(installationPath), preExistingProcessIds);
        return tracker;
    }

    private bool TryEnterLaunch(Guid gameId)
    {
        lock (_launchStateLock)
        {
            return _launchesInProgress.Add(gameId);
        }
    }

    private void ReportDuplicateLaunch(Galgame game, string reason)
    {
        infoService.Info(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
            msg: "GalgamePage_Play_AlreadyRunning".GetLocalized(game.Name.Value ?? string.Empty));
        infoService.Log(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
            $"Game launch ignored: reason={reason}, gameUuid={game.Uuid:D}");
    }

    private static async Task WaitForGameSessionEndAsync(Process initialProcess,
        GameRuntimeProcessRelay processRelay)
    {
        Task initialProcessExit = GameProcessDetector.WaitForExitSafelyAsync(initialProcess);
        Task<Process?> gameplayProcessConfirmation = processRelay.WaitForConfirmationAsync();
        Task completed = await Task.WhenAny(initialProcessExit, gameplayProcessConfirmation);
        if (completed == initialProcessExit)
        {
            await initialProcessExit;
            return;
        }

        Process? gameplayProcess = await gameplayProcessConfirmation;
        if (gameplayProcess is null) return;
        if (GameProcessDetector.SafeGetId(gameplayProcess) == GameProcessDetector.SafeGetId(initialProcess))
        {
            await initialProcessExit;
            return;
        }
        await GameProcessDetector.WaitForExitSafelyAsync(gameplayProcess);
    }

    private static async Task<Process?> WaitForProcessStartAsync(string processName,
        IReadOnlyCollection<int>? excludedProcessIds = null)
    {
        HashSet<int> excluded = excludedProcessIds is null ? [] : new HashSet<int>(excludedProcessIds);
        string normalizedProcessName = Path.GetFileNameWithoutExtension(processName);
        DateTime deadline = DateTime.UtcNow + ProcessWaitTimeout;
        do
        {
            Process? process = Process.GetProcessesByName(normalizedProcessName)
                .FirstOrDefault(candidate => !excluded.Contains(GameProcessDetector.SafeGetId(candidate)));
            if (process is not null) return process;
            await Task.Delay(250);
        } while (DateTime.UtcNow < deadline);
        return null;
    }

    private static bool ProcessMatchesConfiguredName(Process process, string configuredProcessName)
    {
        try
        {
            return string.Equals(
                process.ProcessName,
                Path.GetFileNameWithoutExtension(configuredProcessName),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> SelectProcessAsync(LocalInstallationConfig config)
    {
        SelectProcessDialog dialog = new();
        await dialog.ShowAsync();
        if (dialog.SelectedProcessName is null) return false;
        config.ProcessName = dialog.SelectedProcessName;
        return true;
    }

    private async Task<string?> TryGetSteamIdAsync(GalgameAndPath installation)
    {
        try
        {
            DirectoryInfo? steamApps = new DirectoryInfo(installation.Path).Parent?.Parent;
            if (steamApps is null) return null;
            foreach (FileInfo file in steamApps.GetFiles("appmanifest_*.acf"))
            {
                await using FileStream stream = file.OpenRead();
                KVValue? value = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(stream).Value;
                if (value is null) continue;
                string name = value["name"].ToString(CultureInfo.InvariantCulture);
                if (installation.Path.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return value["appid"].ToString(CultureInfo.InvariantCulture);
            }
        }
        catch (Exception e)
        {
            infoService.DeveloperEvent(e: e);
        }
        return null;
    }

    private async Task<bool> DisplaySteamMessageAsync()
    {
        if (await localSettingsService.ReadSettingAsync<bool>(KeyValues.NotifiedSteamNeedManual)) return true;
        BasicDialog dialog = new("GalgamePage_Play_SteamDialog_Title".GetLocalized(),
            "GalgamePage_Play_SteamDialog_Message".GetLocalized(),
            checkBoxText: "GalgamePage_Play_SteamDialog_CheckBox".GetLocalized());
        await dialog.ShowAsync();
        if (!dialog.PrimaryButtonClicked) return false;
        await localSettingsService.SaveSettingAsync(KeyValues.NotifiedSteamNeedManual, dialog.CheckBoxChecked);
        return true;
    }
}
