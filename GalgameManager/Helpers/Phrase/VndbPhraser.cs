using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json.Nodes;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Helpers.API;
using GalgameManager.Models;
using Newtonsoft.Json.Linq;
using SharpCompress;
using JsonArray = System.Text.Json.Nodes.JsonArray;

namespace GalgameManager.Helpers.Phrase;

public class VndbPhraser : IGalInfoPhraser
{
    private readonly VndbApi _vndb = new();
    private readonly Dictionary<int, JToken> _tagDb = new();
    private bool _init;
    private const string TagDbFile = @"Assets\Data\vndb-tags-2023-04-15.json";
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

            VndbResponse vndbResponse;
            try
            {
                // with v
                var idString = galgame.Ids[(int)RssType.Vndb];
                if (string.IsNullOrEmpty(idString))
                {
                    vndbResponse = await _vndb.GetVisualNovelAsync(new VndbQuery
                    {
                        Fields = VndbFields,
                        Filters = VndbFilters.Equal("search", galgame.Name.Value)
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
                            Filters = VndbFilters.Equal("search", galgame.Name.Value)
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
                        Filters = VndbFilters.Equal("search", galgame.Name.Value)
                    });
            }
            catch (Exception)
            {
                return null;
            }
            
            if (vndbResponse.Results is null || vndbResponse.Results.Count == 0) return null;
            JsonNode rssItem = vndbResponse.Results[0]!;
            result.Name = CheckNotNullToString(rssItem["title"]);
            result.CnName = GetChineseName(rssItem["titles"]!.AsArray());
            result.Description = CheckNotNullToString(rssItem["description"], Galgame.DefaultString);
            result.RssType = GetPhraseType();
            // id eg: v16044 -> 16044
            var id = CheckNotNullToString(rssItem["id"]);
            result.Id = id.StartsWith("v")?id[1..]:id;
            result.Rating = (float)CheckNotNullToInt(rssItem["rating"]);
            result.ExpectedPlayTime = GetLength(rssItem["length"],rssItem["length_minutes"]);
            result.ImageUrl = rssItem["image"] != null ? CheckNotNullToString(rssItem["image"]!["url"]):"";
            // Developers
            if (rssItem["developers"]!.AsArray().Count > 0)
            {
                IEnumerable<string> developers = rssItem["developers"]!.AsArray().Select<JsonNode?, string>(d =>
                    CheckNotNullToString(d!["original"], d["name"]!.ToString()));
                result.Developer = string.Join(",", developers);
            }else
            {
                result.Developer = Galgame.DefaultString;
            }

            result.ReleaseDate = (rssItem["released"] != null
                ? IGalInfoPhraser.GetDateTimeFromString(rssItem["released"]!.ToString())
                : null) ?? DateTime.MinValue;
            // Tags
            result.Tags.Value = new ObservableCollection<string>();
            IOrderedEnumerable<Tag> tmpTags = GetTags(rssItem["tags"]!.AsArray()).OrderByDescending(t => t.Rating);
            tmpTags.ForEach(tag =>
            {
                if (_tagDb.TryGetValue(tag.Id, out JToken? tagInfo))
                    result.Tags.Value.Add(CheckNotNullToString(tagInfo["name"]));
            });
        }
        catch (Exception)
        {
            return null;
        }
        return result;
    }

    public RssType GetPhraseType() => RssType.Vndb;

    private static string GetChineseName(JsonArray titles)
    {
        JsonNode? title = titles.FirstOrDefault(t => t!["lang"]!.ToString() == "zh-Hans") ??
                          titles.FirstOrDefault(t => t!["lang"]!.ToString() == "zh-Hant");
        return title is not null ? title["title"]!.ToString() : "";
    }

    private static int CheckNotNullToInt(JsonNode? jsonNode)
    {
        if (jsonNode is not null)
        {
            var inString = jsonNode.ToString();
            if (string.IsNullOrWhiteSpace(inString) && inString != "null")
            {
                if (int.TryParse(inString, out var outInt)) return outInt;
            }
        }

        return 0;
    }
    
    private static string CheckNotNullToString(JsonNode? jsonNode, string defaultString = "")
    {
        return jsonNode is not null ? jsonNode.AsValue().GetValue<string>() : defaultString;
    }
    
    private static string CheckNotNullToString(JToken? jsonNode, string defaultString = "")
    {
        return jsonNode is not null ? jsonNode.Value<string>() ?? "" : defaultString;
    }

    private static string GetLength(JsonNode? length, JsonNode? lengthMinutes)
    {
        if (lengthMinutes != null && int.TryParse(lengthMinutes.ToString(), out var lengthInt))
        {
            return (lengthInt > 60?lengthInt / 60 + "h":"") + (lengthInt%60 != 0?lengthInt % 60 + "s":"");
        }

        if (length != null && int.TryParse(length.ToString(), out lengthInt))
        {
            switch (lengthInt)
            {
                case 1:
                    return "very short";
                case 2:
                    return "short";
                case 3:
                    return "medium";
                case 4:
                    return "long";
                case 5:
                    return "very long";
            }
        }

        return Galgame.DefaultString;
    }

    private static List<Tag> GetTags(JsonArray tags)
    {
        List<Tag> tagsList = new();
        foreach (JsonNode? tag in tags)
        {
            if (tag is not null)
            {
                float.TryParse(tag["rating"]!.ToString(), out var rating);
                // eg g1212->1212
                int.TryParse(tag["id"]!.ToString()[1..], out var id);
                tagsList.Add(new Tag
                {
                    Id = id,
                    Rating = rating
                });
            }
        }

        return tagsList;
    }

    public struct Tag
    {
        public int Id;
        public float Rating;
    }
}
