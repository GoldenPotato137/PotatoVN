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
    private MixedPhraserData _data;
    private IEnumerable<string> _developerList;
    private bool _init;
    
    private void Init()
    {
        _init = true;
        _developerList = ProducerDataHelper.Producers.SelectMany(p => p.Names);
    }
    
    private string? GetDeveloperFromTags(Galgame galgame)
    {
        if (_init == false)
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
    
    public MixedPhraser(BgmPhraser bgmPhraser, VndbPhraser vndbPhraser, MixedPhraserData data)
    {
        _bgmPhraser = bgmPhraser;
        _vndbPhraser = vndbPhraser;
        _data = data;
        _developerList = new List<string>();
    }
    
    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        if (!_init) Init();
        Galgame? bgm = new(), vndb = new();
        bgm.Name = galgame.Name;
        vndb.Name = galgame.Name;
        // 试图从Id中获取bgmId和vndbId
        try
        {
            (string? bgmId, string ? vndbId) tmp = TryGetId(galgame.Ids[(int)RssType.Mixed]);
            if (!string.IsNullOrEmpty(tmp.bgmId))
            {
                bgm.RssType = RssType.Bangumi;
                bgm.Id = tmp.bgmId;
            }
            if (!string.IsNullOrEmpty(tmp.vndbId))
            {
                vndb.RssType = RssType.Vndb;
                vndb.Id = tmp.vndbId;
            }
        }
        catch (Exception)
        {
            // ignored
        }
        // 从bgm和vndb中获取信息
        bgm = await _bgmPhraser.GetGalgameInfo(bgm);
        vndb = await _vndbPhraser.GetGalgameInfo(vndb);
        if(bgm == null && vndb == null)
            return null;
        Dictionary<RssType, Galgame> metas = new();
        if(bgm is not null) metas[RssType.Bangumi] = bgm;
        if(vndb is not null) metas[RssType.Vndb] = vndb;
        
        // 合并信息
        Galgame result = new();
        result.RssType = RssType.Mixed;
        result.Id = $"bgm:{(bgm == null ? "null" : bgm.Id)},vndb:{(vndb == null ? "null" : vndb.Id)}"; 
        // name
        result.Name = (LockableProperty<string>)GetValue(metas, nameof(Galgame.Name), 
            _ => true, string.Empty);
        // description
        result.Description = (LockableProperty<string>)GetValue(metas, nameof(Galgame.Description), 
            _ => true, string.Empty);
        // expectedPlayTime
        result.ExpectedPlayTime = (LockableProperty<string>)GetValue(metas, nameof(Galgame.ExpectedPlayTime), 
            meta => CheckStr(meta.ExpectedPlayTime.Value), Galgame.DefaultString);
        // rating
        result.Rating = (LockableProperty<float>)GetValue(metas, nameof(Galgame.Rating), 
            _ => true, 0);
        // imageUrl
        result.ImageUrl = (string)GetValue(metas, nameof(Galgame.ImageUrl), 
            meta => CheckStr(meta.ImageUrl), null!);
        // release date
        result.ReleaseDate = (LockableProperty<DateTime>)GetValue(metas, nameof(Galgame.ReleaseDate),
            meta => meta.ReleaseDate != DateTime.MinValue, DateTime.MinValue);
        // characters
        result.Characters = (ObservableCollection<GalgameCharacter>)GetValue(metas, nameof(Galgame.Characters),
            meta => meta.Characters.Count > 0, new ObservableCollection<GalgameCharacter>());
        // Chinese name
        result.CnName = (string)GetValue(metas, nameof(Galgame.CnName),
            meta => CheckStr(meta.CnName), string.Empty);
        // developer
        result.Developer = (LockableProperty<string>)GetValue(metas, nameof(Galgame.Developer),
            meta => CheckStr(meta.Developer), Galgame.DefaultString);
        // tags
        result.Tags = (LockableProperty<ObservableCollection<string>>)GetValue(metas, nameof(Galgame.Tags),
            meta => meta.Tags.Value?.Count > 0, new ObservableCollection<string>());
        
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

    public static (string? bgmId, string? vndbId) TryGetId(string? id)  //id: bgm:xxx,vndb:xxx
    {
        if (id == null || id.Contains("bgm:") == false || id.Contains(",vndb:") == false)
            return (null, null);
        id = id.Replace("bgm:", "").Replace("vndb:", "").Replace(" ","");
        id = id.Replace("，", ","); //替换中文逗号为英文逗号
        var tmp = id.Split(",").ToArray();
        string? bgmId = null, vndbId = null;
        if (tmp[0] != "null") bgmId = tmp[0];
        if (tmp[1] != "null") vndbId = tmp[1];
        return (bgmId, vndbId);
    }

    public static string TrySetId(string str, string? bgmId, string? vndbId)
    {
        (string? bgmId, string? vndbId) lastId = TryGetId(str);
        bgmId = bgmId ?? lastId.bgmId;
        vndbId = vndbId ?? lastId.vndbId;
        return $"bgm:{bgmId},vndb:{vndbId}";
    }

    public RssType GetPhraseType() => RssType.Mixed;

    public async Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter)
    {
        return await _bgmPhraser.GetGalgameCharacter(galgameCharacter);
    }

    private object GetValue(Dictionary<RssType, Galgame> metas, string propName, Func<Galgame, bool> isValueAvailable, 
        object defaultValue)
    {
        ObservableCollection<RssType> order = GetOrder();
        foreach (RssType rssType in order)
        {
            if(!metas.TryGetValue(rssType, out Galgame? meta)) continue;
            if (isValueAvailable(meta))
                return meta.GetType().GetProperty(propName)?.GetValue(meta) ??
                       meta.GetType().GetField(propName)?.GetValue(meta)!;
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
    public const int Version = 4;
    
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
        NameOrder = new() { RssType.Bangumi, RssType.Vndb };
        DescriptionOrder = new() { RssType.Bangumi, RssType.Vndb };
        ExpectedPlayTimeOrder = new() { RssType.Vndb};
        RatingOrder = new() { RssType.Bangumi, RssType.Vndb };
        ImageUrlOrder = new() { RssType.Vndb, RssType.Bangumi };
        ReleaseDateOrder = new() { RssType.Bangumi, RssType.Vndb };
        CharactersOrder = new() { RssType.Bangumi, RssType.Vndb };
        CnNameOrder = new() { RssType.Bangumi, RssType.Vndb };
        DeveloperOrder = new() { RssType.Bangumi, RssType.Vndb };
        TagsOrder = new() { RssType.Bangumi, RssType.Vndb };
        return this;
    }
}

public class MixedPhraserData : IGalInfoPhraserData
{
    public required MixedPhraserOrder Order { get; init; }
}