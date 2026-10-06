using System.Collections.ObjectModel;
using System.Text;
using System.Web;
using GalgameManager.Contracts.Phrase;
using GalgameManager.Core.Helpers;
using GalgameManager.Enums;
using GalgameManager.Helpers.API.Bgm;
using GalgameManager.Models;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Exception = System.Exception;

namespace GalgameManager.Helpers.Phrase;

public class BgmPhraser : IGalInfoPhraser, IGalStatusSync, IGalCharacterPhraser, IGalStaffParser, IGalCoversParser
{
    private HttpClient _httpClient;
    private IBgmApi _bgmApi = null!;
    private bool _authed;
    private string _userId = string.Empty;
    private string _userName = string.Empty;
    private Task? _checkAuthTask;
    private string _token = string.Empty;

    public BgmPhraser(BgmPhraserData data)
    {
        _httpClient = new HttpClient();
        GetHttpClient(data);
    }

    public void UpdateData(IGalInfoPhraserData data)
    {
        if(data is BgmPhraserData bgmData)
            GetHttpClient(bgmData);
    }
    
    private void GetHttpClient(BgmPhraserData data)
    {
        _authed = false;
        _token = data.Token ?? string.Empty;
        _httpClient = Utils.GetDefaultHttpClient().WithApplicationJson();
        _bgmApi = BgmApi.GetApi(_token);
        if (!string.IsNullOrEmpty(_token))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _token);
            _checkAuthTask = Task.Run(async () =>
            {
                try
                {
                    HttpResponseMessage response = await _httpClient.GetAsync("https://api.bgm.tv/v0/me");
                    _authed = response.IsSuccessStatusCode;
                    if (!_authed) return;
                    JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
                    _userId = json["id"]!.ToString();
                    _userName = json["username"]!.ToString();
                }
                catch (Exception)
                {
                    //ignore
                }
            });
        }
    }

    private async Task<bool> EnsureAuthAsync()
    {
        if (_checkAuthTask != null)
        {
            try { await _checkAuthTask; } catch { /* ignore */ }
        }
        if (_authed && !string.IsNullOrEmpty(_userId)) return true;
        if (string.IsNullOrEmpty(_token)) return false;

        try
        {
            _httpClient = Utils.GetDefaultHttpClient().WithApplicationJson();
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _token);
            _bgmApi = BgmApi.GetApi(_token);

            HttpResponseMessage response = await _httpClient.GetAsync("https://api.bgm.tv/v0/me");
            _authed = response.IsSuccessStatusCode;
            if (!_authed) return false;
            JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
            _userId = json["id"]!.ToString();
            _userName = json["username"]!.ToString();
            return true;
        }
        catch (Exception)
        {
            _authed = false;
            return false;
        }
    }
    
    private async Task<int?> GetId(string name)
    {
        // 先试图从本地数据库获取
        var id = await PhraseHelper.TryGetBgmIdAsync(name);
        if (id is not null) return id;
        // 本地数据库没有则从网络获取
        try
        {
            var url = "https://api.bgm.tv/search/subject/" + HttpUtility.UrlEncode(name) + "?type=4";
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
            List<JToken>? games = jsonToken["list"]!.ToObject<List<JToken>>();
            if (games==null || games.Count == 0) return null;
            
            double maxSimilarity = 0;
            var target = 0;
            foreach (JToken game in games)
                if (IGalInfoPhraser.Similarity(name, game["name_cn"]!.ToObject<string>()!) > maxSimilarity ||
                    IGalInfoPhraser.Similarity(name, game["name"]!.ToObject<string>()!) > maxSimilarity)
                {
                    maxSimilarity = Math.Max
                    (
                        IGalInfoPhraser.Similarity(name, game["name_cn"]!.ToObject<string>()!),
                        IGalInfoPhraser.Similarity(name, game["name"]!.ToObject<string>()!)
                    );
                    target = games.IndexOf(game);
                }
                
            return games[target]["id"]!.ToObject<int>();
        }
        catch (Exception)
        {
            return null;
        }
    }
    
    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        var name = galgame.Name.Value ?? "";
        int? id;
        try
        {
            id = Convert.ToInt32(galgame.Ids[(int)RssType.Bangumi] ?? "");
        }
        catch (Exception)
        {
            id = await GetId(name);
        }
        
        if (id == null) return null;
        GameDto gameDto;
        try
        {
            gameDto = await _bgmApi.GetGameAsync(id.Value);
        }
        catch (Exception)
        {
            return null;
        }
        
        Galgame result = new()
        {
            RssType = RssType.Bangumi,
            Id = gameDto.id.ToString(),
            Name = gameDto.name,
            CnName = gameDto.name_cn,
            Description = gameDto.summary,
            ImageUrl = gameDto.images.large,
            Rating = (float)gameDto.rating.score,
            ReleaseDate = DateTimeExtensions.ToDateTime(gameDto.date ?? string.Empty),
            Tags = new ObservableCollection<string>(gameDto.tags.Select(t => t.name)),
        };
        // developer
        JToken? developerInfoBox = gameDto.infobox?.FirstOrDefault(x => x.key.Contains("开发"))?.value;
        if (developerInfoBox is not null)
        {
            switch (developerInfoBox.Type)
            {
                case JTokenType.Array:
                {
                    IEnumerable<char> tmp = developerInfoBox.SelectMany(dev => dev["v"]!.ToString());
                    result.Developer = string.Join(",", tmp);
                    break;
                }
                case JTokenType.String:
                    result.Developer = developerInfoBox.ToString();
                    break;
                default:
                    result.Developer = Galgame.DefaultString;
                    break;
            }
        }
        // characters
        List<RelatedCharacterDto> characters = new();
        try
        {
            characters = await _bgmApi.GetGameCharactersAsync(id.Value);
        }
        catch (Exception)
        {
            // ignored
        }
        foreach (RelatedCharacterDto characterDto in characters.Where(c => c.id is not null))
        {
            GalgameCharacter c = new GalgameCharacter
            {
                Name = characterDto.name,
                Relation = characterDto.relation,
                Ids =
                {
                    [(int)GetPhraseType()] = characterDto.id.ToString(),
                },
            };
            result.Characters.Add(c);
        }
        return result;
    }

    /// <summary>
    /// 获取封面图片，只关注ImageUrl
    /// </summary>
    public async Task<List<string>> GetGalCoversAsync(Galgame galgame)
    {
        var name = galgame.Name.Value ?? "";
        int? id;
        try
        {
            id = Convert.ToInt32(galgame.Ids[(int)RssType.Bangumi] ?? "");
        }
        catch (Exception)
        {
            id = await GetId(name);
        }
        
        if (id == null) return [];
        GameDto gameDto;
        try
        {
            gameDto = await _bgmApi.GetGameAsync(id.Value);
        }
        catch (Exception)
        {
            return [];
        }
        return gameDto.images.large is not null ? [gameDto.images.large] : [];
    }

    public RssType GetPhraseType() => RssType.Bangumi;

    public async Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter)
    {
        var id = galgameCharacter.Ids[(int)GetPhraseType()];
        if (id == null) return null;
        return await GetCharacterById(id);
    }

    /// <summary>
    /// 获取开发商的图片链接
    /// </summary>
    /// <param name="developer">开发商名</param>
    /// <param name="retry">重试次数，不用手动设置它</param>
    /// <returns>图片链接，若找不到则返回null</returns>
    public async Task<string?> GetDeveloperImageUrlAsync(string developer,int retry = 0)
    {
        string? result = null;
        try
        {
            var searchUrl = $"https://bgm.tv/mono_search/{developer}?cat=prsn";
            HttpResponseMessage response = await _httpClient.GetAsync(searchUrl);
            if (!response.IsSuccessStatusCode) return null;
            HtmlDocument doc = new();
            doc.LoadHtml(await response.Content.ReadAsStringAsync());
            HtmlNodeCollection? nodes = doc.DocumentNode.SelectNodes("//div[@class='light_odd clearit']");
            if (nodes == null)
            {
                await Task.Delay(500);
                if (retry < 3)
                    return await GetDeveloperImageUrlAsync(developer, retry + 1);
                return null;
            }
            var similarity = -1;
            HtmlNode? target = null;
            foreach (HtmlNode? node in nodes)
            {
                HtmlNode name = node.SelectSingleNode(".//a[@class='l']");
                var dis = name.InnerText.Levenshtein(developer);
                if (dis < 2 && 1000 - dis > similarity) //如果名字和开发商名字编辑距离小于2，就认为是这个开发商
                {
                    similarity = 1000 - dis;
                    target = node;
                }

                HtmlNode? rateNode = node.SelectSingleNode(".//small[@class='na']");
                if (rateNode != null) //用评分最高者
                {
                    var rateStr = rateNode.InnerText;
                    rateStr = rateStr.Substring(1, rateStr.Length - 2);
                    var rate = Convert.ToInt32(rateStr);
                    if (rate > similarity)
                    {
                        similarity = rate;
                        target = node;
                    }
                }
            }

            if (target is not null)
            {
                HtmlNode? id = target.SelectSingleNode(".//a[@class='l']");
                // eg: /person/7175
                var idStr = id.GetAttributeValue("href", "")[8..];
                return await GetDeveloperImageUrlById(idStr);
            }
        }
        catch (Exception)
        {
            // ignored
        }

        return result;
    }

    private async Task<string?> GetDeveloperImageUrlById(string id, int retry = 0)
    {
        HttpResponseMessage response = await _httpClient.GetAsync($"https://api.bgm.tv/v0/persons/{id}");
        if (!response.IsSuccessStatusCode) return null;

        try
        {
            JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
            return jsonToken["images"]!["large"]!.ToObject<string>()!;
        }
        catch
        {
            await Task.Delay(500);
            if (retry < 3)
                return await GetDeveloperImageUrlById(id, retry + 1);
            return null;
        }
    }

    private async Task<GalgameCharacter?> GetCharacterById(string id)
    {
        HttpResponseMessage response = await _httpClient.GetAsync($"https://api.bgm.tv/v0/characters/{id}");
        if (!response.IsSuccessStatusCode) return null;
        try
        {
            JToken jsonToken = JToken.Parse(await response.Content.ReadAsStringAsync());
            GalgameCharacter character = new()
            {
                Name = jsonToken["name"]?.ToObject<string?>() ?? "",
                BirthDay = jsonToken["birth_day"]?.ToObject<int?>(),
                BirthMon = jsonToken["birth_mon"]?.ToObject<int?>(),
                BirthYear = jsonToken["birth_year"]?.ToObject<int?>(),
                Summary = jsonToken["summary"]?.ToObject<string?>() ?? "-", 
                BloodType = jsonToken["blood_type"]?.ToObject<string?>(),
                PreviewImageUrl = jsonToken["images"]?["large"]?.ToObject<string?>()?.Replace("/l/", "/g/"),
                ImageUrl = jsonToken["images"]?["large"]?.ToObject<string?>(),
            };
            // 对血型做特殊处理，blood_type可能为空
            List<JToken>? infoBox = jsonToken["infobox"]?.ToObject<List<JToken>>();
            JToken? bloodTypeInfoBox = infoBox?.Find(x => x["key"]?.ToObject<string>()?.Contains("血型") ?? false);
            if (bloodTypeInfoBox?["value"] != null)
            {
                character.BloodType = bloodTypeInfoBox["value"]!.ToObject<string>();
            }
            
            JToken? heightTypeInfoBox = infoBox?.Find(x => x["key"]?.ToObject<string>()?.Contains("身高") ?? false);
            if (heightTypeInfoBox?["value"] != null)
            {
                character.Height = heightTypeInfoBox["value"]!.ToObject<string>();
            }
            
            JToken? weightTypeInfoBox = infoBox?.Find(x => x["key"]?.ToObject<string>()?.Contains("体重") ?? false);
            if (weightTypeInfoBox?["value"] != null)
            {
                character.Weight = weightTypeInfoBox["value"]!.ToObject<string>();
            }
            
            JToken? BWHTypeInfoBox = infoBox?.Find(x => x["key"]?.ToObject<string>()?.Contains("BWH") ?? false);
            if (BWHTypeInfoBox?["value"] != null)
            {
                character.BWH = BWHTypeInfoBox["value"]!.ToObject<string>();
            }
            
            JToken? birthDateTypeInfoBox = infoBox?.Find(x => x["key"]?.ToObject<string>()?.Contains("生日") ?? false);
            if (birthDateTypeInfoBox?["value"] != null)
            {
                character.BirthDate = birthDateTypeInfoBox["value"]!.ToObject<string>();
            }

            character.Gender = jsonToken["gender"]?.ToObject<string?>() switch
            {
                "male" => Gender.Male,
                "female" => Gender.Female,
                _ => Gender.Unknown
            };

            return character;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(GalStatusSyncResult, string)> UploadAsync(Galgame galgame)
    {
        if (await EnsureAuthAsync() == false)
            return (GalStatusSyncResult.UnAuthorized, "BgmPhraser_UploadAsync_UnAuthorized".GetLocalized());
        if (string.IsNullOrEmpty(galgame.Ids[(int)RssType.Bangumi]))
            return (GalStatusSyncResult.NoId, "BgmPhraser_UploadAsync_NoId".GetLocalized());
        var data = new
        {
            @private = galgame.PrivateComment,
            rate = galgame.MyRate,
            comment = galgame.Comment,
            type = galgame.PlayType.ToBgmCollectionType()
        };
        StringContent content = new(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
        content.Headers.ContentType!.CharSet = ""; //bgm.tv的api不支持charset
        var error = string.Empty;
        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient.PostAsync($"https://api.bgm.tv/v0/users/-/collections/{galgame.Ids[(int)RssType.Bangumi]}", content);
            if (response.IsSuccessStatusCode == false)
            {
                JObject json = JObject.Parse(await response.Content.ReadAsStringAsync());
                error = json["description"]!.ToString();
            }
        }
        catch (Exception e)
        {
            error = e.Message;
        }
        if(response == null || response.IsSuccessStatusCode == false)
            return (GalStatusSyncResult.Other, error);
        
        return (GalStatusSyncResult.Ok, "BgmPhraser_UploadAsync_Success".GetLocalized());
    }

    public async Task<(GalStatusSyncResult, string)> DownloadAsync(Galgame galgame)
    {
        if (await EnsureAuthAsync() == false) 
            return (GalStatusSyncResult.UnAuthorized, "BgmPhraser_UploadAsync_UnAuthorized".GetLocalized());
        if (string.IsNullOrEmpty(galgame.Ids[(int)RssType.Bangumi]))
            return (GalStatusSyncResult.NoId, "BgmPhraser_UploadAsync_NoId".GetLocalized());
        string errorMsg;
        try
        {
            var userIdOrName = string.IsNullOrEmpty(_userName) ? _userId : _userName;
            HttpResponseMessage response = await _httpClient.GetAsync(
                $"https://api.bgm.tv/v0/users/{userIdOrName}/collections/{galgame.Ids[(int)RssType.Bangumi]}");
            JToken json = JObject.Parse(await response.Content.ReadAsStringAsync());
            if (response.IsSuccessStatusCode == false)
                throw new Exception(json["description"]!.ToString());
            return PhrasePlayStatusJToken(json, galgame);
        }
        catch (Exception e)
        {
            errorMsg = e.Message;
        }
        return (GalStatusSyncResult.Other, errorMsg);
    }

    public async Task<(GalStatusSyncResult, string)> DownloadAllAsync(IList<Galgame> galgames)
    {
        if (await EnsureAuthAsync() == false) return (GalStatusSyncResult.UnAuthorized, "BgmPhraser_UploadAsync_UnAuthorized".GetLocalized());
        int offset = 0, total = -1, cnt = 0;
        GalStatusSyncResult result = GalStatusSyncResult.Ok;
        var msg = string.Empty;
        while (total == -1 || offset < total)
        {
            try
            {
                var userIdOrName = string.IsNullOrEmpty(_userName) ? _userId : _userName;
                HttpResponseMessage response = await _httpClient.GetAsync(
                    $"https://api.bgm.tv/v0/users/{userIdOrName}/collections?subject_type=4&limit=30&offset={offset}");
                JToken json = JObject.Parse(await response.Content.ReadAsStringAsync());
                if (response.IsSuccessStatusCode == false)
                    throw new Exception(json["description"]!.ToString());
                total = json["total"]!.ToObject<int>();
                List<JToken>? games = json["data"]!.ToObject<List<JToken>>();
                offset += games!.Count;
                foreach (JToken game in games)
                {
                    Galgame? tmp = galgames.FirstOrDefault(g => g.Ids[(int)RssType.Bangumi] == game["subject_id"]!.ToString());
                    if (tmp is null) continue;
                    PhrasePlayStatusJToken(game, tmp);
                    cnt++;
                }
            }
            catch (Exception e)
            {
                result = GalStatusSyncResult.Other;
                msg = e.Message;
                break;
            }
        }
        if (string.IsNullOrEmpty(msg))
            msg = string.Format("BgmPhraser_DownloadPlayStatus_Success".GetLocalized(), cnt);
        
        return (result, msg);
    }

    /// <summary>
    /// 将bgm游玩状态json解析到游戏中，需要调用方手动捕捉json解析异常
    /// </summary>
    /// <param name="json">游玩状态json</param>
    /// <param name="galgame">游戏</param>
    /// <returns>解析状态，状态解释</returns>
    private static (GalStatusSyncResult, string) PhrasePlayStatusJToken(JToken json, Galgame galgame)
    {
        galgame.PlayType = json["type"]!.ToObject<int>().BgmCollectionTypeToPlayType();
        galgame.Comment = json["comment"]!.ToString();
        galgame.MyRate = json["rate"]!.ToObject<int>();
        galgame.PrivateComment = json["private"]!.ToObject<bool>();
        return (GalStatusSyncResult.Ok, "BgmPhraser_DownloadAsync_Success".GetLocalized());
    }

    public async Task<Staff?> GetStaffAsync(Staff staff)
    {
        var id = await GetStaffIdAsync(staff);
        if (id is null) return null;
        Staff result = new();
        PersonDetailDto tmp = await _bgmApi.GetPersonAsync(id.Value);
        result.Ids[(int)GetPhraseType()] = id.ToString();
        result.ChineseName = tmp.name;
        // Names
        foreach (InfoBoxItemDto token in tmp.infobox)
        {
            if (token is { key: "简体中文名", value.Type: JTokenType.String })
                result.ChineseName = token.value.ToObject<string>();
            if (token is not { key: "别名", value.Type: JTokenType.Array }) continue;
            foreach (JToken alias in token.value)
            {
                try
                {
                    InfoBoxItemKVDto? tmp2 = alias.ToObject<InfoBoxItemKVDto>();
                    if (tmp2 is null) continue;
                    if (tmp2.k == "日文名") result.JapaneseName = tmp2.v;
                    if (tmp2.k == "罗马字") result.EnglishName = tmp2.v;
                    if (tmp2.k == "纯假名") result.JapaneseName ??= tmp2.v;
                }
                catch (Exception)
                {
                    // ignore
                }
            }
        }
        result.Gender = tmp.gender switch
        {
            "female" => Gender.Female,
            "male" => Gender.Male,
            _ => result.Gender,
        };
        result.Career = new (tmp.career.Select(c => c.ToCareer()));
        result.ImageUrl = tmp.images?.large;
        result.Description = tmp.summary;
        result.BirthDate = new DateTime(tmp.birth_year ?? 1, tmp.birth_mon ?? 1, tmp.birth_day ?? 1);
        if (result.BirthDate == new DateTime(1, 1, 1))
            result.BirthDate = null;
        return result;
    }

    public async Task<List<StaffRelation>> GetStaffsAsync(Galgame game)
    {
        int? gameId = null;
        try
        {
            gameId = Convert.ToInt32(game.Ids[(int)GetPhraseType()] ?? string.Empty);
        }
        catch (Exception)
        {
            if (game.Name.Value is not null)
                gameId = await GetId(game.Name.Value);
        }
        if (gameId is null) return [];
        List<StaffRelation> result = [];
        List<RelatedPersonDto> staffs = await _bgmApi.GetGamePersonsAsync(gameId.Value);
        result.AddRange(staffs.Select(person => new StaffRelation
        {
            ChineseName = person.name,
            ImageUrl = person.images?.large,
            Ids = { [(int)GetPhraseType()] = person.id.ToString() },
            Relation = person.relation switch
            {
                "音乐" => [Career.Musician],
                "剧本" => [Career.Writer],
                "原画" => [Career.Painter],
                _ => [Career.Unknown],
            },
            Career = new(person.career.Distinct().Select(dto => dto.ToCareer())),
        }));
        // bgm上声优是单独在character里的，需要额外获取
        List<RelatedCharacterDto> characters = await _bgmApi.GetGameCharactersAsync(gameId.Value);
        foreach (RelatedCharacterDto character in characters.Where(dto => dto.actors is not null))
            result.AddRange((character.actors ?? []).Select(seiyu => new StaffRelation
            {
                ChineseName = seiyu.name,
                ImageUrl = seiyu.images?.large,
                Ids = { [(int)GetPhraseType()] = seiyu.id.ToString() },
                Relation = [Career.Seiyu],
                Career = new(seiyu.career.Distinct().Select(dto => dto.ToCareer())),
            }));
        return result;
    }

    private async Task<int?> GetStaffIdAsync(Staff staff)
    {
        int? id = null;
        try
        {
            id = Convert.ToInt32(staff.Ids[(int)GetPhraseType()] ?? string.Empty);
        }
        catch (Exception)
        {
            // ignore
        }
        if (id is not null || staff.Name is null) return id;
        try
        {
            Paged<PersonDto> tmp = await _bgmApi.SearchPersonAsync(new SearchPersonPayload { keyword = staff.Name });
            if (tmp.data.Count > 0) id = tmp.data[0].id;
        }
        catch (Exception)
        {
            //ignore
        }
        return id;
    }
}

public class BgmPhraserData : IGalInfoPhraserData
{
    public string? Token;

    public BgmPhraserData() { }
    
    public BgmPhraserData(string? token)
    {
        Token = token;
    }
}
