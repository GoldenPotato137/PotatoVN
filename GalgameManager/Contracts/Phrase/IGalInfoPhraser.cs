using GalgameManager.Models;
using GalgameManager.Services;

namespace GalgameManager.Contracts.Phrase;

public interface IGalInfoPhraser
{
    public Task<Galgame?> GetGalgameInfo(string name);
    public RssType GetPhraseType();
}