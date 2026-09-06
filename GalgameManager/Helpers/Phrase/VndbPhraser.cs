using System.Collections.ObjectModel;
using System.Reflection;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Helpers.API;
using GalgameManager.Models;
using Newtonsoft.Json.Linq;
using PotatoDBMapper.Models;
using Staff = GalgameManager.Models.Staff;
using HtmlAgilityPack;
using GalgameManager.Helpers;

namespace GalgameManager.Helpers.Phrase;

public class VndbPhraser : IGalInfoPhraser, IGalStatusSync, IGalCharacterPhraser, IGalStaffParser, IGalHeaderParser, IGalCoversParser, IGalHeadersParser
{
    private VndbApi _vndbApi;

    private readonly Dictionary<int, JToken> _tagDb = new();
    private readonly Dictionary<string, string> _releaseToVndbId = [];
    private readonly Dictionary<string, VndbRelease> _releaseCache = []; //因为这个被频繁使用，缓存下来
    private bool _init;
    private const string TagDbFile = @"Assets\Data\vndb-tags-latest.json";
    // 标签翻译文件来源: https://greasyfork.org/zh-CN/scripts/445990-vndbtranslatorlib
    // 作者: rui li 2
    // 协议: MIT
    private const string TagTranslationFile = @"Assets\Data\vndb-tags-translation.json";
    /// <summary>
    /// id eg:g530[1..]=530=(int)530
    /// </summary>
    private const string VndbFields = "title, titles.title, titles.lang, description, image.url, id, rating, length, " +
                                      "length_minutes, tags.id, tags.rating, tags.spoiler, developers.original, developers.name, released";
    private const string ReleaseFields = "id, languages.lang, languages.title, vns.id, producers.developer, " +
                                         "producers.publisher, producers.original, images.url, released";
    private const string StaffFields = "id, aid, name, original, lang, gender, description";
    private const string SearchRankSort = "searchrank";

    private bool _authed;
    private bool _isChineseCulture = true;
    private bool _translateTags = true;
    private bool _censorTags = true;
    private bool _removeSpoilerTags = true;
    private Task? _checkAuthTask;

    public VndbPhraser()
    {
        _vndbApi = new VndbApi();
    }

    public VndbPhraser(VndbPhraserData data)
    {
        _vndbApi = new VndbApi();
        UpdateData(data);
    }

    public void UpdateData(IGalInfoPhraserData data)
    {
        if (data is VndbPhraserData vndbData)
        {
            _checkAuthTask = Task.Run(async () =>
            {
                _vndbApi.UpdateToken(vndbData.Token);
                try
                {
                    await _vndbApi.GetAuthInfo();
                    _authed = true;
                }
                catch (InvalidTokenException)
                {
                    _authed = false;
                    _vndbApi.UpdateToken(null);
                }
                catch (Exception)
                {
                    _authed = false; //todo:修复该phraser
                }
            });

            // 更新语言
            _isChineseCulture = vndbData.IsChineseCulture;
            _translateTags = vndbData.TranslateTags;
            _censorTags = vndbData.CensorTags;
            _removeSpoilerTags = vndbData.RemoveSpoilerTags;
            _init = false;
        }
    }

    public static int GetId(string id)
    {
        if (id.StartsWith("v")) return int.Parse(id[1..]);
        if (int.TryParse(id, out var i)) return i;
        return 0;
    }

    private async Task Init()
    {
        _init = true;
        Assembly assembly = Assembly.GetExecutingAssembly();
        var file = Path.Combine(Path.GetDirectoryName(assembly.Location)!, TagDbFile);
        if (!File.Exists(file)) return;

        _tagDb.Clear();
        JToken json = JToken.Parse(await File.ReadAllTextAsync(file));
        List<JToken>? tags = json.ToObject<List<JToken>>();
        tags!.ForEach(tag => _tagDb.Add(int.Parse(tag["id"]!.ToString()), tag));

         // 如果是中文，并且开启了翻译，则应用翻译
        if (_isChineseCulture && _translateTags)
        {
            // 加载翻译文件
            var translationFile = Path.Combine(Path.GetDirectoryName(assembly.Location)!, TagTranslationFile);
            if (!File.Exists(translationFile)) return;

            try
            {
                JToken translationJson = JObject.Parse(await File.ReadAllTextAsync(translationFile));

                // 遍历所有标签，应用翻译
                foreach (var tag in _tagDb.Values)
                {
                    string? originalName = tag["name"]?.ToString();
                    if (originalName != null && translationJson[originalName] != null)
                    {
                        tag["name"] = translationJson[originalName]!.ToString();
                    }
                    // 保存原始名称，以便在需要时能够访问
                    if (originalName != null)
                    {
                        tag["original_name"] = originalName;
                    }
                }
            }
            catch (Exception)
            {
                // 翻译过程中出错，但不影响基本功能
            }
        }
    }

    private static async Task TryGetId(Galgame galgame)
    {
        MapModel? map = await PhraseHelper.TryGetMapAsync(galgame);
        if (map is null) return;
        galgame.Ids[(int)RssType.Vndb] = map.VndbId.ToString();
    }

    private static VndbQuery CreateVnNameSearchQuery(string name)
    {
        return new VndbQuery
        {
            Fields = VndbFields,
            Filters = VndbFilters.Equal("search", name),
            Sort = SearchRankSort,
            Results = 1
        };
    }

    private Task<VndbResponse<VndbVn>?> SearchVnByNameAsync(string name)
    {
        return CallVndbApiAsync(() => _vndbApi.GetVisualNovelAsync(CreateVnNameSearchQuery(name)));
    }

    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        if (!_init) await Init();
        Galgame result = new();
        try
        {
            var releaseId = galgame.Ids[(int)RssType.Vndb];
            if (!(releaseId ?? string.Empty).StartsWith('r')) releaseId = null;
            // 试图离线获取ID
            if (releaseId is null) //只有非R字头ID才走离线获取ID表
                await TryGetId(galgame);

            VndbRelease? release = null;
            if (releaseId is not null) release = await GetReleaseAsync(releaseId);

            // with v
            var idString = releaseId is null ? galgame.Ids[(int)RssType.Vndb] : await GetVndbIdFromReleaseId(releaseId);
            VndbResponse<VndbVn>? vndbResponse;
            if (string.IsNullOrEmpty(idString))
            {
                vndbResponse = await SearchVnByNameAsync(galgame.Name.Value!);
            }
            else
            {
                if (idString[0] != 'v')
                    idString = "v" + idString;
                vndbResponse = await CallVndbApiAsync(() => _vndbApi.GetVisualNovelAsync(new VndbQuery
                {
                    Fields = VndbFields,
                    Filters = VndbFilters.Equal("id", idString)
                }));
                if (vndbResponse is null) return null;
                if (vndbResponse.Results is null || vndbResponse.Results.Count == 0)
                {
                    vndbResponse = await SearchVnByNameAsync(galgame.Name.Value!);
                }
            }

            if (vndbResponse?.Results is null || vndbResponse.Results.Count == 0) return null;
            VndbVn rssItem = vndbResponse.Results[0];
            result.Name = GetJapaneseName(rssItem.Titles) ?? rssItem.Title ?? Galgame.DefaultString;
            result.Name.Value = GetTitle(release?.Languages, "ja") ?? result.Name.Value; //优先用released的标题
            result.CnName = GetChineseName(rssItem.Titles);
            result.CnName = GetTitle(release?.Languages, "zh-Hans") ??
                            GetTitle(release?.Languages, "zh-Hant") ?? result.CnName;
            result.Description = rssItem.Description ?? Galgame.DefaultString;
            result.RssType = GetPhraseType();
            // id eg: v16044 -> 16044
            var id = rssItem.Id! ;
            result.Id = id.StartsWith("v")?id[1..]:id;
            result.Id = release?.Id ?? result.Id; // 优先使用releaseId
            result.Rating =(float)Math.Round(rssItem.Rating / 10 ?? 0.0D, 1);
            result.ExpectedPlayTime = GetLength(rssItem.Lenth,rssItem.LengthMinutes);
            result.ImageUrl = rssItem.Image != null ? rssItem.Image.Url! :"";
            result.ImageUrl = release?.Images?.FirstOrDefault(i => !string.IsNullOrEmpty(i.Url))?.Url ?? result.ImageUrl;
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
            // Engine
            var vnIdForEngine = id.StartsWith("v") ? id : "v" + id;
            try
            {
                var engine = await GetEngineAsync(vnIdForEngine);
                result.Engine.Value = engine ?? string.Empty;
            }
            catch (Exception)
            {
                result.Engine.Value = string.Empty;
            }
            // Tags
            result.Tags.Value = new ObservableCollection<string>();
            if (rssItem.Tags != null)
            {
                IEnumerable<VnTag> tmpTags = rssItem.Tags.OrderByDescending(t => t.Rating);
                if (_removeSpoilerTags)
                    tmpTags = tmpTags.Where(t => t.Spoiler == null || t.Spoiler <= 1);

                foreach (VndbTag tag in tmpTags)
                {
                    if (!int.TryParse(tag.Id![1..], out var i)) continue;
                    if (_tagDb.TryGetValue(i, out JToken? tagInfo))
                    {
                        var category = tagInfo["cat"]?.ToString() ?? string.Empty;
                        if (category == "tech") continue;
                        if (_censorTags && category == "ero") continue;
                        if (category != "cont" && category != "ero") continue;
                        result.Tags.Value.Add(tagInfo["name"]!.ToString() ?? "");
                    }
                }
            }
            // Characters
            try
            {
                VndbResponse<VndbCharacter> vndbCharacterResponse = await _vndbApi.GetVnCharacterAsync(new VndbQuery
                {
                    Filters = VndbFilters.Equal("vn", VndbFilters.Equal("id", id)),
                    Fields = "id, name, original, vns.id, vns.role"
                });
                if (vndbCharacterResponse.Results is not null && vndbResponse.Results.Count != 0)
                {
                    foreach (VndbCharacter character in vndbCharacterResponse.Results)
                    {
                        GalgameCharacter c = new()
                        {
                            Name = character.Original ?? character.Name ?? "",
                            Ids =
                            {
                                [(int)GetPhraseType()] =
                                    character.Id!.StartsWith("v") ? character.Id[1..] : character.Id
                            }
                        };
                        List<VndbVn.VndbRole?>? vns = character.Vns?.Where(vn => vn.Id == id).Select(vn => vn.Role)
                            .ToList();
                        if (vns is { Count: > 0 })
                        {
                            c.Relation = vns[0] switch
                            {
                                VndbVn.VndbRole.Main => "主角",
                                VndbVn.VndbRole.Primary => "主要人物",
                                VndbVn.VndbRole.Side => "次要人物",
                                VndbVn.VndbRole.Appears => "仅出现",
                                _ => "-"
                            };
                        }

                        result.Characters.Add(c);
                    }
                }
            }
            catch
            {
                return result;
            }
        }
        catch (Exception)
        {
            return null;
        }
        return result;

        string? GetTitle(List<VndbLanguage>? languages, string targetLoc)
        {
            if (languages == null) return null;
            VndbLanguage? lang = languages.FirstOrDefault(l => l.Lang == targetLoc);
            if (string.IsNullOrEmpty(lang?.Title)) return null;
            return lang.Title;
        }
    }

    /// <summary>
    /// 获取封面图片，复用 GetGalgameImagesAsync 的实现
    /// </summary>
    public async Task<List<string>> GetGalCoversAsync(Galgame galgame)
    {
        List<string> result = [];
        if (string.IsNullOrEmpty(galgame.Ids[(int)RssType.Vndb])) await TryGetId(galgame);
        if (string.IsNullOrEmpty(galgame.Ids[(int)RssType.Vndb])) return result;

        var idString = galgame.Ids[(int)RssType.Vndb];
        if (string.IsNullOrEmpty(idString)) return [];
        if (idString[0] != 'v') idString = "v" + idString;

        // First, get the main cover image from API
        VndbResponse<VndbVn>? vndbResponse = await CallVndbApiAsync(() => _vndbApi.GetVisualNovelAsync(new VndbQuery
        {
            Fields = "image.url",
            Filters = VndbFilters.Equal("id", idString),
        }));

        if (vndbResponse?.Results?.Count > 0)
        {
            VndbVn rssItem = vndbResponse.Results[0];
            if (rssItem.Image?.Url is not null)
                result.Add(rssItem.Image.Url);
        }

        // Then, scrape all cover images from the VNDB website
        try
        {
            var url = $"https://vndb.org/{idString}/cv";
            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync(url);

            // Find all <a> tags with href containing /cv/
            var nodes = doc.DocumentNode.SelectNodes("//a[contains(@href,'/cv/')]");
            if (nodes != null)
            {
                var foundImages = new HashSet<string>();

                foreach (var node in nodes)
                {
                    var href = node.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(href))
                    {
                        // Convert relative URLs to absolute
                        if (href.StartsWith("//"))
                            href = "https:" + href;
                        else if (href.StartsWith("/"))
                            href = "https://vndb.org" + href;

                        // Only add /cv/ images
                        if (href.Contains("/cv/"))
                        {
                            foundImages.Add(href);
                        }
                    }
                }

                result.AddRange(foundImages);
            }
        }
        catch (Exception ex)
        {
            // If web scraping fails, we still have the main cover from API
            System.Diagnostics.Debug.WriteLine($"Failed to scrape VNDB covers: {ex.Message}");
        }

        return result.Distinct().ToList();
    }

    public RssType GetPhraseType() => RssType.Vndb;

    public async Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter)
    {
        var id = galgameCharacter.Ids[(int)GetPhraseType()];
        if (id == null) return null;
        return await GetCharacterById(id);
    }

    private async Task<GalgameCharacter?> GetCharacterById(string id)
    {
        VndbResponse<VndbCharacter> characterResponse = await _vndbApi.GetVnCharacterAsync(new VndbQuery
        {
            Fields =
                "id, name, original, aliases, description, image.url, blood_type, height, weight, bust, waist, hips, cup, age, birthday, sex, vns.id, vns.role",
            Filters = VndbFilters.Equal("id", id.StartsWith("c")?id:$"c{id}")
        });
        if (characterResponse.Count < 1 || characterResponse.Results == null ||
            characterResponse.Results.Count < 1) return null;
        VndbCharacter vnCharacter = characterResponse.Results[0];
        GalgameCharacter character = new()
        {
            Name = vnCharacter.Original ?? vnCharacter.Name ?? "",
            PreviewImageUrl = vnCharacter.Image?.Url,
            ImageUrl = vnCharacter.Image?.Url,
            Summary = vnCharacter.Description ?? "-",
            Gender = vnCharacter.Sex?[1] switch
            {
                "m" => Gender.Male,
                "f" => Gender.Female,
                _ => Gender.Unknown
            },
            Height = vnCharacter.Height!=null?$"{vnCharacter.Height}cm":"-",
            Weight = vnCharacter.Weight!=null?$"{vnCharacter.Weight}cm":"-",
            BWH = vnCharacter.Bust!=null?$"B{vnCharacter.Bust}({vnCharacter.Cup})/W{vnCharacter.Waist}/H{vnCharacter.Hips}":"-",
            BloodType = vnCharacter.BloodType,
            BirthMon = vnCharacter.Birthday?[0],
            BirthDay = vnCharacter.Birthday?[1],
            BirthDate = vnCharacter.Birthday != null ? $"{vnCharacter.Birthday?[0]}月{vnCharacter.Birthday?[1]}日":"-"
        };
        return character;
    }
    private static string GetChineseName(IReadOnlyCollection<VndbTitle>? titles)
    {
        if (titles == null) return "";
        VndbTitle? title = titles.FirstOrDefault(t => t.Lang == "zh-Hans") ??
                           titles.FirstOrDefault(t => t.Lang == "zh-Hant");
        return title?.Title!;
    }
    private static string? GetJapaneseName(IReadOnlyCollection<VndbTitle>? titles)
    {
        if (titles == null) return null;
        VndbTitle? title = titles.FirstOrDefault(t => t.Lang == "ja");
        return title?.Title;
    }

    private static string GetLength(VndbVn.VnLenth? length, int? lengthMinutes)
    {
        if (lengthMinutes != null)
        {
            return (lengthMinutes > 60?lengthMinutes / 60 + "h":"") + (lengthMinutes%60 != 0?lengthMinutes % 60 + "m":"");
        }

        if (length == null) return Galgame.DefaultString;
        return length switch
        {
            VndbVn.VnLenth.VeryShort => "very short",
            VndbVn.VnLenth.Short => "short",
            VndbVn.VnLenth.Medium => "medium",
            VndbVn.VnLenth.Long => "long",
            VndbVn.VnLenth.VeryLong => "very long",
            _ => Galgame.DefaultString
        };
    }

    public async Task<GalgameCharacter?> GetGalgameCharacterByName(string name)
    {
        VndbResponse<VndbCharacter> characterResponse = await _vndbApi.GetVnCharacterAsync(new VndbQuery
        {
            Fields =
                "id, name, original, aliases, description, image.url, blood_type, height, weight, bust, waist, hips, cup, age, birthday, sex, vns.id, vns.role",
            Filters = VndbFilters.Equal("search", name)
        });
        if (characterResponse.Count < 1 || characterResponse.Results == null ||
            characterResponse.Results.Count < 1) return null;
        VndbCharacter vnCharacter = characterResponse.Results[0];
        GalgameCharacter character = new()
        {
            Name = vnCharacter.Name ?? "",
            PreviewImageUrl = vnCharacter.Image?.Url,
            ImageUrl = vnCharacter.Image?.Url,
            Summary = vnCharacter.Description ?? "",
            Gender = vnCharacter.Sex?[1] switch
            {
                "m" => Gender.Male,
                "f" => Gender.Female,
                _ => Gender.Unknown
            },
            Height = $"{vnCharacter.Height}cm",
            Weight = $"{vnCharacter.Weight}cm",
            BWH = $"B{vnCharacter.Bust}({vnCharacter.Cup})/W{vnCharacter.Waist}/H{vnCharacter.Hips}",
            BloodType = vnCharacter.BloodType,
            BirthMon = vnCharacter.Birthday?[0],
            BirthDay = vnCharacter.Birthday?[1],
            BirthDate = vnCharacter.Birthday != null ? $"{vnCharacter.Birthday?[0]}月{vnCharacter.Birthday?[1]}日":"-"
        };
        return character;
    }

    /// <summary>
    /// 获取releaseId->VndbId的映射
    /// </summary>
    /// <param name="releaseId"></param>
    /// <param name="retry">不用填入这个字段</param>
    /// <returns></returns>
    public async Task<string?> GetVndbIdFromReleaseId(string? releaseId, bool retry = false)
    {
        if (string.IsNullOrEmpty(releaseId)) return null;
        var result =  _releaseToVndbId.GetValueOrDefault(releaseId);
        if (result is not null) return result;
        try
        {
            VndbRelease? release = await GetReleaseAsync(releaseId);
            if (release?.Vns?.Count > 0)
            {
                result = release.Vns[0].Id;
                if (!string.IsNullOrEmpty(result)) _releaseToVndbId[releaseId] = result; // 缓存结果
            }
        }
        catch (Exception e)
        {
            if (e is not  ThrottledException || retry) return null;
            await Task.Delay(60 * 1000); // 1 minute
            return await GetVndbIdFromReleaseId(releaseId, true);
        }
        return result;
    }

    private async Task<VndbRelease?> GetReleaseAsync(string? releaseId, bool retry = false)
    {
        if (releaseId == null) return null;
        if (_releaseCache.TryGetValue(releaseId, out VndbRelease? cachedRelease))
            return cachedRelease;
        try
        {
            VndbResponse<VndbRelease> response = await _vndbApi.GetReleaseAsync(new VndbQuery
            {
                Fields = ReleaseFields,
                Filters = VndbFilters.Equal("id", releaseId)
            });
            if (response.Results == null || response.Results.Count == 0) return null;
            VndbRelease release = response.Results[0];
            _releaseCache[releaseId] = release; // 缓存结果
            return release;
        }
        catch (Exception e)
        {
            if (e is not ThrottledException || retry) return null;
            await Task.Delay(60 * 1000); // 1 minute
            return await GetReleaseAsync(releaseId, true);
        }
    }

    /// <summary>
    /// 获取游戏引擎信息
    /// </summary>
    private async Task<string?> GetEngineAsync(string? vnId, bool retry = false)
    {
        if (string.IsNullOrEmpty(vnId)) return null;
        try
        {
            VndbResponse<VndbRelease> response = await _vndbApi.GetReleaseAsync(new VndbQuery
            {
                Fields = "engine",
                Filters = VndbFilters.And(
                    VndbFilters.Equal("vn", VndbFilters.Equal("id", vnId)),
                    VndbFilters.Equal("official", true),
                    VndbFilters.Equal("platform", "win"),
                    VndbFilters.NotEqual("released", "TBA"),
                    VndbFilters.Equal("rtype", "complete")
                ),
                Results = 100,
                Sort = "released",
                Reverse = true
            });
            if (response.Results == null || response.Results.Count == 0) return null;
            // 返回第一个有引擎值的记录
            var engine = response.Results.FirstOrDefault(r => !string.IsNullOrEmpty(r.Engine))?.Engine;
            return engine;
        }
        catch (Exception e)
        {
            if (e is not ThrottledException || retry) return null;
            await Task.Delay(60 * 1000); // 1 minute
            return await GetEngineAsync(vnId, true);
        }
    }

    public async Task<(GalStatusSyncResult, string)> UploadAsync(Galgame galgame)
    {
        if (_checkAuthTask != null) await _checkAuthTask;
        if (!_authed) return (GalStatusSyncResult.UnAuthorized, "VndbPhraser_UnAuthorized".GetLocalized());
        if (string.IsNullOrEmpty(galgame.Ids[(int)RssType.Vndb]))
            return (GalStatusSyncResult.NoId, "VndbPhraser_NoId".GetLocalized());
        var id = galgame.Ids[(int)RssType.Vndb]!.StartsWith("v")
            ? galgame.Ids[(int)RssType.Vndb]!
            : "v" + galgame.Ids[(int)RssType.Vndb]!;

        try
        {
            // 先尝试读取
            VndbResponse<VndbUserListItem> tryGetResponse = await _vndbApi.GetUserVisualNovelListAsync(new VndbQuery
            {
                Fields = "vote, labels.id", Filters = VndbFilters.Equal("id", id)
            });
            var labelSet = galgame.PlayType.ToVndbCollectionType();
            PatchUserListRequest patchUserListRequest = new()
            {
                LabelsSet = new List<int> {labelSet},
                Notes = galgame.Comment,
                Vote = galgame.MyRate == 0 ? null : galgame.MyRate * 10 // BgmRate: 0~10, VndbRate: 10~100, vndb的一个奇怪的点, 它网站上是 0~10
                // Vndb无private选项
            };
            if (tryGetResponse.Results?.Count == 1)
            {
                patchUserListRequest.LabelsUnset = new List<int>();
                // 去除旧标签
                foreach (UserLabel userListItem in tryGetResponse.Results![0].Labels!)
                {
                    if (userListItem.Id is <= 6 and >= 1 && userListItem.Id != labelSet)
                        patchUserListRequest.LabelsUnset.Add(userListItem.Id);
                }
            }

            await _vndbApi.ModifyUserVnAsync(id, patchUserListRequest);
        }
        catch (Exception e)
        {
            return (GalStatusSyncResult.Other, e.Message);
        }
        return (GalStatusSyncResult.Ok, "VndbPhraser_UploadAsync_Success".GetLocalized());
    }

    public async Task<(GalStatusSyncResult, string)> DownloadAsync(Galgame galgame)
    {
        if (_checkAuthTask != null) await _checkAuthTask;
        if (!_authed) return (GalStatusSyncResult.UnAuthorized, "VndbPhraser_UnAuthorized".GetLocalized());
        if (string.IsNullOrEmpty(galgame.Ids[(int)RssType.Vndb]))
            return (GalStatusSyncResult.NoId, "VndbPhraser_NoId".GetLocalized());
        var id = galgame.Ids[(int)RssType.Vndb]!.StartsWith("v")
            ? galgame.Ids[(int)RssType.Vndb]!
            : "v" + galgame.Ids[(int)RssType.Vndb]!;
        try
        {
            VndbResponse<VndbUserListItem> response = await _vndbApi.GetUserVisualNovelListAsync(new VndbQuery
            {
                Fields = "vote, labels.id, notes", Filters = VndbFilters.Equal("id", id)
            });

            if (response.Results?.Count != 1)
                return (GalStatusSyncResult.Ok, "VndbPhraser_DownloadAsync_Success".GetLocalized());

            VndbUserListItem r = response.Results[0];
            if (r.Vote.HasValue) galgame.MyRate = r.Vote.Value / 10;
            if (r.Notes != null) galgame.Description = r.Notes;
            if (r.Labels != null) galgame.PlayType = r.Labels.First(l=>l.Id is <= 6 and >= 1).Id.VndbCollectionTypeToPlayType();
        }
        catch (Exception e)
        {
            return (GalStatusSyncResult.Other, e.Message);
        }
        return (GalStatusSyncResult.Ok, "VndbPhraser_DownloadAsync_Success".GetLocalized());

    }

    public async Task<(GalStatusSyncResult, string)> DownloadAllAsync(IList<Galgame> galgames)
    {
        if (_checkAuthTask != null) await _checkAuthTask;
        if (!_authed) return (GalStatusSyncResult.UnAuthorized, "VndbPhraser_UnAuthorized".GetLocalized());
        try
        {
            VndbResponse<VndbUserListItem> response = await _vndbApi.GetUserVisualNovelListAsync(new VndbQuery
            {
                Fields = "vote, labels.id, notes"
            });
            if (response.Results == null || response.Results.Count == 0) return (GalStatusSyncResult.Ok, "VndbPhraser_UploadAsync_Success".GetLocalized());
            foreach (VndbUserListItem listItem in response.Results)
            {
                Galgame? galgame = galgames.FirstOrDefault(g => g.Ids[(int)RssType.Bangumi] == listItem.Id?[1..]);
                if (galgame == null)continue;
                if (listItem.Vote.HasValue) galgame.MyRate = listItem.Vote.Value / 10;
                if (listItem.Notes != null) galgame.Description = listItem.Notes;
                if (listItem.Labels != null) galgame.PlayType = listItem.Labels.First(l=>l.Id is <= 6 and >= 1).Id.VndbCollectionTypeToPlayType();
            }
        }
        catch (Exception e)
        {
            return (GalStatusSyncResult.Other, e.Message);
        }
        return (GalStatusSyncResult.Ok, "VndbPhraser_DownloadAsync_Success".GetLocalized());
    }

    public async Task<Staff?> GetStaffAsync(Staff staff)
    {
        var id = staff.Ids[(int)GetPhraseType()];
        if (id is null && staff.Name is null) return null;
        VndbResponse<VndbStaff>? vndbResponse = await CallVndbApiAsync(() => _vndbApi.GetStaffAsync(new VndbQuery
        {
            Fields = StaffFields,
            Filters = id is null ? VndbFilters.Equal("search", staff.Name!) : VndbFilters.Equal("id", id),
        }));
        if (vndbResponse is null) return null;
        VndbStaff? rssItem = (vndbResponse.Results ?? []).FirstOrDefault(s => s.Id == id || s.Name == staff.Name
            || s.Original == staff.Name);
        if (rssItem is null) return null;
        Staff result = new()
        {
            Ids = { [(int)GetPhraseType()] = rssItem.Id },
            EnglishName = rssItem.Name,
            JapaneseName = rssItem.Original,
            Gender = rssItem.Gender switch
            {
                "f" => Gender.Female,
                "m" => Gender.Male,
                _ => Gender.Unknown
            },
            Description = rssItem.Description,
        };
        return result;
    }

    public async Task<List<StaffRelation>> GetStaffsAsync(Galgame game)
    {
        if (!_init) await Init();
        if (string.IsNullOrEmpty(game.Ids[(int)RssType.Vndb])) await TryGetId(game);
        if (string.IsNullOrEmpty(game.Ids[(int)RssType.Vndb])) return new List<StaffRelation>();

        var id = game.Ids[(int)RssType.Vndb]!.StartsWith('v')
            ? game.Ids[(int)RssType.Vndb]!
            : "v" + game.Ids[(int)RssType.Vndb]!;
        List<StaffRelation> result = [];

        List<string> filter = StaffFields.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => $"staff.{s.Trim()}").ToList();
        filter.AddRange(["staff.eid","staff.role", "staff.note"]);
        filter.AddRange(StaffFields.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => $"va.staff.{s.Trim()}").Append("va.note"));
        var fieldStr = string.Join(", ", filter);
        VndbResponse<VndbVn>? vndbResponse = await CallVndbApiAsync(() => _vndbApi.GetVisualNovelAsync(new VndbQuery
        {
            Fields = fieldStr,
            Filters = VndbFilters.Equal("id", id),
        }));
        if (!(vndbResponse is null || vndbResponse.Results is null || vndbResponse.Results.Count == 0))
        {
            VndbVn rssItem = vndbResponse.Results[0];
            result.AddRange((rssItem.Staff ?? []).Select(staff => GetStaffRelation(staff,
                staff.Role switch
                {
                    VnStaff.StaffRole.Scenario => Career.Writer,
                    VnStaff.StaffRole.Artist => Career.Painter,
                    VnStaff.StaffRole.Vocals or VnStaff.StaffRole.Composer => Career.Musician,
                    _ => Career.Unknown,
                })));
            result.AddRange((rssItem.Va ?? []).Where(v => v.Staff is not null)
                .Select(va => GetStaffRelation(va.Staff, Career.Seiyu)));
        }
        return result;

        StaffRelation GetStaffRelation(VndbStaff? staff, Career relation)
        {
            return new StaffRelation
            {
                Ids = { [(int)GetPhraseType()] = staff?.Id },
                EnglishName = staff?.Name,
                JapaneseName = staff?.Original,
                Gender = staff?.Gender switch
                {
                    "f" => Gender.Female,
                    "m" => Gender.Male,
                    _ => Gender.Unknown
                },
                Description = staff?.Description,
                Relation = [relation],
            };
        }
    }

    public async Task<string?> GetGalHeaderAsync(Galgame game)
    {
        if (string.IsNullOrEmpty(game.Ids[(int)RssType.Vndb])) await TryGetId(game);
        if (string.IsNullOrEmpty(game.Ids[(int)RssType.Vndb])) return null;
        var id = game.Ids[(int)RssType.Vndb]!.StartsWith('v')
            ? game.Ids[(int)RssType.Vndb]!
            : "v" + game.Ids[(int)RssType.Vndb]!;
        if (id.StartsWith('r')) id = await GetVndbIdFromReleaseId(id) ?? string.Empty; // 如果是releaseId，换成vndbId
        if (string.IsNullOrEmpty(id)) return null;
        return await GetHeader(id, 0);

        async Task<string?> GetHeader(string vid, int depth)
        {
            ExVndb.ExVn? result = await ExVndb.TryGetExVnAsync(vid);
            if (result is not null && (result.BestHeaderImage is not null || result.AlternativeHeaderImage is not null))
            {
                var imageId = result.BestHeaderImage ?? result.AlternativeHeaderImage;
                var lastTwo = imageId![^2..];
                return $"https://t.vndb.org/sf/{lastTwo}/{imageId[2..]}.jpg";
            }
            // 没有在数据库中搜到图片，尝试从在线api获取截图
            VndbResponse<VndbVn>? vndbResponse = await CallVndbApiAsync(() => _vndbApi.GetVisualNovelAsync(new VndbQuery
            {
                Fields = "screenshots.url, screenshots.sexual, screenshots.violence, relations.id",
                Filters = VndbFilters.Equal("id", vid),
            }));
            if (vndbResponse?.Results?.Count > 0)
            {
                if (vndbResponse.Results[0].Screenshots?.Count > 0)
                    return vndbResponse.Results[0].Screenshots?
                        .FirstOrDefault(sc => sc is { Sexual: < 10, Violence: < 10 })?.Url;
                if (depth == 1) return null;
                //else: 这个游戏没有截图，可能是某个续作或FD，尝试从相关游戏（一般为正作）获取截图
                foreach (VndbVn relationGame in vndbResponse.Results[0].Relations ?? [])
                {
                    var tmp = await GetHeader(relationGame.Id!, depth + 1);
                    if (tmp is not null) return tmp;
                }
            }
            return null;
        }
    }

    public async Task<List<string>> GetGalHeadersAsync(Galgame game)
    {
        List<string> result = [];
        if (string.IsNullOrEmpty(game.Ids[(int)RssType.Vndb])) await TryGetId(game);
        if (string.IsNullOrEmpty(game.Ids[(int)RssType.Vndb])) return result;
        var id = game.Ids[(int)RssType.Vndb]!.StartsWith('v')
            ? game.Ids[(int)RssType.Vndb]!
            : "v" + game.Ids[(int)RssType.Vndb]!;

        await GetHeader(id, 0);
        return result;

        async Task GetHeader(string vid, int depth)
        {
            ExVndb.ExVn? exVn = await ExVndb.TryGetExVnAsync(vid);
            if (exVn is not null && (exVn.BestHeaderImage is not null || exVn.AlternativeHeaderImage is not null))
            {
                var imageId = exVn.BestHeaderImage ?? exVn.AlternativeHeaderImage;
                var lastTwo = imageId![^2..];
                result.Add($"https://t.vndb.org/sf/{lastTwo}/{imageId[2..]}.jpg");
            }

            VndbResponse<VndbVn>? vndbResponse = await CallVndbApiAsync(() => _vndbApi.GetVisualNovelAsync(new VndbQuery
            {
                Fields = "screenshots.url, screenshots.sexual, screenshots.violence, relations.id",
                Filters = VndbFilters.Equal("id", vid),
            }));

            if (vndbResponse?.Results?.Count > 0)
            {
                if (vndbResponse.Results[0].Screenshots?.Count > 0)
                {
                    foreach (var screenshot in vndbResponse.Results[0].Screenshots!
                                 .Where(sc => sc is { Sexual: < 10, Violence: < 10 }))
                    {
                        if (screenshot.Url is not null)
                            result.Add(screenshot.Url);
                    }
                }

                if (depth == 1) return;
                //else: 这个游戏没有截图，可能是某个续作或FD，尝试从相关游戏（一般为正作）获取截图
                if (result.Count == 0 && vndbResponse.Results[0].Relations is not null)
                {
                    foreach (VndbVn relationGame in vndbResponse.Results[0].Relations!)
                    {
                        // 递归获取相关游戏的截图，但不覆盖result，而是追加（虽然此处逻辑稍微有点变动，原本是找到一个就返回）
                        // 为了避免递归太深或者无限循环，depth限制为1
                        // 注意：这里可能会导致获取到很多不相关的图，但原逻辑是"如果没有"才去找
                        // 所以这里保持"如果result为空"才去找
                        if (result.Count > 0) break;
                        await GetHeader(relationGame.Id!, depth + 1);
                    }
                }
            }
        }
    }

    /// 一个简单的wrapper，自动处理throttle，返回值为null时表示失败
    private static async Task<VndbResponse<T>?> CallVndbApiAsync<T>(Func<Task<VndbResponse<T>>> func)
    {
        do
        {
            try
            {
                return await func();
            }
            catch (ThrottledException)
            {
                await Task.Delay(60 * 1000); // 1 minute
            }
            catch (Exception)
            {
                return null;
            }
        } while (true);
    }


}

public class VndbPhraserData : IGalInfoPhraserData
{
    public string? Token;
    public bool IsChineseCulture;
    public bool TranslateTags;
    public bool CensorTags = true;
    public bool RemoveSpoilerTags = true;

    public VndbPhraserData() { }

    public VndbPhraserData(string? token, bool isChineseCulture = true, bool translateTags = true,
        bool censorTags = true, bool removeSpoilerTags = true)
    {
        Token = token;
        IsChineseCulture = isChineseCulture;
        TranslateTags = translateTags;
        CensorTags = censorTags;
        RemoveSpoilerTags = removeSpoilerTags;
    }
}
