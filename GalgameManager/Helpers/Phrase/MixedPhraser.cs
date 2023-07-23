using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Helpers.Phrase;

public class MixedPhraser : IGalInfoPhraser
{
    private readonly BgmPhraser _bgmPhraser;
    private readonly VndbPhraser _vndbPhraser;
    
    public MixedPhraser(BgmPhraser bgmPhraser, VndbPhraser vndbPhraser)
    {
        _bgmPhraser = bgmPhraser;
        _vndbPhraser = vndbPhraser;
    }
    
    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        Galgame? bgm = new(), vndb = new();
        bgm.Name = galgame.Name;
        vndb.Name = galgame.Name;
        // 试图从Id中获取bgmId和vndbId
        try
        {
            (string? bgmId, string ? vndbId) tmp = TryGetId(galgame.Id);
            if (tmp.bgmId != null)
            {
                bgm.RssType = RssType.Bangumi;
                bgm.Id = tmp.bgmId;
            }
            if (tmp.vndbId != null)
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
        Galgame result = new();
        result.RssType = RssType.Mixed;
        result.Id = $"bgm:{(bgm == null ? "null" : bgm.Id)},vndb:{(vndb == null ? "null" : vndb.Id)}";
        // name
        result.Name = vndb !=null ? vndb.Name : bgm!.Name;

        result.CnName = bgm != null ? bgm!.CnName:"";

        // description
        result.Description = bgm != null ? bgm.Description : vndb!.Description;
        // developer
        if(bgm != null)
            result.Developer = bgm.Developer;
        // expectedPlayTime
        if(vndb != null)
            result.ExpectedPlayTime = vndb.ExpectedPlayTime;
        // rating
        result.Rating = bgm != null ? bgm.Rating : vndb!.Rating;
        // imageUrl
        result.ImageUrl = vndb != null ? vndb.ImageUrl : bgm!.ImageUrl;
        // tags
        result.Tags = bgm != null ? bgm.Tags : vndb!.Tags;
        return result;
    }

    private static (string? bgmId, string? vndbId) TryGetId(string? id)  //id: bgm:xxx,vndb:xxx
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

    public RssType GetPhraseType() => RssType.Mixed;
}

public class MixedPhraserData : IGalInfoPhraserData
{
}