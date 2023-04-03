using GalgameManager.Models;
using GalgameManager.Services;

namespace GalgameManager.Contracts.Phrase;

public interface IGalInfoPhraser
{
    /// <summary>
    /// 从rss中获取galgame信息
    /// </summary>
    /// <param name="galgame">galgame</param>
    /// <returns>获取到的galgame信息（放到一个空的galgame里），如果获取不到信息则返回null</returns>
    public Task<Galgame?> GetGalgameInfo(Galgame galgame);
    
    public RssType GetPhraseType();
}