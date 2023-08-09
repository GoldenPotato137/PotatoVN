using System.Collections.ObjectModel;
using System.Reflection;
using System.Text.Json.Nodes;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Helpers.API;
using GalgameManager.Models;
using Newtonsoft.Json.Linq;
using SharpCompress;

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
    private const string VndbFields = "title, titles.title, titles.lang, description, image.url, id, rating, length, length_minutes, tags.id, tags.rating, developers.original";

    private async Task Init()
    {
        _init = true;
        var assembly = Assembly.GetExecutingAssembly();
        var file = Path.Combine(Path.GetDirectoryName(assembly.Location)!, TagDbFile);
        if (!File.Exists(file)) return;

        var json = JToken.Parse(await File.ReadAllTextAsync(file));
        var tags = json.ToObject<List<JToken>>();
        tags!.ForEach(tag => _tagDb.Add(int.Parse(tag["id"]!.ToString()), tag));
    }

    private async Task TryGetId(Galgame galgame)
    {
        RssType old = galgame.RssType;
        galgame.RssType = GetPhraseType();
        if (string.IsNullOrEmpty(galgame.Id))
        {
            var id = await PhraseHelper.TryGetVndbIdAsync(galgame.Name!);
            if (id is not null)
            {
                galgame.Id = id.ToString();
                return;
            }
        }
        galgame.RssType = old;
    }
    
    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        if (!_init) await Init();
        var result = new Galgame();
        try
        {
            // 试图离线获取ID
            await TryGetId(galgame);

            VndbResponse? visualNovels;
            try
            {
                if (galgame.RssType != RssType.Vndb) throw new Exception();
                var idString = galgame.Id;
                if (!string.IsNullOrEmpty(idString) && idString[0] == 'v')
                    idString = idString[1..];
                visualNovels = await _vndb.GetVisualNovelAsync(new VndbQuery
                {
                    Fields = VndbFields,
                    Filters = VndbFilters.Equal("id", "v"+idString)
                });
                if (visualNovels.Count == 0)
                {
                    visualNovels = await _vndb.GetVisualNovelAsync(new VndbQuery
                        {
                            Fields = VndbFields,
                            Filters = VndbFilters.Equal("search", galgame.Name.Value)
                        });
                }
            }
            catch (VndbApi.ThrottledException)
            {
                await Task.Delay(60 * 1000); // 1 minute
                visualNovels = await _vndb.GetVisualNovelAsync(new VndbQuery
                    {
                        Fields = VndbFields,
                        Filters = VndbFilters.Equal("search", galgame.Name.Value)
                    });
            }
            catch (Exception)
            {
                visualNovels = null;
            }
            
            if (visualNovels == null || visualNovels.Count == 0) return null;
            JsonNode rssItem = visualNovels.Results?[0]!;
            result.Name = rssItem["title"]!.ToString();
            result.CnName = GetChineseName(rssItem["titles"]!.AsArray());
            result.Description = rssItem["description"]!.ToString();
            result.RssType = GetPhraseType();
            // id eg: v16044 -> 16044
            result.Id = rssItem["id"]!.ToString()[1..];
            result.Rating = (float)CheckNotNullToInt(rssItem["rating"]!.ToString());
            result.ExpectedPlayTime = GetLength(rssItem["length"]!.ToString(), 
                rssItem["length_minutes"]!.ToString());
            result.ImageUrl = rssItem["image"]!["url"]!.ToString();
            // Developers
            if (rssItem["developers"]!.AsArray().Count > 0)
            {
                result.Developer = rssItem["developers"]!.AsArray()[0]!["original"]!.ToString();
            }
            // Tags
            result.Tags.Value = new ObservableCollection<string>();
            IOrderedEnumerable<Tag> tmpTags = GetTags(rssItem["tags"]!.AsArray()).OrderByDescending(t => t.Rating);
            tmpTags.ForEach(tag =>
            {
                if (_tagDb.TryGetValue((int)tag.Id, out JToken? tagInfo))
                    result.Tags.Value.Add(tagInfo["name"]!.ToString());
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
        JsonNode? title = titles.FirstOrDefault(t => t!["lang"]!.ToString() == "zh-Hans") ?? titles.FirstOrDefault(t => t!["lang"]!.ToString() == "zh-Hant");
        return title is not null ? title!["title"]!.ToString() : "";
    }

    private static int CheckNotNullToInt(string inString)
    {
        if (string.IsNullOrWhiteSpace(inString) && inString != "null")
        {
            if (int.TryParse(inString, out var outInt)) return outInt;
        }

        return 0;
    }

    private static string GetLength(string? length, string? lengthMinutes)
    {
        if (!string.IsNullOrWhiteSpace(lengthMinutes) && int.TryParse(lengthMinutes, out var lengthInt))
        {
            return (lengthInt > 60?lengthInt / 60 + "h":"") + (lengthInt%60 != 0?lengthInt % 60 + "s":"");
        }
        else if (string.IsNullOrWhiteSpace(length) && int.TryParse(length, out lengthInt))
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
