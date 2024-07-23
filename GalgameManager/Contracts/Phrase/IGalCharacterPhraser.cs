using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Phrase;

/// <summary>
/// 实现：基于 IGalInfoPhraser 需要在 GetGalgameInfo 中实现读取基本信息功能
/// 在通过 IGalCharacterPhraser.GetGalgameCharacter 读取完整信息
/// </summary>
public interface IGalCharacterPhraser
{
    public RssType GetPhraseType();
    
    public Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter);
}