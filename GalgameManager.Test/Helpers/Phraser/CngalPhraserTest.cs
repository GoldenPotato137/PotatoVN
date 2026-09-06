using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
[Category("Phraser")]
public class CngalPhraserTest
{
    private CngalPhraser _phraser = new();

    [SetUp]
    public void Init()
    {
        _phraser = new();
    }

    [Test]
    [TestCase("三色绘恋")]
    public async Task PhraseTest(string name)
    {
        Galgame? game = new(name);
        game = await _phraser.GetGalgameInfo(game);
        if(game == null)
        {
            Assert.Fail();
            return;
        }

        if(game.Name != "三色△绘恋") Assert.Fail();
        if(game.Id != "80") Assert.Fail();
        if(game.Developer != "绘恋制作组") Assert.Fail();
        if(game.ReleaseDate != new DateTime(2017, 9, 21)) Assert.Fail();
        if(game.Characters.Count != 9) Assert.Fail();
        Assert.Pass();
    }
}
