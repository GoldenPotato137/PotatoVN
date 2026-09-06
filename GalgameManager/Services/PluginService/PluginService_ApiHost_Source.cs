using GalgameManager.Contracts.Services;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.WinApp.Base.Contracts;
using GalgameManager.WinApp.Base.Models.Plugin;

namespace GalgameManager.Services;

public partial class PluginService
{
    public partial class PotatoVnApiHost : IPotatoVnApi
    {
        private readonly IGalgameSourceCollectionService _sourceCollectionService =
            App.GetService<IGalgameSourceCollectionService>();

        public List<GalgameSourceBase> GetAllSources() => _sourceCollectionService.GetGalgameSources().ToList();

        public GalgameSourceBase? GetSourceById(Guid sourceId) =>
            _sourceCollectionService.GetGalgameSourceFromId(sourceId);

        public GalgameSourceBase? GetSourceByUrl(string url) =>
            _sourceCollectionService.GetGalgameSourceFromUrl(url);

        public GalgameSourceBase? GetSource(GalgameSourceType type, string path) =>
            _sourceCollectionService.GetGalgameSource(type, path);

        public async Task<GalgameSourceBase> AddSourceAsync(GalgameSourceType type, string path,
            bool scan = true, bool manualSelectFolder = false)
        {
            GalgameSourceBase? result = null;
            await UiThreadInvokeHelper.InvokeAsync(async () =>
                result = await _sourceCollectionService.AddGalgameSourceAsync(type, path, scan, manualSelectFolder));
            return result ?? throw new InvalidOperationException("Failed to add game source.");
        }

        public Task DeleteSourceAsync(GalgameSourceBase source) => UiThreadInvokeHelper.InvokeAsync(() =>
            _sourceCollectionService.DeleteGalgameFolderAsync(ResolveSource(source)));

        public Task DeleteSourceAsync(GalgameSourceBase source, bool removeGames) =>
            UiThreadInvokeHelper.InvokeAsync(() =>
                _sourceCollectionService.DeleteGalgameFolderAsync(ResolveSource(source), removeGames));

        public void ScanAllSources() => UiThreadInvokeHelper.Invoke(_sourceCollectionService.ScanAll);

        public void ScanSource(GalgameSourceBase source) =>
            UiThreadInvokeHelper.Invoke(() => _sourceCollectionService.Scan(ResolveSource(source)));

        public void SaveSource(GalgameSourceBase source) => _sourceCollectionService.Save(ResolveSource(source));

        public GameInstallationInfo? AddGameToSource(GalgameSourceBase source, Galgame game, string path,
            LocalInstallationConfig? localConfiguration = null)
        {
            GalgameAndPath? entry = _sourceCollectionService.MoveInNoOperate(
                ResolveSource(source), ResolveGame(game), path, localConfiguration?.Clone());
            return entry is null ? null : ToInstallationInfo(entry);
        }

        public BgTaskBase MoveGame(Galgame game, Guid? moveInSourceId, string? moveInPath = null,
            Guid? moveOutSourceId = null)
        {
            if (moveInSourceId is null && moveOutSourceId is null)
                throw new ArgumentException("At least one source must be specified.");
            GalgameSourceBase? moveIn = ResolveSource(moveInSourceId);
            GalgameSourceBase? moveOut = ResolveSource(moveOutSourceId);
            return _sourceCollectionService.MoveAsync(moveIn, moveInPath, moveOut, ResolveGame(game));
        }

        public string GetSourcePath(GalgameSourceType type, string gamePath) =>
            _sourceCollectionService.GetSourcePath(type, gamePath);

        private GalgameSourceBase ResolveSource(GalgameSourceBase source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return ResolveSource(source.Id)!;
        }

        private GalgameSourceBase? ResolveSource(Guid? sourceId) => sourceId is { } id
            ? _sourceCollectionService.GetGalgameSourceFromId(id)
              ?? throw new KeyNotFoundException($"Game source {id} was not found.")
            : null;
    }
}
