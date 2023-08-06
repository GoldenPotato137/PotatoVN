using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
public class VndbPhraserTest
{
    private VndbPhraser _vndbPhraser;

    [SetUp]
    public void Init()
    {
        _vndbPhraser = new VndbPhraser();
    }
    
    [Test]
    [TestCase("スタディ§ステディ")]
    [TestCase("サノバウィッチ")]
    [TestCase("喫茶ステラと死神の蝶")]
    public async Task PhraseTest(string name)
    {
        // Arrange
        Galgame? game = new(name);
        // Act
        game = await _vndbPhraser.GetGalgameInfo(game);
        // Assert
        if(game == null)
        {
            Assert.Fail();
            return;
        }
        
        switch (name)
        {
            case "スタディ§ステディ":
                if(game.Name != "Study § Steady") Assert.Fail();
                if(game.Id != "24689") Assert.Fail();
                break;
            case "サノバウィッチ":
                if(game.Id != "16044") Assert.Fail();
                break;
            case "喫茶ステラと死神の蝶":
                if (game.Id != "26414") Assert.Fail();
                break;
        }
        
        Assert.Pass();
    }

    [Test]
    [TestCase("22423")] // 樱之诗 - 在樱花之森上飞舞
    public async Task PhraseWithIdTest(string id)
    {
        // Arrange
        Galgame? game = new()
        {
            RssType = RssType.Bangumi,
            Id = id
        };
        // Act
        game = await _vndbPhraser.GetGalgameInfo(game);
        // Assert
        if(game == null)
        {
            Assert.Fail();
            return;
        }

        switch (id)
        {
            case "22423":
                if (game.Id != "22423") Assert.Fail();
                if (game.Name != "サクラノ詩 —櫻の森の上を舞う—") Assert.Fail();
                if (game.Developer != "枕") Assert.Fail();
                break;
        }
        
        Assert.Pass();
    }
}