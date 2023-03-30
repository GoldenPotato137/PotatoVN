using GalgameManager.Models;

namespace GalgameManager.Contracts.Phrase;

public interface IGalInfoPhraser
{
    public Task<Galgame?> GetGalgameInfo(string name);
}