using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
public class YmgalPhraserTest
{
    private YmgalPhraser _ymgalPhraser;

    [SetUp]
    public void Init()
    {
        _ymgalPhraser = new YmgalPhraser();
    }
    
    [Test]
    public async Task PhraseTest()
    {
        Galgame? game = new("千恋万花");
        await _ymgalPhraser.GetGalgameInfo(game);
        Assert.Pass();
    }
}