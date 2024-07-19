using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Helpers.Phrase;

public class MixedPhraser : IGalInfoPhraser, IGalCharacterPhraser
{
    private readonly BgmPhraser _bgmPhraser;
    private readonly VndbPhraser _vndbPhraser;
    private readonly YmgalPhraser _ymgalPhraser;
    private MixedPhraserData _data;
    private IEnumerable<string> _developerList;
    private bool _init;
    private static string[] _sourcesNames = { "vndb", "bgm", "ymgal" };
    
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
            foreach(var dev in _developerList)
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
    
    public MixedPhraser(BgmPhraser bgmPhraser, VndbPhraser vndbPhraser, YmgalPhraser ymgalPhraser, MixedPhraserData data)
    {
        _bgmPhraser = bgmPhraser;
        _vndbPhraser = vndbPhraser;
        _ymgalPhraser = ymgalPhraser;
        _data = data;
        _developerList = new List<string>();
    }
    
    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        if (!_init) Init();
        Galgame? bgm = new(), vndb = new(), ymgal = new();
        bgm.Name = galgame.Name;
        vndb.Name = galgame.Name;
        ymgal.Name = galgame.Name;
        // 试图从Id中获取bgmId和vndbId
        Dictionary<string, string?> ids = Id2IdDict(galgame.Ids[(int)RssType.Mixed] ?? "");
        
        if (ids.TryGetValue("bgm", out var b))
        {
            bgm.RssType = RssType.Bangumi;
            bgm.Id = b;
        }
        if (ids.TryGetValue("vndb", out var v))
        {
            vndb.RssType = RssType.Vndb;
            vndb.Id = v;
        }
        if (ids.TryGetValue("ymgal", out var y))
        {
            ymgal.RssType = RssType.Ymgal;
            ymgal.Id = y;
        }
        Task<Galgame?> bgmTask = _bgmPhraser.GetGalgameInfo(bgm);
        Task<Galgame?> vndbTask = _vndbPhraser.GetGalgameInfo(vndb);
        Task<Galgame?> ymgalTask = _ymgalPhraser.GetGalgameInfo(ymgal);
        await Task.WhenAll(new List<Task> {bgmTask, vndbTask, ymgalTask}); //这几个源可以并行搜刮
        bgm = bgmTask.Result;
        vndb = vndbTask.Result;
        ymgal = ymgalTask.Result;
        if(bgm == null && vndb == null && ymgal == null)
            return null;
        Dictionary<RssType, Galgame> metas = new();
        if(bgm is not null) metas[RssType.Bangumi] = bgm;
        if(vndb is not null) metas[RssType.Vndb] = vndb;
        if(ymgal is not null) metas[RssType.Ymgal] = ymgal;
        
        // 合并信息
        Galgame result = new();
        result.RssType = RssType.Mixed;
        ids.Clear();
        ids["bgm"] = bgm?.Id;
        ids["vndb"] = vndb?.Id;
        ids["ymgal"] = ymgal?.Id;
        result.Id = IdDict2Id(ids); 
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
        return result;

        bool CheckStr(string? str) => !string.IsNullOrEmpty(str) && str != Galgame.DefaultString;
    }

    public void UpdateData(IGalInfoPhraserData data) => _data = (MixedPhraserData) data;
    
    public static Dictionary<string, string?> Id2IdDict(string ids)
    {
        Dictionary<string, string?> idDict = new();
        ids = ids.Replace("，", ",").Replace(" ", "");
        foreach (var id in ids.Split(",").Where(s => s.Contains(':')))
        {
            var parts = id.Split(":");
            if (parts.Length == 2 && _sourcesNames.Contains(parts[0])) 
                idDict[parts[0]] = parts[1] == "null" ? null : parts[1];
        }

        return idDict;
    }
    
    public static string IdDict2Id(Dictionary<string, string?> ids)
    {
        List<string> idParts = new();
        foreach (var (name, id) in ids)
        {
            if (_sourcesNames.Contains(name) && !id.IsNullOrEmpty())
            {
                idParts.Add($"{name}:{id}");
            }
        }
        return string.Join(",", idParts);
    }
    
    public static string IdList2Id(IList<string?> ids)
    {
        Dictionary<string, string?> idDict = new()
        {
            ["bgm"] = ids[(int)RssType.Bangumi],
            ["vndb"] = ids[(int)RssType.Vndb],
            ["ymgal"] = ids[(int)RssType.Ymgal]
        };
        return IdDict2Id(idDict);
    }

    public RssType GetPhraseType() => RssType.Mixed;

    public async Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter)
    {
        return await _bgmPhraser.GetGalgameCharacter(galgameCharacter);
    }

    private T GetValue<T>(Dictionary<RssType, Galgame> metas, string propName, Func<Galgame, bool> isValueAvailable, 
        T defaultValue)
    {
        ObservableCollection<RssType> order = GetOrder();
        foreach (RssType rssType in order)
        {
            if(!metas.TryGetValue(rssType, out Galgame? meta)) continue;
            if (isValueAvailable(meta))
                return (T)(meta.GetType().GetProperty(propName)?.GetValue(meta) ??
                           meta.GetType().GetField(propName)?.GetValue(meta)!);
        }
        return defaultValue;
        
        ObservableCollection<RssType> GetOrder()
        {
            Type type = typeof(MixedPhraserOrder);
            PropertyInfo? prop =  type.GetProperty($"{propName}Order");
            Debug.Assert(prop != null, nameof(prop) + " != null");
            return (ObservableCollection<RssType>)prop.GetValue(_data.Order)!;
        }
    }
}

public class MixedPhraserOrder
{
    // 版本号，每次添加新搜刮器/添加新字段的时候都应该把这个数字+1，以便galgameCollectionService能够更新配置中已有的顺序配置
    // 更新配置不需要手动编写，已经在GalgameCollectionService中使用反射实现，会自动添加新的默认配置
    public const int Version = 5;
    
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

    public MixedPhraserOrder SetToDefault()
    {
        NameOrder = new() { RssType.Bangumi, RssType.Vndb, RssType.Ymgal };
        DescriptionOrder = new() { RssType.Ymgal, RssType.Bangumi, RssType.Vndb };
        ExpectedPlayTimeOrder = new() { RssType.Vndb};
        RatingOrder = new() { RssType.Bangumi, RssType.Vndb };
        ImageUrlOrder = new() { RssType.Vndb, RssType.Bangumi, RssType.Ymgal };
        ReleaseDateOrder = new() { RssType.Bangumi, RssType.Vndb, RssType.Ymgal };
        CharactersOrder = new() { RssType.Bangumi, RssType.Vndb };
        CnNameOrder = new() { RssType.Bangumi, RssType.Vndb, RssType.Ymgal };
        DeveloperOrder = new() { RssType.Bangumi, RssType.Vndb, RssType.Ymgal };
        TagsOrder = new() { RssType.Bangumi, RssType.Vndb };
        return this;
    }
}

public class MixedPhraserData : IGalInfoPhraserData
{
    public required MixedPhraserOrder Order { get; init; }
}