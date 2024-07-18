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
    // 特例：Description为空
    [TestCase("妹調教日記～こんなツンデレが俺の妹なわけない!～")]
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
                if (game.Id != "26414" || game.CnName != "星光咖啡馆与死神之蝶") Assert.Fail();
                if (game.Characters.All(
                        c => _vndbPhraser.GetGalgameCharacter(c).Result?.Name != "明月 栞那")) Assert.Fail();
                break;
            case "妹調教日記～こんなツンデレが俺の妹なわけない!～":
                if (game.Id != "9303" || game.CnName != "妹调教日记") Assert.Fail();
                break;
        }
        
        Assert.Pass();
    }
}