using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Phrase;

public interface IGalStaffParser
{
    /// <summary>
    /// 获取staff信息，将获取到的信息用一个新的staff返回，不要直接修改传入staff <br/>
    /// 注意捕获异常
    /// </summary>
    /// <param name="staff"></param>
    /// <returns>若搜刮失败返回null</returns>
    public Task<Staff?> GetStaffAsync(Staff staff);

    /// <summary>
    /// 获取该游戏的所有staff信息（简略版），待后续进一步解析（如果设置中打开了解析游戏制作人的选项）<br/>
    /// 同一个Staff可以多次出现（多个职位），后续调用时会自行合并 <br/>
    /// 如果获取不到则返回空列表 <br/>
    /// <b>内部不捕获异常，调用方需捕获异常</b>
    /// </summary>
    /// <exception cref="HttpRequestException"></exception>
    /// <returns></returns>
    public Task<List<StaffRelation>> GetStaffsAsync(Galgame game);
}