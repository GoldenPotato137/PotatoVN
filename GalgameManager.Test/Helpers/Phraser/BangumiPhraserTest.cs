using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
public class BangumiPhraserTest
{
    private BgmPhraser _phraser = new(new BgmPhraserData());
    
    [SetUp]
    public void Init()
    {
        var token = Environment.GetEnvironmentVariable("BGM_TOKEN"); // 请在环境变量中设置 BGM_TOKEN
        BgmPhraserData data = new()
        {
            Token = string.IsNullOrEmpty(token) ? null : token
        };
        _phraser = new BgmPhraser(data);
    }

    [Test]
    [TestCase("ambitious_mission")]
    [TestCase("月に寄りそう乙女の作法")]
    [TestCase("近月少女的礼仪2")]
    public async Task PhraseTest(string name)
    {
        // Arrange
        Galgame? game = new(name);
        // Act
        game = await _phraser.GetGalgameInfo(game, null);
        // Assert
        if(game == null)
        {
            Assert.Fail();
            return;
        }
        
        switch (name)
        {
            case "月に寄りそう乙女の作法":
                if(game.Name != "月に寄りそう乙女の作法") Assert.Fail();
                if(game.Id != "44123") Assert.Fail();
                break;
            case "近月少女的礼仪2":
                if(game.Id != "105074") Assert.Fail();
                break;
            case "ambitious_mission":
                if (game.Id != "360498") Assert.Fail();
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
        game = await _phraser.GetGalgameInfo(game, null);
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