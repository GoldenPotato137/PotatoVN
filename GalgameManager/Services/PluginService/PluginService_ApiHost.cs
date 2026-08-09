using CommunityToolkit.Mvvm.Messaging;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Models.Plugin;
using LiteDB;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public partial class PluginService
{
    public partial class PotatoVnApiHost(PluginX plugin) : IPotatoVnApi
    {
        private readonly ILiteCollection<PluginData> _pluginDataDb = App.GetService<ILocalSettingsService>()
            .Database.GetCollection<PluginData>("plugin_data");
        private readonly IInfoService _infoService = App.GetService<IInfoService>();
        private readonly IBgTaskService _bgTaskService = App.GetService<IBgTaskService>();
        private readonly IGalgameCollectionService _gameService = App.GetService<IGalgameCollectionService>();
        private readonly ILocalSettingsService _settingService = App.GetService<ILocalSettingsService>();
        private readonly ISidebarService _sidebarService = App.GetService<ISidebarService>();
        private readonly IGameLaunchService _gameLaunchService =
            App.GetService<IGameLaunchService>(); // 为插件按明确安装实例启动游戏

        #region GAMES

        public List<Galgame> GetAllGames() => _gameService.Galgames.ToList();

        public Galgame? GetGameByUuid(Guid? uuid) => _gameService.GetGalgameFromUuid(uuid);

        public Galgame? GetGameByUid(GalgameUid? uid, GalgameUidFetchMode mode = GalgameUidFetchMode.Same) =>
            _gameService.GetGalgameFromUid(uid, mode);

        public Galgame? GetGameById(string? id, RssType rssType) => _gameService.GetGalgameFromId(id, rssType);

        public Galgame? GetGameByName(string? name) => _gameService.GetGalgameFromName(name);

        /// <inheritdoc />
        public async Task<Galgame> AddGameInstallation(string path, bool force = true, bool requireConfirm = true)
        {
            Galgame? result = await _gameService.AddGameAsync(GalgameSourceType.LocalFolder, path, force, requireConfirm);
            return result ?? throw new InvalidOperationException("Failed to add game.");
        }

        /// <inheritdoc />
        public async Task<Galgame> AddVirtualGame(string name, bool force = true, bool requireConfirm = true)
        {
            Galgame? result = await _gameService.AddGameAsync(GalgameSourceType.Virtual, name, force, requireConfirm);
            return result ?? throw new InvalidOperationException("Failed to add virtual game.");
        }

        public async Task AddVirtualGameAsync(Galgame game)
        {
            ArgumentNullException.ThrowIfNull(game);
            if (_gameService.GetGalgameFromUuid(game.Uuid) is not null)
                throw new InvalidOperationException($"Game {game.Uuid} is already in the library.");
            await _gameService.AddVirtualGalgameAsync(game);
        }

        /// <inheritdoc />
        public IReadOnlyList<GameInstallationInfo> GetGameInstallations(Galgame game)
        {
            Galgame hostGame = ResolveGame(game);
            return hostGame.LocalInstallations.Select(ToInstallationInfo).ToList();
        }

        /// <inheritdoc />
        public async Task LaunchGameAsync(Galgame game, Guid? installationId = null)
        {
            game = ResolveGame(game);
            GalgameAndPath? installation = installationId is { } id
                ? game.LocalInstallations.FirstOrDefault(i => i.EntryId == id)
                : game.LocalInstallations.FirstOrDefault(i => i.EntryId == game.PreferredInstallationId);
            installation ??= game.LocalInstallations.Count == 1 ? game.LocalInstallations[0] : null;
            if (installation is null)
                throw new InvalidOperationException("No unambiguous local installation is available.");
            await UiThreadInvokeHelper.InvokeAsync(() => _gameLaunchService.LaunchAsync(game, installation));
        }

        public LocalInstallationConfig? GetGameInstallationConfiguration(Galgame game, Guid installationId) =>
            ResolveGame(game).LocalInstallations
                .FirstOrDefault(installation => installation.EntryId == installationId)?.LocalConfig?.Clone();

        public async Task UpdateGameInstallationAsync(Galgame game, Guid installationId,
            LocalInstallationConfig configuration, bool makePreferred = false)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            Galgame hostGame = ResolveGame(game);
            GalgameAndPath installation = FindInstallation(hostGame, installationId);
            installation.LocalConfig = configuration.Clone();
            if (makePreferred) hostGame.SetPreferredInstallation(installation);
            _sourceCollectionService.Save(installation.Source!);
            await _gameService.SaveGalgameAsync(hostGame);
        }

        public async Task SetPreferredGameInstallationAsync(Galgame game, Guid installationId)
        {
            Galgame hostGame = ResolveGame(game);
            GalgameAndPath installation = FindInstallation(hostGame, installationId);
            hostGame.SetPreferredInstallation(installation);
            await _gameService.SaveGalgameAsync(hostGame);
        }

        public Task RemoveGameInstallationAsync(Galgame game, Guid installationId, bool deleteFiles = false)
        {
            Galgame hostGame = ResolveGame(game);
            return _sourceCollectionService.MoveOutNoOperate(FindInstallation(hostGame, installationId), deleteFiles);
        }

        public Task SaveGameAsync(Galgame game) => _gameService.SaveGalgameAsync(ResolveGame(game));

        public Task SaveGameMetadataAsync(Galgame game, Guid? sourceId = null)
        {
            Galgame hostGame = ResolveGame(game);
            GalgameSourceBase? source = sourceId is { } id
                ? _sourceCollectionService.GetGalgameSourceFromId(id)
                    ?? throw new KeyNotFoundException($"Game source {id} was not found.")
                : null;
            return _gameService.SaveGalgameMetaAsync(hostGame, source);
        }

        public Task RemoveGameAsync(Galgame game, bool removeFromDisk = false) =>
            _gameService.RemoveGalgame(ResolveGame(game), removeFromDisk);

        public Task<Galgame> ParseGameAsync(Galgame game, RssType rssType = RssType.None,
            bool requireConfirm = false, GameParseType type = GameParseType.All) =>
            _gameService.ParseGalInfoAsync(ResolveGame(game), rssType, requireConfirm, type);

        public Task<Galgame> ParseGameInfoOnlyAsync(Galgame game, RssType rssType = RssType.None,
            bool requireConfirm = false) => _gameService.ParseGalInfoOnlyAsync(game, rssType, requireConfirm);

        public Task<GalgameCharacter> ParseGameCharacterAsync(GalgameCharacter character,
            RssType rssType = RssType.None) => _gameService.PhraseGalCharacterAsync(character, rssType);

        public Task<List<string>> ParseGameImagesAsync(Galgame game, GameParseType type) =>
            _gameService.ParserGalImagesAsync(game, type);

        private Galgame ResolveGame(Galgame game)
        {
            ArgumentNullException.ThrowIfNull(game);
            return _gameService.GetGalgameFromUuid(game.Uuid)
                   ?? throw new KeyNotFoundException($"Game {game.Uuid} was not found.");
        }

        private static GalgameAndPath FindInstallation(Galgame game, Guid installationId) =>
            game.LocalInstallations.FirstOrDefault(installation => installation.EntryId == installationId)
            ?? throw new KeyNotFoundException($"Game installation {installationId} was not found.");

        private static GameInstallationInfo ToInstallationInfo(GalgameAndPath installation) => new(
            installation.EntryId,
            installation.Source?.Id ?? Guid.Empty,
            installation.Source?.SourceType ?? GalgameSourceType.UnKnown,
            installation.Source?.Name ?? string.Empty,
            installation.Path,
            installation.IsPreferred,
            Directory.Exists(installation.Path));

        #endregion

        #region DATA

        public Task<string?> GetDataAsync()
        {
            //Task包一层，防止调用方直接在UI线程调用
            return Task.Run(() =>
            {
                PluginData? data = _pluginDataDb.FindById(plugin.Info.Id);
                return data?.Data;
            });
        }

        public async Task SaveDataAsync(string data)
        {
            //Task包一层，防止调用方直接在UI线程调用
            await Task.Run(() =>
            {
                PluginData? existing = _pluginDataDb.FindById(plugin.Info.Id);
                if (existing == null)
                {
                    _pluginDataDb.Insert(new PluginData
                    {
                        PluginId = plugin.Info.Id,
                        Data = data,
                    });
                }
                else
                {
                    existing.Data = data;
                    _pluginDataDb.Update(existing);
                }
            });
        }

        #endregion

        #region MESSAGES

        public IMessenger Messenger => App.GetService<IMessenger>();

        #endregion

        #region NOTIFICATION

        public void Info(InfoBarSeverity infoBarSeverity, string? title = null, string? msg = null, int? displayTimeMs = 3000)
            => _infoService.Info(infoBarSeverity, title, msg, displayTimeMs);

        public void Event(InfoBarSeverity infoBarSeverity, string title, Exception? exception = null, string? msg = null,
            Action? callbackAction = null, string? callbackButtonText = null) =>
            _infoService.Event(EventType.PluginEvent ,infoBarSeverity, title, exception, msg, callbackAction, callbackButtonText);

        public void DeveloperEvent(InfoBarSeverity infoBarSeverity = InfoBarSeverity.Warning, string? msg = null, Exception? e = null)
            => _infoService.DeveloperEvent(infoBarSeverity, msg, e);

        public void Log(InfoBarSeverity severity = InfoBarSeverity.Warning, string msg = "") =>
            _infoService.Log(severity, msg);

        #endregion

        #region BG_TASKS

        public Task AddBgTask(BgTaskBase bgTask) => _bgTaskService.AddBgTask(bgTask);

        public IEnumerable<BgTaskBase> GetBgTasks() => _bgTaskService.GetBgTasks();

        public T? GetBgTask<T>(string key) where T : BgTaskBase => _bgTaskService.GetBgTask<T>(key);

        public object? ActivationArgs => ActivationService.ActivationArgs;

        public LanguageEnum Language => _settingService.ReadSettingAsync<LanguageEnum>(KeyValues.Language).Result;

        #endregion

        #region SIDEBAR

        public void RegisterSidebarButton(SidebarButtonInfo button, Func<Task> onClick)
            => _sidebarService.RegisterPluginButton(plugin.Info.Id, plugin.Info.Name, button, onClick);

        public void UnregisterSidebarButton(string buttonId)
            => _sidebarService.UnregisterPluginButton(plugin.Info.Id, buttonId);

        #endregion

        #region UTILS

        public async Task<string?> DownloadImageAsync(string imageUrl, string imageName, HttpClient? client,
            Action<Exception>? onException = null)
        {
            StorageFolder imgFolder = await FileHelper.GetFolderAsync(FileHelper.FolderType.Images);
            DirectoryInfo pluginImgDir = new(Path.Combine(imgFolder.Path, plugin.Info.Id.ToString()));
            return await DownloadHelper.DownloadAndSaveImageWithDiffThread(imageUrl,
                fileNameWithoutExtension: imageName, onException: onException, client: client,
                targetFolder: pluginImgDir);
        }

        public string GetPluginPath()
        {
            if (plugin.Plugin is null) return plugin.Path;
            try
            {
                //对于热重载插件，其目录是一个临时目录
                return PluginXamlHost.GetRuntimePath(plugin.Plugin.GetType().Assembly);
            }
            catch
            {
                return plugin.Path;
            }
        }

        public void InvokeOnMainThread(Action action) => UiThreadInvokeHelper.Invoke(action);

        public Task InvokeOnMainThreadAsync(Func<Task> action) => UiThreadInvokeHelper.InvokeAsync(action);

        #endregion

        #region OBSOLETE_APIS

        /// <inheritdoc />
        [Obsolete($"请使用{nameof(AddGameInstallation)}")]
        public Task<Galgame> AddGame(string path, bool force = true, bool requireConfirm = true) =>
            AddGameInstallation(path, force, requireConfirm);

        #endregion
    }
}
