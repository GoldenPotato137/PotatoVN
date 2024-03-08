using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Contracts.Phrase;

public interface IGalCharacterPhraser
{
    public RssType GetPhraseType();
    
    public Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter galgameCharacter);
}