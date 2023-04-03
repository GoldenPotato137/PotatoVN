using System.Diagnostics.CodeAnalysis;

using GalgameManager.Contracts.Phrase;
using GalgameManager.Models;
using GalgameManager.Services;

using VndbSharp;
using VndbSharp.Models;
using VndbSharp.Models.Errors;

namespace GalgameManager.Helpers.Phrase;

[SuppressMessage("ReSharper", "EnforceIfStatementBraces")]
public class VndbPhraser : IGalInfoPhraser
{
    private readonly Vndb _vndb;

    public VndbPhraser()
    {
        _vndb = new Vndb(true).WithClientDetails("GalgameManager", "1.0-dev").WithFlagsCheck(true);
    }
    
    public async Task<Galgame?> GetGalgameInfo(Galgame galgame)
    {
        var result = new Galgame();
        try
        {
            var visualNovels = await _vndb.GetVisualNovelAsync(VndbFilters.Search.Fuzzy(galgame.Name), VndbFlags.FullVisualNovel);
            if (visualNovels == null || visualNovels.Count == 0)
            {
                var error = _vndb.GetLastError();
                if (error is not { Type: ErrorType.Throttled }) return null;
                await Task.Delay(60 * 1000); // 1 minute
                visualNovels = await _vndb.GetVisualNovelAsync(VndbFilters.Search.Fuzzy(galgame.Name), VndbFlags.FullVisualNovel);
                if (visualNovels == null) return null;
            }
            var rssItem = visualNovels.Items[0];
            result.Name = rssItem.Name;
            result.Description = rssItem.Description;
            result.RssType = GetPhraseType();
            result.Id = rssItem.Id.ToString();
            result.Rating = (float)rssItem.Rating;
            result.ExpectedPlayTime = rssItem.Length.ToString() ?? Galgame.DefaultString;
            result.ImageUrl = rssItem.Image;
        }
        catch (Exception)
        {
            return null;
        }
        return result;
    }

    public RssType GetPhraseType() => RssType.Vndb;
}
