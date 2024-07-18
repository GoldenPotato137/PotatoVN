using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
public class MixedPhraserTest
{
    private MixedPhraser? _mixedPhraser;
    private BgmPhraser _bgmPhraser = null!;
    private VndbPhraser _vndbPhraser = null!;
    
    [SetUp]
    public void Init()
    {
        var token = Environment.GetEnvironmentVariable("BGM_TOKEN"); // 请在环境变量中设置 BGM_TOKEN
        BgmPhraserData data = new()
        {
            Token = string.IsNullOrEmpty(token) ? null : token
        };
        _bgmPhraser = new(data);
        _vndbPhraser = new();
        _mixedPhraser = new MixedPhraser(_bgmPhraser, _vndbPhraser, new MixedPhraserData
        {
            Order = new MixedPhraserOrder().SetToDefault(),
        });
    }

    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task PhraseTest(string name)
    {
        // Arrange
        Galgame? game = new(name);
        // Act
        game = await _mixedPhraser!.GetGalgameInfo(game);
        // Assert
        if(game == null)
        {
            Assert.Fail();
            return;
        }

        switch (name)
        {
            case "近月少女的礼仪":
                if(game.Name != "月に寄りそう乙女の作法") Assert.Fail();
                if(game.Id != "bgm:44123,vndb:10680") Assert.Fail();
                if(!game.Description.Value!.StartsWith("主人公身为“大藏游星”")) Assert.Fail();
                if(game.Developer != "Navel") Assert.Fail();
                Assert.That(game.ImageUrl?.StartsWith("https://t.vndb.org/"), Is.True); //默认来自VNDB
                break;
        }
        
        Assert.Pass();
    }
    
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task PhraseTestWithCustomOrder(string name)
    {
        // Arrange
        Galgame? game = new(name);
        MixedPhraserOrder order = new MixedPhraserOrder().SetToDefault();
        order.NameOrder = new() { RssType.Vndb, RssType.Bangumi };
        order.ImageUrlOrder = new() { RssType.Bangumi, RssType.Vndb };
        order.DescriptionOrder = new() { RssType.Vndb, RssType.Bangumi };
        MixedPhraser phraser = new MixedPhraser(_bgmPhraser, _vndbPhraser, new MixedPhraserData
        {
            Order = order,
        });
        // Act
        game = await phraser.GetGalgameInfo(game);
        // Assert
        if(game == null)
        {
            Assert.Fail();
            return;
        }

        switch (name)
        {
            case "近月少女的礼仪":
                Assert.That(game.Name.Value, Is.EqualTo("Tsuki ni Yorisou Otome no Sahou")); // 从VNDB中获取
                Assert.That(game.Id, Is.EqualTo("bgm:44123,vndb:10680"));
                Assert.That(game.Description.Value?.StartsWith("Navel tenth anniversary project"), Is.True); // 从VNDB中获取
                Assert.That(game.ImageUrl?.StartsWith("https://lain.bgm.tv/"), Is.True); // 从BGM中获取
                break;
        }
        
        Assert.Pass();
    }

    [Test]
    [TestCase("44123","10680")] // 近月少女的礼仪
    public async Task PhraseTestWithId(string bgmId, string vndbId)
    {
        // Arrange
        Galgame? game = new("极道胁迫！逆袭的运动部员们"); // 故意使用错误的名字
        game.RssType = RssType.Mixed;
        game.Id = $"bgm:{bgmId},vndb:{vndbId}";
        // Act
        game = await _mixedPhraser!.GetGalgameInfo(game);
        // Assert
        if(game == null)
        {
            Assert.Fail();
            return;
        }

        switch (bgmId)
        {
            case "44123":
                if(game.Developer != "Navel") Assert.Fail(); // 从VNDB中获取
                break;
        }
        
        Assert.Pass();
    }
}