using System.Diagnostics;
using System.Runtime.InteropServices;
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
    public async Task<Galgame> AddGameAsync(GalgameSourceType sourceType, string path, bool force,
        bool requireConfirm = true)
    {
        IGalgameSourceService sourceService = SourceServiceFactory.GetSourceService(sourceType);
        Galgame? meta = null;
        // 尝试从本地获取游戏信息
        try
        {
            if (sourceType is not GalgameSourceType.Virtual) meta = await sourceService.LoadMetaAsync(path);
        }
        catch (Exception)
        {
            _infoService.Info(InfoBarSeverity.Warning, title: "GalgameCollectionService_LoadMetaFailed".GetLocalized(),
                "GalgameCollectionService_LoadMetaFailed_Msg".GetLocalized(path));
        }

        // 尝试从数据源获取游戏信息
        try
        {
            meta ??= await ParseGalInfoOnlyAsync(new Galgame(await GetNameFromPath(sourceType, path)),
                requireConfirm: requireConfirm);
        }
        catch (Exception e)
        {
            meta ??= new Galgame(await GetNameFromPath(sourceType, path));
            _infoService.Log(msg:$"Failed on parsing galgame info for {e}");
        }
        
        // 检查该游戏是否已经存在
        if (GetGalgameFromUid(meta.Uid) is { } existGame)
        {
            if (existGame.Uid.GetMatchKind(meta.Uid) == GalgameUidMatchKind.NameOnly)
            {
                if (!requireConfirm)
                    throw new NameOnlyGameMatchException(existGame.Uuid, existGame.Name.Value ?? string.Empty);
                ContentDialog confirmDialog = new()
                {
                    XamlRoot = App.MainWindow!.Content.XamlRoot,
                    Title = "MultiInstall_NameMatch_Title".GetLocalized(),
                    Content = "MultiInstall_NameMatch_Content".GetLocalized() +
                              $"\n{existGame.Name.Value}\n{path}",
                    PrimaryButtonText = "MultiInstall_LinkInstallation".GetLocalized(),
                    CloseButtonText = "Cancel".GetLocalized(),
                    DefaultButton = ContentDialogButton.Close,
                };
                if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
                    throw new PvnException("Canceled".GetLocalized());
            }
            Galgame tmp = await DealWithExistGameAsync(sourceType, path, existGame, meta);
            await SaveGalgameAsync(tmp);
            await RaiseGalgameMutatedAsync(new GalgameMutationEventArgs(tmp, GalgameChangeKind.SourceEntries,
                GalgameChangeOrigin.LocalOperation));
            return tmp;
        }
        
        // 如果不是强制添加，且没有找到游戏信息，则抛出异常
        if (!force && meta.IsIdsEmpty())
            throw new PvnException("AddGalgameResult_NotFoundInRss".GetLocalized());

        // 添加游戏并移入对应的源
        meta.AddTime = DateTime.Now; // 游戏添加时间
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            try
            {
                lock (_gamePersistenceLock)
                    _galgames.Add(meta);
            }
            catch (COMException e)
            {
                _infoService.DeveloperEvent(e:e);
            }
        });
        GameParseType parseType = GameParseType.HeaderImage | GameParseType.Character;
        if (meta.ImagePath.Value == Galgame.DefaultImagePath) parseType |= GameParseType.Image;
        await ParseGalInfoInternalAsync(meta, RssType.None, requireConfirm: false, parseType, notify: false);
        if (!IsCurrentGameInstance(meta))
            throw new PvnException("Canceled".GetLocalized());
        
        meta.ErrorOccurred += e =>
            _infoService.Event(EventType.GalgameEvent, InfoBarSeverity.Warning, "GalgameEvent", e);
        GalgameSourceBase source = await GetOrAddSourceAsync(sourceType, path);
        LocalInstallationConfig? localConfig =
            source is ILocalGalgameSource
                ? meta.CreateLegacyLocalConfiguration(path)
                : null;
        GalgameAndPath? addedEntry = _galSrcService.MoveInNoOperate(source, meta, path, localConfig);
        if (!IsCurrentGameInstance(meta))
        {
            if (addedEntry?.Source?.Galgames.Contains(addedEntry) is true)
                await _galSrcService.MoveOutNoOperate(addedEntry);
            throw new PvnException("Canceled".GetLocalized());
        }
        
        await SaveGalgameAsync(meta);
        if (!IsCurrentGameInstance(meta))
        {
            if (addedEntry?.Source?.Galgames.Contains(addedEntry) is true)
                await _galSrcService.MoveOutNoOperate(addedEntry);
            throw new PvnException("Canceled".GetLocalized());
        }
        GalgameChangeKind changes = GalgameChangeKind.Added | GalgameChangeKind.Metadata |
                                    GalgameChangeKind.SourceEntries | GalgameChangeKind.Images |
                                    GalgameChangeKind.Characters;
        await RaiseGalgameMutatedAsync(new GalgameMutationEventArgs(meta, changes,
            GalgameChangeOrigin.LocalOperation));
        return meta;
    }
    
    public async Task AddVirtualGalgameAsync(Galgame game,
        GalgameChangeOrigin origin = GalgameChangeOrigin.LocalOperation)
    {
        await UiThreadInvokeHelper.InvokeAsync(() =>
        {
            try
            {
                lock (_gamePersistenceLock)
                    _galgames.Add(game);
            }
            catch (COMException e)
            {
                _infoService.DeveloperEvent(e:e);
            }
        });
        await SaveGalgameAsync(game);
        await RaiseGalgameMutatedAsync(new GalgameMutationEventArgs(game, GalgameChangeKind.Added, origin));
    }

    public async Task<Galgame> SetLocalPathAsync(Galgame galgame, string path)
    {
        Galgame result = await DealWithExistGameAsync(GalgameSourceType.LocalFolder, path, galgame, null);
        await SaveGalgameAsync(result);
        await RaiseGalgameMutatedAsync(new GalgameMutationEventArgs(result, GalgameChangeKind.SourceEntries,
            GalgameChangeOrigin.LocalOperation));
        return result;
    }

    public async Task<string> GetNameFromPath(GalgameSourceType sourceType, string path)
    {
        switch (sourceType)
        {
            case GalgameSourceType.Virtual:
                return path;
            case GalgameSourceType.LocalZip:
            {
                // 压缩包：从文件名提取包名（去掉 .part1.zip 等后缀）
                var zipName = GalgameZipSource.GetPackName(path);
                var zipPattern = await LocalSettingsService.ReadSettingAsync<string>(KeyValues.RegexPattern) ?? ".+";
                var zipRegexIndex = await LocalSettingsService.ReadSettingAsync<int>(KeyValues.RegexIndex);
                var zipRemoveBorder = await LocalSettingsService.ReadSettingAsync<bool>(KeyValues.RegexRemoveBorder);
                return NameRegex.GetName(zipName, zipPattern, zipRemoveBorder, zipRegexIndex);
            }
            case GalgameSourceType.LocalFolder:
            case GalgameSourceType.Steam:
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type">这次新增的库的类型</param>
    /// <param name="path">该游戏在该库的位置（绝对位置）</param>
    /// <param name="existGame"></param>
    /// <param name="meta">如果已有该游戏的信息（.PotatoVN文件夹），填入它来合并游戏信息</param>
    /// <returns></returns>
    /// <exception cref="PvnException"></exception>
    private async Task<Galgame> DealWithExistGameAsync(GalgameSourceType type, string path, Galgame existGame,
        Galgame? meta)
    {
        existGame.MergeTime(meta);
        switch (type)
        {
            case GalgameSourceType.Virtual:
                throw new PvnException("AddGalgameResult_AlreadyInLibrary".GetLocalized());
            case GalgameSourceType.LocalFolder:
            case GalgameSourceType.Steam:
                GalgameSourceBase localSource = await GetOrAddSourceAsync(type, path);
                if (localSource.Contain(existGame))
                    throw new PvnException("AddGalgameResult_AlreadyInLibrary".GetLocalized());
                LocalInstallationConfig? config = meta?.CreateLegacyLocalConfiguration(path);
                _galSrcService.MoveInNoOperate(localSource, existGame, path, config);
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
