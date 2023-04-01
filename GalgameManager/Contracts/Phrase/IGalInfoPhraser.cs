using GalgameManager.Models;
using GalgameManager.Services;

namespace GalgameManager.Contracts.Phrase;

public interface IGalInfoPhraser
{
    public Task<Galgame?> GetGalgameInfo(Galgame galgame);
    public RssType GetPhraseType();
}