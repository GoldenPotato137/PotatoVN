using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Helpers.Phrase;

public class MixedPhraser : IGalInfoPhraser, IGalCharacterPhraser, IGalStaffParser
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
            _ => true
        };
    }

    public MixedPhraser(BgmPhraser bgmPhraser, VndbPhraser vndbPhraser, YmgalPhraser ymgalPhraser, 
        SteamParser steamParser, MixedPhraserData data, IMessenger? bus = null)
    {
        _phrasers[RssType.Bangumi] = bgmPhraser;
        _phrasers[RssType.Vndb] = vndbPhraser;
        _phrasers[RssType.Ymgal] = ymgalPhraser;
        _phrasers[RssType.Steam] = steamParser;
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
        {
            List<(RssType rssType, Task<Galgame?> task)> running = new List<(RssType rssType, Task<Galgame?> task)>();
            lock (lockObj)
                foreach (var (rssType, task) in phraserTasks)
                    if (task != null)
                        running.Add((rssType, task));
            try
            {
                await Task.WhenAll(running.Select(t => t.task));
            }
            catch (Exception)
            {
                // ignore aggregate exceptions; handle per-task status below
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
        result.Name = GetValue(metas, nameof(Galgame.Name), _ => true,
            new LockableProperty<string>(string.Empty));
        // description
        result.Description = GetValue(metas, nameof(Galgame.Description),
            _ => true, new LockableProperty<string>(string.Empty));
        // expectedPlayTime
        result.ExpectedPlayTime = GetValue(metas, nameof(Galgame.ExpectedPlayTime),
            meta => CheckStr(meta.ExpectedPlayTime.Value),
            new LockableProperty<string>(Galgame.DefaultString));
        // rating
        result.Rating = GetValue(metas, nameof(Galgame.Rating),
            _ => true, new LockableProperty<float>(0));
        // imageUrl
        result.ImageUrl = GetValue<string>(metas, nameof(Galgame.ImageUrl),
            meta => CheckStr(meta.ImageUrl), null!);
        foreach (RssType type in _data.Order.ImageUrlOrder)
            if (metas.TryGetValue(type, out Galgame? tmp) && !string.IsNullOrEmpty(tmp?.ImageUrl))
                result.AlternateImageUrls.Add(tmp.ImageUrl);
        result.AlternateImageUrls.Remove(result.ImageUrl);
        // release date
        result.ReleaseDate = GetValue(metas, nameof(Galgame.ReleaseDate),
            meta => meta.ReleaseDate.Value != DateTime.MinValue,
            new LockableProperty<DateTime>(DateTime.MinValue));
        // characters
        result.Characters = GetValue(metas, nameof(Galgame.Characters),
            meta => meta.Characters.Count > 0, new ObservableCollection<GalgameCharacter>());
        // Chinese name
        result.CnName = GetValue(metas, nameof(Galgame.CnName),
            meta => CheckStr(meta.CnName), string.Empty);
        // developer
        result.Developer = GetValue(metas, nameof(Galgame.Developer),
            meta => CheckStr(meta.Developer),
            new LockableProperty<string>(Galgame.DefaultString));
        // tags
        result.Tags = GetValue(metas, nameof(Galgame.Tags),
            meta => meta.Tags.Value?.Count > 0,
            new LockableProperty<ObservableCollection<string>>(new ObservableCollection<string>()));

        // developer from tag
        if (result.Developer == Galgame.DefaultString)
        {
            var tmp = GetDeveloperFromTags(result);
            if (tmp != null)
                result.Developer = tmp;
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
    public const int Version = 12;
    
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
    public ObservableCollection<RssType> TagsOrder { get; set; } = new();
    public ObservableCollection<RssType> StaffOrder { get; set; } = new();

    public MixedPhraserOrder SetToDefault(bool isChineseCulture = true)
    {

        if (isChineseCulture)
        {
            // 中文用户偏好的顺序设置
            NameOrder = new() { RssType.Bangumi, RssType.Ymgal, RssType.Vndb, RssType.Steam };
            DescriptionOrder = new() { RssType.Bangumi, RssType.Ymgal, RssType.Vndb, RssType.Steam };
            ExpectedPlayTimeOrder = new() { RssType.Vndb };
            RatingOrder = new() { RssType.Bangumi, RssType.Vndb };
            ImageUrlOrder = new() { RssType.Steam, RssType.Vndb, RssType.Bangumi, RssType.Ymgal,  };
            ReleaseDateOrder = new() { RssType.Bangumi, RssType.Ymgal, RssType.Vndb };
            CharactersOrder = new() { RssType.Bangumi, RssType.Ymgal, RssType.Vndb };
            CnNameOrder = new() { RssType.Bangumi, RssType.Ymgal, RssType.Vndb };
            DeveloperOrder = new() { RssType.Bangumi, RssType.Ymgal, RssType.Vndb, RssType.Steam };
            TagsOrder = new() { RssType.Bangumi, RssType.Vndb, RssType.Steam };
            StaffOrder = new() { RssType.Bangumi, RssType.Ymgal, RssType.Vndb };
        }
        else
        {
            // 非中文用户偏好的顺序设置
            NameOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Bangumi, RssType.Steam };
            DescriptionOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Steam, RssType.Bangumi };
            ExpectedPlayTimeOrder = new() { RssType.Vndb };
            RatingOrder = new() { RssType.Vndb, RssType.Bangumi };
            ImageUrlOrder = new() { RssType.Steam, RssType.Vndb, RssType.Ymgal, RssType.Bangumi };
            ReleaseDateOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Bangumi };
            CharactersOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Bangumi };
            CnNameOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Bangumi };
            DeveloperOrder = new() { RssType.Vndb, RssType.Steam, RssType.Ymgal, RssType.Bangumi };
            TagsOrder = new() { RssType.Vndb, RssType.Steam, RssType.Bangumi };
            StaffOrder = new() { RssType.Vndb, RssType.Ymgal, RssType.Bangumi };
        }

        return this;
    }
}

public class MixedPhraserData : IGalInfoPhraserData
{
    public required MixedPhraserOrder Order { get; init; }
    public required MixedPhraserEnabled Enabled { get; init; }
}

public class MixedPhraserEnabled
{
    public bool BangumiEnabled { get; set; } = true;
    public bool VndbEnabled { get; set; } = true;
    public bool YmgalEnabled { get; set; } = true;
    public bool SteamEnabled { get; set; } = true;
}
