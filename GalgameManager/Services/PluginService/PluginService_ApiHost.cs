using CommunityToolkit.Mvvm.Messaging;
using Windows.Storage;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.WinApp.Base.Contracts;
using LiteDB;
using Microsoft.UI.Xaml.Controls;

namespace GalgameManager.Services;

public partial class PluginService
{
    public class PotatoVnApiHost(PluginX plugin) : IPotatoVnApi
    {
        private readonly ILiteCollection<PluginData> _pluginDataDb = App.GetService<ILocalSettingsService>()
            .Database.GetCollection<PluginData>("plugin_data");
        private readonly IInfoService _infoService = App.GetService<IInfoService>();
        private readonly IBgTaskService _bgTaskService = App.GetService<IBgTaskService>();
        private readonly IGalgameCollectionService _gameService = App.GetService<IGalgameCollectionService>();

        #region GAMES

        public List<Galgame> GetAllGames() => _gameService.Galgames.ToList();

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
            _infoService.Event(EventType.PluginEvent, infoBarSeverity, title, exception, msg, callbackAction, callbackButtonText);

        public void DeveloperEvent(InfoBarSeverity infoBarSeverity = InfoBarSeverity.Warning, string? msg = null, Exception? e = null)
            => _infoService.DeveloperEvent(infoBarSeverity, msg, e);

        public void Log(InfoBarSeverity severity = InfoBarSeverity.Warning, string msg = "") =>
            _infoService.Log(severity, msg);

        #endregion

        #region BG_TASKS

        public Task AddBgTask(BgTaskBase bgTask) => _bgTaskService.AddBgTask(bgTask);

        public IEnumerable<BgTaskBase> GetBgTasks() => _bgTaskService.GetBgTasks();

        public T? GetBgTask<T>(string key) where T : BgTaskBase => _bgTaskService.GetBgTask<T>(key);

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

        public string GetPluginPath() => plugin.Path;

        public void InvokeOnMainThread(Action action) => UiThreadInvokeHelper.Invoke(action);

        #endregion

        #region LLM

        private readonly LlmManagerService _llmManager = App.GetService<LlmManagerService>();

        public async Task<ChatMessage> ChatAsync(string prompt)
        {
            return await _llmManager.ChatAsync(prompt);
        }

        public Task<BatchChatResponse> BatchChatAsync(string prompt)
        {
            return _llmManager.BatchChatAsync(prompt);
        }

        #endregion
    }
}