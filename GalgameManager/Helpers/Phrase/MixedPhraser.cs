using System.Collections.ObjectModel;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Helpers.Phrase;

public class MixedPhraser : IGalInfoPhraser, IGalCharacterPhraser
{
    private readonly BgmPhraser _bgmPhraser;
    private readonly VndbPhraser _vndbPhraser;
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
    
    public MixedPhraser(BgmPhraser bgmPhraser, VndbPhraser vndbPhraser)
    {
        _bgmPhraser = bgmPhraser;
        _vndbPhraser = vndbPhraser;
        _developerList = new List<string>();
    }
    
    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        if (_init == false)
            Init();
        Galgame? bgm = new(), vndb = new();
        bgm.Name = galgame.Name;
        vndb.Name = galgame.Name;
        // 试图从Id中获取bgmId和vndbId
        try
        {
            (string? bgmId, string ? vndbId) tmp = TryGetId(galgame.Ids[(int)RssType.Mixed]);
            if (string.IsNullOrEmpty(tmp.bgmId) == false)
            {
                bgm.RssType = RssType.Bangumi;
                bgm.Id = tmp.bgmId;
            }
            if (string.IsNullOrEmpty(tmp.vndbId) == false)
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
        
        // 合并信息
        Galgame result = new()
        {
            RssType = RssType.Mixed,
            Id = $"bgm:{(bgm == null ? "null" : bgm.Id)},vndb:{(vndb == null ? "null" : vndb.Id)}",
            // name
            Name = bgm != null ? bgm.Name : vndb!.Name,
            // description
            Description = bgm != null ? bgm.Description : vndb!.Description,
            // expectedPlayTime
            ExpectedPlayTime = vndb != null ? vndb.ExpectedPlayTime: Galgame.DefaultString,
            // rating
            Rating = bgm != null ? bgm.Rating : vndb!.Rating,
            // imageUrl
            ImageUrl = vndb != null ? vndb.ImageUrl : bgm!.ImageUrl,
            // release date
            ReleaseDate = bgm?.ReleaseDate ?? vndb!.ReleaseDate,
            Characters =  (bgm?.Characters.Count > 0 ? bgm?.Characters : vndb?.Characters) ?? new ObservableCollection<GalgameCharacter>()
        };

        // Chinese name
        if (bgm != null && !string.IsNullOrEmpty(bgm.CnName))result.CnName =  bgm.CnName;
        else if (vndb != null && !string.IsNullOrEmpty(vndb.CnName)) result.CnName = vndb.CnName;
        else result.CnName = "";
        
        // developer
        if (bgm != null && bgm.Developer != Galgame.DefaultString)result.Developer = bgm.Developer;
        else if (vndb != null && vndb.Developer != Galgame.DefaultString)result.Developer = vndb.Developer;
        // tags
        result.Tags = bgm != null ? bgm.Tags : vndb!.Tags;
        
        // developer from tag
        if (result.Developer == Galgame.DefaultString)
        {
            var tmp = GetDeveloperFromTags(result);
            if (tmp != null)
                result.Developer = tmp;
        }
        return result;
    }

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
}

public class MixedPhraserData : IGalInfoPhraserData
{
}