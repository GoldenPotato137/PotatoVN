using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Helpers.API.Ymgal;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Refit;
using System.Collections.ObjectModel;
using System.Text;
using System.Web;


// ReSharper disable ClassNeverInstantiated.Global

namespace GalgameManager.Helpers.Phrase;

public class YmgalPhraser: IGalInfoPhraser, IGalCharacterPhraser, IGalStaffParser
{
    private IYmgalApi _ymgalApi;
    private Task<IYmgalApi>? _apiInitTask;

    public YmgalPhraser()
    {
        // 初始化一个未认证的API实例
        _ymgalApi = YmgalApi.GetApi();
        // 后台任务获取认证的API实例
        _apiInitTask = YmgalApi.GetAuthenticatedApiAsync();
    }

    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        // 确保先初始化API
        await EnsureApiInitialized();

        var name = galgame.Name.Value ?? "";
        int? id;
        try
        {
            if (galgame.RssType != RssType.Ymgal) throw new Exception();
            id = Convert.ToInt32(galgame.Id ?? "");
        }
        catch (Exception)
        {
            // 如果ID无效，尝试搜索游戏
            try
            {
                var searchResponse = await ExecuteWithTokenRefreshAsync(async api => 
                    await api.SearchGameAsync(name));
                    
                if (!searchResponse.Success || searchResponse.Data?.Result.Count == 0) 
                    return null;
                
                id = searchResponse.Data?.Result?.FirstOrDefault()?.Id;
            }
            catch (Exception)
            {
                return null;
            }
        }

        try
        {
            var gameResponse = await ExecuteWithTokenRefreshAsync(async api => 
                await api.GetGameAsync(id ?? throw new InvalidOperationException("ID cannot be null")));
                
            if (!gameResponse.Success || gameResponse.Data?.Game == null)
                return null;
                
            var g = gameResponse.Data.Game;
            Galgame result = new()
            {
                Name = g.Name,
                CnName = g.ChineseName ?? "",
                Description = g.Introduction,
                ReleaseDate = IGalInfoPhraser.GetDateTimeFromString(g.ReleaseDate) ?? DateTime.MinValue, 
                ImageUrl = g.MainImg,
                Id = g.Gid != 0 ? g.Gid.ToString() : g.Id.ToString()
            };

            // 获取开发商信息        
            try
            {
                var developerResponse = await ExecuteWithTokenRefreshAsync(async api => 
                    await api.GetOrganizationAsync(g.DeveloperId));
                    
                if (developerResponse.Success && developerResponse.Data?.Org != null)
                {
                    result.Developer = developerResponse.Data.Org.Name;
                }
                else
                {
                    result.Developer = Galgame.DefaultString;
                }
            }
            catch
            {
                result.Developer = Galgame.DefaultString;
            }

            // 获取人物信息


            foreach (var c in g.Characters)
            {
                GalgameCharacter character = new()
                {                  
                    Ids = 
                    {
                        [(int)RssType.Ymgal] = c.Cid.ToString()
                    },
                    Relation = c.CharacterPosition == 1 ? "主角" : "配角",
                    Name = gameResponse.Data.CidMapping.TryGetValue(c.Cid.ToString(), out var mapping) ? mapping.Name ?? "" : "",
                };
                result.Characters.Add(character);
            }



            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public RssType GetPhraseType() => RssType.Ymgal;

    public async Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter)
    {
        var cid = galgameCharacter.Ids[(int)RssType.Ymgal];
        if (cid == null) return null;
        return await GetCharacterById(cid);

    }

    private async Task<GalgameCharacter?> GetCharacterById(string cid)
    {
        try
        {
            // 确保API已初始化
            await EnsureApiInitialized();

            // 将字符串ID转换为整数
            if (!int.TryParse(cid, out var characterId))
                return null;

            // 调用API获取角色信息
            API.ApiResponse<API.CharacterResponse> characterResponse = await _ymgalApi.GetCharacterAsync(characterId);

            if (!characterResponse.Success || characterResponse.Data?.character == null)
                return null;

            API.Character c = characterResponse.Data.character;

            // 创建GalgameCharacter对象
            GalgameCharacter character = new GalgameCharacter
            {
                Ids = 
                {
                    [(int)GetPhraseType()] = cid.ToString()
                },
                Name = c.Name,
                Summary = c.Introduction,
                ImageUrl = c.MainImg,
                PreviewImageUrl = c.MainImg,
                Gender = GetGenderFromInt(c.Gender),
            };

            // 处理生日信息
            if (!string.IsNullOrEmpty(c.Birthday) && c.Birthday != "0000-00-00")
            {
            
                // 尝试解析年月日
                var parts = c.Birthday.Split('-');
                if (parts.Length == 3)
                {
                    // 年份处理：如果年份 >= 3000，视为未知年份，不设置BirthYear
                    if (int.TryParse(parts[0], out var year) && year > 0 && year < 3000)
                        character.BirthYear = year;
            
                    // 月份处理：确保月份在有效范围(1-12)内
                    if (int.TryParse(parts[1], out var month) && month >= 1 && month <= 12)
                        character.BirthMon = month;
            
                    // 日期处理：确保日期在有效范围(1-31)内
                    if (int.TryParse(parts[2], out var day) && day >= 1 && day <= 31)
                        character.BirthDay = day;

                    character.BirthDate = character.BirthMon + "月" + character.BirthDay + "日";
                }
            }

            // 设置ID
            character.Ids[(int)RssType.Ymgal] = cid;

            return character;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 根据Ymgal API的性别整数获取Gender枚举值
    private Gender GetGenderFromInt(int genderValue)
    {
        return genderValue switch
        {
            1 => Gender.Male,
            2 => Gender.Female,
            _ => Gender.Unknown
        };
    }

    public async Task<Staff?> GetStaffAsync(Staff staff)
    {
        var pid = staff.Ids[(int)RssType.Ymgal];
        if (pid == null) return null;
        var newStaff = await GetStaffById(pid);
        if (newStaff != null)
        {
            newStaff.Career = staff.Career;
            newStaff.Ids = staff.Ids;
        }
        return newStaff;
    }

    public async Task<List<StaffRelation>> GetStaffsAsync(Galgame galgame)
    {
        try
        {
            // 确保先初始化API
            await EnsureApiInitialized();

            var name = galgame.Name.Value ?? "";
            int? id;
            try
            {
                if (galgame.RssType != RssType.Ymgal) throw new Exception();
                id = Convert.ToInt32(galgame.Id ?? "");
            }
            catch (Exception)
            {
                // 如果ID无效，尝试搜索游戏
                try
                {
                    var searchResponse = await ExecuteWithTokenRefreshAsync(async api =>
                        await api.SearchGameAsync(name));

                    if (!searchResponse.Success || searchResponse.Data?.Result.Count == 0)
                        return [];

                    id = searchResponse.Data?.Result?.FirstOrDefault()?.Id;
                }
                catch (Exception)
                {
                    return [];
                }
            }

            try
            {
                List<StaffRelation> result = new();
                var gameResponse = await ExecuteWithTokenRefreshAsync(async api =>
                    await api.GetGameAsync(id ?? throw new InvalidOperationException("ID cannot be null")));

                if (!gameResponse.Success || gameResponse.Data?.Game == null)
                    return [];

                var g = gameResponse.Data.Game;

                foreach (var s in g.Staff)
                {
                    StaffRelation staffRelation = new()
                    {
                        JapaneseName = s.EmpName == "None" ? null : s.EmpName,
                        Ids = 
                        {
                            [(int)GetPhraseType()] = s.Pid is not null ? s.Pid.ToString() : s.Sid.ToString()
                        },
                        Relation = DetermineCareerByJobName(s.JobName).ToList(),
                        Career = new ObservableCollection<Career>(DetermineCareerByJobName(s.JobName)),

                    };
                    if (s.JobName.Contains("其他"))
                    {
                        continue;
                    }
                    result.Add(staffRelation);
                }

                // 单独获取声优信息
                foreach (var c in g.Characters)
                {
                    if (c.CvId != null && c.CvId != 0)
                    {
                        StaffRelation staffRelation = new()
                        {
                            Ids = 
                            {
                                [(int)GetPhraseType()] = c.CvId.ToString()
                            },
                            Relation = new List<Career> { Career.Seiyu },
                            Career = new ObservableCollection<Career> { Career.Seiyu },
                        };
                        result.Add(staffRelation);
                    }
                }


                return result;
            }
            catch (Exception)
            {
                return [];
            }
        }
        catch (Exception)
        {
            return [];
        }
    }

    private Career[] DetermineCareerByJobName(string jobName)
    {
        List<Career> result = new ();
    

        if (string.IsNullOrWhiteSpace(jobName))
            return [Career.Unknown];

        // 音乐相关
        if (jobName.Contains("音乐") || jobName.Contains("歌曲") ||
            jobName.Contains("作曲") || jobName.Contains("编曲"))
            result.Add(Career.Musician);

        // 文案、剧本相关
        if (jobName.Contains("脚本") || jobName.Contains("剧本") ||
            jobName.Contains("文案") || jobName.Contains("导演") ||
            jobName.Contains("监督") || jobName.Contains("企划") ||
            jobName.Contains("シナリオ"))
            result.Add(Career.Writer);

        // 绘画、美术相关
        if (jobName.Contains("人物设计") || jobName.Contains("サブ原画") ||
            jobName.Contains("原画") || jobName.Contains("背景") ||
            jobName.Contains("立绘") || jobName.Contains("CG") ||
            jobName.Contains("イラスト"))
            result.Add(Career.Painter);

        // 配音相关
        if (jobName.Contains("声优") || jobName.Contains("配音") ||
            jobName.Contains("声優") || jobName.Contains("CV"))
            result.Add(Career.Seiyu);

        // 程序相关
        if (jobName.Contains("程序") || jobName.Contains("プログラム") ||
            jobName.Contains("编程"))
            result.Add(Career.Programmer);

        // 制作人相关
        if (jobName.Contains("制作") || jobName.Contains("制片") ||
            jobName.Contains("Producer") || jobName.Contains("プロデューサー"))
            result.Add(Career.Producer);

        if (result.Count == 0)
            result.Add(Career.Unknown);

        return result.ToArray();
    }

    
    private async Task<Staff?> GetStaffById(string pid)
    {
        try
        {
            // 确保API已初始化
            await EnsureApiInitialized();

            // 将字符串ID转换为整数
            if (!int.TryParse(pid, out var personId))
                return null;

            // 调用API获取工作人员信息
            API.ApiResponse<API.StaffResponse> staffResponse = await _ymgalApi.GetStaffAsync(personId);

            if (!staffResponse.Success || staffResponse.Data?.person == null)
                return null;

            API.Person p = staffResponse.Data.person;

            // 创建Staff对象
            Staff staff = new Staff
            {
                JapaneseName = p.Name,
                ChineseName = p.ChineseName,
                Gender = GetGenderFromInt(p.Gender),
                Description = p.Introduction,
                ImageUrl = p.MainImg,
            };

            // 处理生日信息
            if (!string.IsNullOrEmpty(p.Birthday) && p.Birthday != "0000-00-00")
            {
                
                if (DateTime.TryParse(p.Birthday, out DateTime birthDate))
                {

                    // 如果年份大于2500年，视为1年
                    if (birthDate.Year >= 2500)
                        birthDate = new DateTime(1, birthDate.Month, birthDate.Day);
                    staff.BirthDate = birthDate;
                }
            }

            return staff;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 确保API已初始化
    private async Task EnsureApiInitialized()
    {
        if (_apiInitTask != null)
        {
            _ymgalApi = await _apiInitTask;
            _apiInitTask = null; // 初始化完成后清除任务引用
        }
    }

    // 带有token刷新逻辑的API调用执行器
    private async Task<T> ExecuteWithTokenRefreshAsync<T>(Func<IYmgalApi, Task<T>> apiCall, int retryCount = 0)
    {
        // 最大重试次数为2，防止死循环
        const int maxRetries = 2;
        
        try
        {
            // 确保API已初始化
            await EnsureApiInitialized();
            
            // 执行API调用，传入当前的API实例
            return await apiCall(_ymgalApi);
        }
        catch (ApiException ex) when ((ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                                      ex.StatusCode == System.Net.HttpStatusCode.Forbidden) &&
                                      retryCount < maxRetries)
        {
            // 如果是401(Unauthorized)或403(Forbidden)，且未超过最大重试次数，尝试刷新token
            _ymgalApi = await YmgalApi.GetAuthenticatedApiAsync();
            
            // 递增重试计数，并重试API调用
            return await ExecuteWithTokenRefreshAsync(apiCall, retryCount + 1);
        }
    }

}







