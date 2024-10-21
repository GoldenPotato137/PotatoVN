using System.Diagnostics;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Models.Sources;
using Microsoft.UI.Xaml.Controls;
using PvnException = GalgameManager.Models.PvnException;

namespace GalgameManager.Services;

public partial class GalgameCollectionService
{
    public async Task<Galgame> AddGameAsync(GalgameSourceType sourceType, string path, bool force)
    {
        IGalgameSourceService sourceService = SourceServiceFactory.GetSourceService(sourceType);
        Galgame? meta = null;
        // 尝试从本地获取游戏信息
        try
        {
            meta = await sourceService.LoadMetaAsync(path);
        }
        catch (Exception)
        {
            _infoService.Info(InfoBarSeverity.Warning, title: "GalgameCollectionService_LoadMetaFailed".GetLocalized(),
                "GalgameCollectionService_LoadMetaFailed_Msg".GetLocalized(path));
        }

        // 尝试从数据源获取游戏信息
        meta ??= await PhraseGalInfoAsync(new Galgame(await GetNameFromPath(sourceType, path)));
        // 检查该游戏是否已经存在
        if (GetGalgameFromUid(meta.Uid) is { } existGame)
        {
            Galgame tmp = await DealWithExistGameAsync(sourceType, path, existGame);
            GalgameChangedEvent?.Invoke(tmp);
            return tmp;
        }
        
        // 如果不是强制添加，且没有找到游戏信息，则抛出异常
        if (!force && meta.IsIdsEmpty())
            throw new PvnException("AddGalgameResult_NotFoundInRss".GetLocalized());

        // 添加游戏并移入对应的源
        _galgames.Add(meta);
        _galgameMap[meta.Uid] = meta;
        GalgameAddedEvent?.Invoke(meta);
        GalgameChangedEvent?.Invoke(meta);
        meta.ErrorOccurred += e =>
            _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning, "GalgameEvent", e);
        GalgameSourceBase source = await GetOrAddSourceAsync(sourceType, path);
        _galSrcService.MoveInNoOperate(source, meta, path);
        
        await SaveGalgamesAsync(meta);
        return meta;
    }

    public async Task<Galgame> SetLocalPathAsync(Galgame galgame, string path)
    {
        Galgame result = await DealWithExistGameAsync(GalgameSourceType.LocalFolder, path, galgame);
        GalgameChangedEvent?.Invoke(result);
        await SaveGalgamesAsync(result);
        return result;
    }

    private async Task<string> GetNameFromPath(GalgameSourceType sourceType, string path)
    {
        switch (sourceType)
        {
            case GalgameSourceType.LocalFolder:
            case GalgameSourceType.LocalZip:
                var name = Path.GetFileName(Path.GetDirectoryName(path + Path.DirectorySeparatorChar)) ??
                           throw new Exception("GalgameCollectionService_GetNameFromPathFailed".GetLocalized());
                var pattern = await LocalSettingsService.ReadSettingAsync<string>(KeyValues.RegexPattern) ?? ".+";
                var regexIndex = await LocalSettingsService.ReadSettingAsync<int>(KeyValues.RegexIndex);
                var removeBorder = await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.RegexRemoveBorder); 
                return NameRegex.GetName(name, pattern, removeBorder, regexIndex);
        }

        Debug.Fail("应该在GalgameCollectionService_AddGame里面实现该类型源的GetNameFromPath");
        throw new PvnException(string.Empty);
    }

    private async Task<Galgame> DealWithExistGameAsync(GalgameSourceType type, string path, Galgame existGame)
    {
        switch (type)
        {
            case GalgameSourceType.LocalFolder:
                // 一个游戏只能属于一个本地文件夹
                if (existGame.Sources.Any(s => s is GalgameFolderSource))
                    throw new PvnException("AddGalgameResult_AlreadyInLibrary".GetLocalized());
                // 把游戏移入对应的本地库
                _galSrcService.MoveInNoOperate(await GetOrAddSourceAsync(GalgameSourceType.LocalFolder, path),
                    existGame, path);
                break;
            case GalgameSourceType.LocalZip:
                GalgameSourceBase targetSource = await GetOrAddSourceAsync(GalgameSourceType.LocalZip, path);
                if (targetSource.Contain(existGame))
                    throw new PvnException("AddGalgameResult_AlreadyInLibrary".GetLocalized());
                // 把游戏移入对应的本地压缩库
                _galSrcService.MoveInNoOperate(targetSource, existGame, path);
                break;
            default:
                Debug.Fail("应该在GalgameCollectionService_AddGame里面实现该类型源的DealWithExistGameAsync");
                throw new PvnException(string.Empty);
        }
        return existGame;
    }

    /// 获取某个游戏的源，若不存在则添加
    private async Task<GalgameSourceBase> GetOrAddSourceAsync(GalgameSourceType type, string gamePath)
    {
        // 从游戏路径获取源路径
        string sourcePath;
        try
        {
            sourcePath = _galSrcService.GetSourcePath(type, gamePath);
        }
        catch (Exception e)
        {
            throw new PvnException($"Failed to get source path {e.Message}");
        }

        GalgameSourceBase? source = _galSrcService.GetGalgameSource(type, sourcePath);
        source ??= await _galSrcService.AddGalgameSourceAsync(type, sourcePath, false);
        return source;
    }
}