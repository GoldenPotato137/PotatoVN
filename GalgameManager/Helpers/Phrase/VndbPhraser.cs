using System.Collections.ObjectModel;
using System.Reflection;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Helpers.API;
using GalgameManager.Models;
using Newtonsoft.Json.Linq;

namespace GalgameManager.Helpers.Phrase;

public class VndbPhraser : IGalInfoPhraser
{
    private readonly VndbApi _vndb = new();
    private readonly Dictionary<int, JToken> _tagDb = new();
    private bool _init;
    private const string TagDbFile = @"Assets\Data\vndb-tags-latest.json";
    /// <summary>
    /// id eg:g530[1..]=530=(int)530
    /// </summary>
    private const string VndbFields = "title, titles.title, titles.lang, description, image.url, id, rating, length, " +
                                      "length_minutes, tags.id, tags.rating, developers.original, developers.name, released";

    private async Task Init()
    {
        _init = true;
        Assembly assembly = Assembly.GetExecutingAssembly();
        var file = Path.Combine(Path.GetDirectoryName(assembly.Location)!, TagDbFile);
        if (!File.Exists(file)) return;

        JToken json = JToken.Parse(await File.ReadAllTextAsync(file));
        List<JToken>? tags = json.ToObject<List<JToken>>();
        tags!.ForEach(tag => _tagDb.Add(int.Parse(tag["id"]!.ToString()), tag));
    }

    private static async Task TryGetId(Galgame galgame)
    {
        if (string.IsNullOrEmpty(galgame.Ids[(int)RssType.Vndb]))
        {
            var id = await PhraseHelper.TryGetVndbIdAsync(galgame.Name!);
            if (id is not null)
            {
                galgame.Ids[(int)RssType.Vndb] = id.ToString();
            }
        }
    }
    
    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        if (!_init) await Init();
        Galgame result = new();
        try
        {
            // 试图离线获取ID
            await TryGetId(galgame);

            VndbResponse<VndbVn> vndbResponse;
            try
            {
                // with v
                var idString = galgame.Ids[(int)RssType.Vndb];
                if (string.IsNullOrEmpty(idString))
                {
                    vndbResponse = await _vndb.GetVisualNovelAsync(new VndbQuery
                    {
                        Fields = VndbFields,
                        Filters = VndbFilters.Equal("search", galgame.Name.Value!)
                    });
                }
                else
                {
                    if (!string.IsNullOrEmpty(idString) && idString[0] != 'v')
                        idString = "v"+idString;
                    vndbResponse = await _vndb.GetVisualNovelAsync(new VndbQuery
                    {
                        Fields = VndbFields,
                        Filters = VndbFilters.Equal("id", idString)
                    });
                    if (vndbResponse.Results is null || vndbResponse.Results.Count == 0)
                    {
                        vndbResponse = await _vndb.GetVisualNovelAsync(new VndbQuery
                        {
                            Fields = VndbFields,
                            Filters = VndbFilters.Equal("search", galgame.Name.Value!)
                        });
                    }
                }
            }
            catch (VndbApi.ThrottledException)
            {
                await Task.Delay(60 * 1000); // 1 minute
                vndbResponse = await _vndb.GetVisualNovelAsync(new VndbQuery
                    {
                        Fields = VndbFields,
                        Filters = VndbFilters.Equal("search", galgame.Name.Value!)
                    });
            }
            catch (Exception)
            {
                return null;
            }
            
            if (vndbResponse.Results is null || vndbResponse.Results.Count == 0) return null;
            VndbVn rssItem = vndbResponse.Results[0];
            result.Name = rssItem.Title ?? "";
            result.CnName = GetChineseName(rssItem.Titles);
            result.Description = rssItem.Description ?? Galgame.DefaultString;
            result.RssType = GetPhraseType();
            // id eg: v16044 -> 16044
            var id = rssItem.Id! ;
            result.Id = id.StartsWith("v")?id[1..]:id;
            result.Rating =(float)Math.Round(rssItem.Rating / 10 ?? 0.0D, 1);
            result.ExpectedPlayTime = GetLength(rssItem.Lenth,rssItem.LengthMinutes);
            result.ImageUrl = rssItem.Image != null ? rssItem.Image.Url! :"";
            // Developers
            if (rssItem.Developers?.Count > 0)
            {
                IEnumerable<string> developers = rssItem.Developers.Select<VndbProducer, string>(d =>
                    d.Original ?? d.Name ?? "");
                result.Developer = string.Join(",", developers);
            }else
            {
                result.Developer = Galgame.DefaultString;
            }

            result.ReleaseDate = (rssItem.Released != null
                ? IGalInfoPhraser.GetDateTimeFromString(rssItem.Released)
                : null) ?? DateTime.MinValue;
            // Tags
            result.Tags.Value = new ObservableCollection<string>();
            if (rssItem.Tags != null)
            {
                IOrderedEnumerable<VndbTag> tmpTags = rssItem.Tags.OrderByDescending(t => t.Rating);
                foreach (VndbTag tag in tmpTags)
                {
                    if (!int.TryParse(tag.Id![1..], out var i)) continue;
                    if (_tagDb.TryGetValue(i, out JToken? tagInfo))
                        result.Tags.Value.Add(tagInfo["name"]!.ToString() ?? "");
                }
            }
        }
        catch (Exception)
        {
            return null;
        }
        return result;
    }

    public RssType GetPhraseType() => RssType.Vndb;

    private static string GetChineseName(List<VndbTitle>? titles)
    {
        if (titles == null) return "";
        VndbTitle? title = titles.FirstOrDefault(t => t.Lang == "zh-Hans") ??
                           titles.FirstOrDefault(t => t.Lang == "zh-Hant");
        return title?.Title!;
    }
    private static string GetLength(VndbVn.VnLenth? length, int? lengthMinutes)
    {
        if (lengthMinutes != null)
        {
            return (lengthMinutes > 60?lengthMinutes / 60 + "h":"") + (lengthMinutes%60 != 0?lengthMinutes % 60 + "m":"");
        }

        if (length != null)
        {
            switch (length)
            {
                case VndbVn.VnLenth.VeryShort:
                    return "very short";
                case VndbVn.VnLenth.Short:
                    return "short";
                case VndbVn.VnLenth.Medium:
                    return "medium";
                case VndbVn.VnLenth.Long:
                    return "long";
                case VndbVn.VnLenth.VeryLong:
                    return "very long";
            }
        }

        return Galgame.DefaultString;
    }
}
