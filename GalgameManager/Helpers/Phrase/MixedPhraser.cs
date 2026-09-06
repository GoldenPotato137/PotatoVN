using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Helpers.Phrase;

public class MixedPhraser : IGalInfoPhraser, IGalCharacterPhraser, IGalStaffParser, IGalCoversParser
{
    private MixedPhraserData _data;
    private IEnumerable<string> _developerList;
    private bool _init;
    private Dictionary<RssType, IGalInfoPhraser?> _phrasers = new();
    private readonly IMessenger? _bus;

    private void Init()
    {
        _init = true;
        _developerList = ProducerDataHelper.Producers.SelectMany(p => p.Names);
    }

    private string? GetDeveloperFromTags(Galgame galgame)
    {
        if (!_init)
            Init();
        string? result = null;
        foreach (var tag in galgame.Tags.Value!)
        {
            double maxSimilarity = 0;
            foreach (var dev in _developerList)
            {
                if (IGalInfoPhraser.Similarity(dev, tag) > maxSimilarity)
                {
                    maxSimilarity = IGalInfoPhraser.Similarity(dev, tag);
                    result = dev;
                }
            }

            if (result != null && maxSimilarity > 0.75) // magic number: 一个tag和开发商的相似度大于0.75就认为是开发商
                break;
        }

        return result;
    }

    private bool IsPhraserEnabled(RssType rssType)
    {
        return rssType switch
        {
            RssType.Bangumi => _data.Enabled.BangumiEnabled,
            RssType.Vndb => _data.Enabled.VndbEnabled,
            RssType.Ymgal => _data.Enabled.YmgalEnabled,
            RssType.Steam => _data.Enabled.SteamEnabled,
            RssType.Hikarinagi => _data.Enabled.HikarinagiEnabled,
            _ => true
        };
    }

    public MixedPhraser(IGalInfoPhraser bgmPhraser, IGalInfoPhraser vndbPhraser, IGalInfoPhraser ymgalPhraser,
        IGalInfoPhraser steamParser, IGalInfoPhraser hikarinagiPhraser, MixedPhraserData data, IMessenger? bus = null)
    {
        _phrasers[RssType.Bangumi] = bgmPhraser;
        _phrasers[RssType.Vndb] = vndbPhraser;
        _phrasers[RssType.Ymgal] = ymgalPhraser;
        _phrasers[RssType.Steam] = steamParser;
        _phrasers[RssType.Hikarinagi] = hikarinagiPhraser;
        _data = data;
        _developerList = new List<string>();
        _bus = bus;
    }

    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        if (!_init) Init();
        Dictionary<RssType, Task<Galgame?>?> phraserTasks = new();
        object lockObj = new();
        foreach (RssType phraserType in RssTypeHelper.UsablePhrasers)
        {
            if (_phrasers.TryGetValue(phraserType, out IGalInfoPhraser? phraser) && phraser != null && IsPhraserEnabled(phraserType))
            {
                if (galgame.Ids[(int)phraserType] == "-1") continue;
                Galgame game = new() { Name = galgame.Name };
                game.RssType = phraserType;
                game.Ids = (string?[])galgame.Ids.Clone();
                lock (lockObj)
                {
                    phraserTasks[phraserType] = phraser.GetGalgameInfo(game);
                }
                _ = phraserTasks[phraserType]!.ContinueWith(_ =>
                {
                    _bus?.Send(new GalgameParsingEventArgs(galgame, GetWaitingMsg()));
                });
            }
        }

        _bus?.Send(new GalgameParsingEventArgs(galgame, GetWaitingMsg()));
        List<Task<Galgame?>> pending;
        lock (lockObj)
            pending = phraserTasks.Values.Where(t => t != null).Cast<Task<Galgame?>>().ToList();
        if (pending.Count > 0)
        {
            try
            {
                if (_data.TimeoutSeconds > 0)
                    await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(_data.TimeoutSeconds));
                else
                    await Task.WhenAll(pending);
            }
            catch (TimeoutException)
            {
                // Soft timeout: drop incomplete sources below.
            }
            catch (Exception)
            {
                // WhenAll throws if any task faults; incomplete/faulted sources are dropped below.
            }
        }

        foreach (var (rssType, task) in phraserTasks.ToList())
        {
            if (task is null) continue;
            if (!task.IsCompletedSuccessfully)
            {
                lock (lockObj)
                    phraserTasks[rssType] = null;
            }
        }

        Dictionary<RssType, Galgame> metas = new();
        Galgame result = new();
        foreach (var (rssType, task) in phraserTasks)
        {
            if (task == null) continue;
            if (await task is { } game)
            {
                metas[rssType] = game;
                result.Ids[(int)rssType] = game.Id;
            }
        }
        foreach (RssType rss in RssTypeHelper.UsablePhrasers.Where(rss => galgame.Ids[(int)rss] == "-1"))
            result.Ids[(int)rss] = "-1";

        if (metas.Count == 0) return null;

        // 对比所有的phraser的游戏名
        // 如果有一个phraser的游戏名和当前的游戏名不一样，就认为是不同的游戏，重新获取

        // 创建一个与metas长度相同的bool数组，初始值为false
        List<bool> isSameGame = new List<bool>(new bool[metas.Count]);

        for (int i = 0; i < metas.Count; i++)
        {
            for (int j = i + 1; j < metas.Count; j++)
            {
                var name1 = metas.ElementAt(i).Value.Name.Value ?? string.Empty;
                var name2 = metas.ElementAt(j).Value.Name.Value ?? string.Empty;
                int threshold = (int)(Math.Max(name1.Length, name2.Length) * 0.3);
                if (name1.Levenshtein(name2) <= threshold)
                {
                    // 如果两个游戏名的相似度大于等于0.3，就认为是同一个游戏
                    // 如果游戏名长度为10时，有超过三个字符不一致时则认为是不同的游戏
                    isSameGame[i] = true;
                    isSameGame[j] = true;
                }
            }
        }

        // 如果所有的游戏名都不一样, 仅保留第一条记录
        // 否则删除游戏名不一样的记录
        if (isSameGame.All(x => x == false))
        {

            while (metas.Count > 1)
            {
                var removedRssType = metas.ElementAt(metas.Count - 1).Key;
                // 删除最后一个元素
                metas.Remove(removedRssType);
                result.Ids[(int)removedRssType] = null;
            }
        }
        else if (isSameGame.Any(x => x == true))
        {
            // 无事发生
        }
        else
        {
            foreach (var rssType in metas.Keys.ToList())
            {
                if (!isSameGame[(int)rssType])
                    metas.Remove(rssType);
                result.Ids[(int)rssType] = null;
            }
        }

        // 合并信息
        result.RssType = RssType.Mixed;
        result.UpdateMixedId();
        // name
        if (_data.Enabled.NameEnabled)
            result.Name = GetValue(metas, nameof(Galgame.Name), _ => true,
                new LockableProperty<string>(string.Empty));
        // description
        if (_data.Enabled.DescriptionEnabled)
            result.Description = GetValue(metas, nameof(Galgame.Description),
                _ => true, new LockableProperty<string>(string.Empty));
        // expectedPlayTime
        if (_data.Enabled.ExpectedPlayTimeEnabled)
            result.ExpectedPlayTime = GetValue(metas, nameof(Galgame.ExpectedPlayTime),
                meta => CheckStr(meta.ExpectedPlayTime.Value),
                new LockableProperty<string>(Galgame.DefaultString));
        // rating
        if (_data.Enabled.RatingEnabled)
            result.Rating = GetValue(metas, nameof(Galgame.Rating),
                _ => true, new LockableProperty<float>(0));
        // imageUrl
        if (_data.Enabled.ImageUrlEnabled)
        {
            result.ImageUrl = GetValue<string>(metas, nameof(Galgame.ImageUrl),
                meta => CheckStr(meta.ImageUrl), null!);
            foreach (RssType type in _data.Order.ImageUrlOrder)
                if (metas.TryGetValue(type, out Galgame? tmp) && !string.IsNullOrEmpty(tmp?.ImageUrl))
                    result.AlternateImageUrls.Add(tmp.ImageUrl);
            result.AlternateImageUrls.Remove(result.ImageUrl);
        }
        // release date
        if (_data.Enabled.ReleaseDateEnabled)
            result.ReleaseDate = GetValue(metas, nameof(Galgame.ReleaseDate),
                meta => meta.ReleaseDate.Value != DateTime.MinValue,
                new LockableProperty<DateTime>(DateTime.MinValue));
        // characters
        if (_data.Enabled.CharactersEnabled)
            result.Characters = GetValue(metas, nameof(Galgame.Characters),
                meta => meta.Characters.Count > 0, new ObservableCollection<GalgameCharacter>());
        // Chinese name
        if (_data.Enabled.CnNameEnabled)
            result.CnName = GetValue(metas, nameof(Galgame.CnName),
                meta => CheckStr(meta.CnName), string.Empty);
        // tags (must be scraped before developer extraction from tags)
        if (_data.Enabled.TagsEnabled)
            result.Tags = GetValue(metas, nameof(Galgame.Tags),
                meta => meta.Tags.Value?.Count > 0,
                new LockableProperty<ObservableCollection<string>>(new ObservableCollection<string>()));
        // developer
        if (_data.Enabled.DeveloperEnabled)
        {
            result.Developer = GetValue(metas, nameof(Galgame.Developer),
                meta => CheckStr(meta.Developer),
                new LockableProperty<string>(Galgame.DefaultString));
            // developer from tag
            if (_data.Enabled.TagsEnabled && result.Developer == Galgame.DefaultString)
            {
                var tmp = GetDeveloperFromTags(result);
                if (tmp != null)
                    result.Developer = tmp;
            }
        }
        // engine
        if (_data.Enabled.EngineEnabled)
        {
            result.Engine = GetValue(metas, nameof(Galgame.Engine),
                meta => CheckStr(meta.Engine),
                new LockableProperty<string>(Galgame.DefaultString));
        }

        _bus?.Send(new GalgameParsingEventArgs(galgame, "done!"));
        return result;

        bool CheckStr(string? str) => !string.IsNullOrEmpty(str) && str != Galgame.DefaultString;

        string GetWaitingMsg()
        {
            lock (lockObj)
            {
                var tmp = "MixedParser_WaitingMsg".GetLocalized();
                foreach (var (rssType, task) in phraserTasks)
                {
                    if (task == null) continue;
                    if (!task.IsCompleted)
                        tmp += rssType + " ";
                }
                return tmp.Trim();
            }
        }
    }

    public void UpdateData(IGalInfoPhraserData data) => _data = (MixedPhraserData)data;

    public RssType GetPhraseType() => RssType.Mixed;

    /// <summary>
    /// 获取封面图片，遍历所有支持Cover的解析器
    /// </summary>
    public async Task<List<string>> GetGalCoversAsync(Galgame galgame)
    {
        if (!_init) Init();
        List<string> result = [];
        foreach (RssType phraserType in _data.Order.ImageUrlOrder)
        {
            if (!IsPhraserEnabled(phraserType)) continue;
            if (galgame.Ids[(int)phraserType] == "-1") continue;
            if (_phrasers.TryGetValue(phraserType, out IGalInfoPhraser? phraser) &&
                phraser != null && phraser is IGalCoversParser coverParser)
            {
                Galgame game = new() { Name = galgame.Name };
                game.RssType = phraserType;
                game.Ids = (string?[])galgame.Ids.Clone();

                try
                {
                    List<string> covers = await coverParser.GetGalCoversAsync(game);
                    result.AddRange(covers);
                }
                catch
                {
                    // ignore individual phraser failures
                }
            }
        }
        return result.Distinct().ToList();
    }

    public async Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter)
    {
        foreach (RssType phraserType in _data.Order.CharactersOrder)
        {
            if (galgameCharacter.Ids[(int)phraserType] != null &&
                _phrasers.TryGetValue(phraserType, out IGalInfoPhraser? phraser) &&
                phraser is IGalCharacterPhraser characterPhraser)
                return await characterPhraser.GetGalgameCharacter(galgameCharacter);
        }

        return null;
    }

    private T GetValue<T>(Dictionary<RssType, Galgame> metas, string propName, Func<Galgame, bool> isValueAvailable,
        T defaultValue)
    {
        ObservableCollection<RssType> order = GetOrder();
        foreach (RssType rssType in order)
        {
            if (!metas.TryGetValue(rssType, out Galgame? meta)) continue;
            if (isValueAvailable(meta))
                return (T)(meta.GetType().GetProperty(propName)?.GetValue(meta) ??
                           meta.GetType().GetField(propName)?.GetValue(meta)!);
        }

        return defaultValue;

        ObservableCollection<RssType> GetOrder()
        {
            Type type = typeof(MixedPhraserOrder);
            PropertyInfo? prop = type.GetProperty($"{propName}Order");
            Debug.Assert(prop != null, nameof(prop) + " != null");
            return (ObservableCollection<RssType>)prop.GetValue(_data.Order)!;
        }
    }

    public async Task<Staff?> GetStaffAsync(Staff staff)
    {
        foreach (RssType phraserType in _data.Order.StaffOrder)
        {
            try
            {
                if (staff.Ids[(int)phraserType] != null &&
                    _phrasers.TryGetValue(phraserType, out IGalInfoPhraser? phraser) &&
                    phraser is IGalStaffParser staffParser)
                    return await staffParser.GetStaffAsync(staff);
            }
            catch (Exception)
            {
                //ignore
            }
        }

        return null;
    }

    public async Task<List<StaffRelation>> GetStaffsAsync(Galgame game)
    {
        foreach (RssType phraserType in _data.Order.StaffOrder)
        {
            try
            {
                if (game.Ids[(int)phraserType] != null &&
                    _phrasers.TryGetValue(phraserType, out IGalInfoPhraser? phraser) &&
                    phraser is IGalStaffParser staffParser)
                    return await staffParser.GetStaffsAsync(game);
            }
            catch (Exception)
            {
                // ignore
            }
        }
        return [];
    }
}

public class MixedPhraserOrder
{
    // 版本号，每次添加新搜刮器/添加新字段的时候都应该把这个数字+1，以便galgameCollectionService能够更新配置中已有的顺序配置
    // 更新配置不需要手动编写，已经在GalgameCollectionService中使用反射实现，会自动添加新的默认配置
    public const int Version = 14;

    // 为什么使用ObservableCollection：为了能够在MixedPhraserOrderDialog中使顺序能够drag&drop
    // 所有变量都应该命名为：{字段名}Order，此处字段名应该与Galgame中对应的字段名一致（为了让GetValue中的反射能够找到对应的字段）
    public ObservableCollection<RssType> NameOrder { get; set; } = new();
    public ObservableCollection<RssType> DescriptionOrder { get; set; } = new();
    public ObservableCollection<RssType> ExpectedPlayTimeOrder { get; set; } = new();
    public ObservableCollection<RssType> RatingOrder { get; set; } = new();
    public ObservableCollection<RssType> ImageUrlOrder { get; set; } = new();
    public ObservableCollection<RssType> ReleaseDateOrder { get; set; } = new();
    public ObservableCollection<RssType> CharactersOrder { get; set; } = new();
    public ObservableCollection<RssType> CnNameOrder { get; set; } = new();
    public ObservableCollection<RssType> DeveloperOrder { get; set; } = new();
    public ObservableCollection<RssType> EngineOrder { get; set; } = new();
    public ObservableCollection<RssType> TagsOrder { get; set; } = new();
    public ObservableCollection<RssType> StaffOrder { get; set; } = new();

    public MixedPhraserOrder SetToDefault(bool isChineseCulture = true)
    {

        // Hikarinagi不提供评分与Staff信息（且评分合并不做空值检查），故RatingOrder/StaffOrder中不加入Hikarinagi
        if (isChineseCulture)
        {
            // 中文用户偏好的顺序设置
            NameOrder = new() { RssType.Hikarinagi, RssType.Bangumi, RssType.Ymgal, RssType.Vndb, RssType.Steam };
            DescriptionOrder = new() { RssType.Hikarinagi, RssType.Bangumi, RssType.Ymgal, RssType.Vndb, RssType.Steam };
            ExpectedPlayTimeOrder = new() { RssType.Vndb };
            RatingOrder = new() { RssType.Bangumi, RssType.Vndb };
            ImageUrlOrder = new() { RssType.Steam, RssType.Vndb, RssType.Hikarinagi, RssType.Bangumi, RssType.Ymgal,  };
            ReleaseDateOrder = new() { RssType.Hikarinagi, RssType.Bangumi, RssType.Ymgal, RssType.Vndb };
            CharactersOrder = new() { RssType.Hikarinagi, RssType.Bangumi, RssType.Ymgal, RssType.Vndb };
            CnNameOrder = new() { RssType.Hikarinagi, RssType.Bangumi, RssType.Ymgal, RssType.Vndb };
            DeveloperOrder = new() { RssType.Hikarinagi, RssType.Bangumi, RssType.Ymgal, RssType.Vndb, RssType.Steam };
            EngineOrder = new() { RssType.Vndb };
            TagsOrder = new() { RssType.Hikarinagi, RssType.Bangumi, RssType.Vndb, RssType.Steam };
            StaffOrder = new() { RssType.Bangumi, RssType.Ymgal, RssType.Vndb };
        }
        else
        {
            // 非中文用户偏好的顺序设置
            NameOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Hikarinagi, RssType.Bangumi, RssType.Steam };
            DescriptionOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Steam, RssType.Hikarinagi, RssType.Bangumi };
            ExpectedPlayTimeOrder = new() { RssType.Vndb };
            RatingOrder = new() { RssType.Vndb, RssType.Bangumi };
            ImageUrlOrder = new() { RssType.Steam, RssType.Vndb, RssType.Ymgal, RssType.Hikarinagi, RssType.Bangumi };
            ReleaseDateOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Hikarinagi, RssType.Bangumi };
            CharactersOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Hikarinagi, RssType.Bangumi };
            CnNameOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Hikarinagi, RssType.Bangumi };
            DeveloperOrder = new() { RssType.Vndb, RssType.Steam, RssType.Ymgal, RssType.Hikarinagi, RssType.Bangumi };
            EngineOrder = new() { RssType.Vndb };
            TagsOrder = new() { RssType.Vndb, RssType.Steam, RssType.Hikarinagi, RssType.Bangumi };
            StaffOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Bangumi };
        }

        return this;
    }
}

public class MixedPhraserData : IGalInfoPhraserData
{
    public required MixedPhraserOrder Order { get; init; }
    public required MixedPhraserEnabled Enabled { get; init; }
    /// <summary>统一最长等待秒数；0 表示不限制。</summary>
    public int TimeoutSeconds { get; init; } = 30;
}

public class MixedPhraserEnabled
{
    public bool BangumiEnabled { get; set; } = false;
    public bool VndbEnabled { get; set; } = true;
    public bool YmgalEnabled { get; set; } = true;
    public bool SteamEnabled { get; set; } = true;
    public bool HikarinagiEnabled { get; set; } = true;

    // Information type scraping toggles
    public bool NameEnabled { get; set; } = true;
    public bool DescriptionEnabled { get; set; } = true;
    public bool DeveloperEnabled { get; set; } = true;
    public bool EngineEnabled { get; set; } = true;
    public bool TagsEnabled { get; set; } = true;
    public bool RatingEnabled { get; set; } = true;
    public bool ExpectedPlayTimeEnabled { get; set; } = true;
    public bool ReleaseDateEnabled { get; set; } = true;
    public bool CnNameEnabled { get; set; } = true;
    public bool ImageUrlEnabled { get; set; } = true;
    public bool CharactersEnabled { get; set; } = true;
}
