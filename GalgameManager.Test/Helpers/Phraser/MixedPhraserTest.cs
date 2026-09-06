using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
[Category("Phraser")]
public class MixedPhraserTest
{
    private MixedPhraser? _mixedPhraser;
    private BgmPhraser _bgmPhraser = null!;
    private VndbPhraser _vndbPhraser = null!;
    private YmgalPhraser _ymgalPhraser = null!;
    private SteamParser _steamParser = null!;
    private HikarinagiPhraser _hikarinagiPhraser = null!;
    
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
        _ymgalPhraser = new();
        _steamParser = new SteamParser("schinese");
        _hikarinagiPhraser = new HikarinagiPhraser();
        // 混合源默认已关闭Bangumi搜刮器（见MixedPhraserEnabled），本测试类断言了Bangumi的数据，故显式开启
        _mixedPhraser = new MixedPhraser(_bgmPhraser, _vndbPhraser, _ymgalPhraser, _steamParser, _hikarinagiPhraser, new MixedPhraserData
        {
            Order = new MixedPhraserOrder().SetToDefault(),
            Enabled = new MixedPhraserEnabled { BangumiEnabled = true },
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
                Assert.That(game.Ids[(int)RssType.Bangumi], Is.EqualTo("44123"));
                Assert.That(game.Ids[(int)RssType.Vndb], Is.EqualTo("10680"));
                Assert.That(game.Ids[(int)RssType.Ymgal], Is.EqualTo("31147"));
                if(!game.Description.Value!.StartsWith("主人公身为“大藏游星”")) Assert.Fail(); // from BGM
                if(game.Developer != "Navel") Assert.Fail(); // from VNDB
                // from STEAM
                Assert.That(game.ImageUrl?.StartsWith("https://cdn.akamai.steamstatic.com/steam/apps/1776970/library_600x900.jpg"), Is.True);
                break;
        }
        
        Assert.Pass();
    }
    
    [Test]
    [TestCase("千恋万花")]
    [TestCase("Ever17 -the out of infinity-")]
    public async Task PhraseTestWithCustomOrder(string name)
    {
        // Arrange
        Galgame? game = new(name);
        MixedPhraserOrder order = new MixedPhraserOrder().SetToDefault();
        order.NameOrder = new() { RssType.Vndb, RssType.Bangumi };
        order.ImageUrlOrder = new() { RssType.Bangumi, RssType.Vndb };
        order.DescriptionOrder = new() { RssType.Vndb, RssType.Bangumi };
        MixedPhraser phraser = new MixedPhraser(_bgmPhraser, _vndbPhraser, _ymgalPhraser, _steamParser, _hikarinagiPhraser, new MixedPhraserData
        {
            Order = order,
            Enabled = new MixedPhraserEnabled { BangumiEnabled = true }, // 默认配置已关闭Bangumi，这里断言了Bangumi的数据，需显式开启
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
            case "千恋万花":
                // VNDB搜不到游戏，fallback到Bangumi  update：VNDB已经有了（2024-11-13）
                // Assert.That(game.Description.Value?.StartsWith("電車も通っていない山の中に"), Is.True); // 从BGM中获取
                break;
            case "Ever17 -the out of infinity-":
                Assert.That(game.Name.Value, Is.EqualTo("Ever17 -the out of infinity-")); // 从VNDB中获取
                Assert.That(game.Ids[(int)RssType.Bangumi], Is.EqualTo("1126")); // 未登录状态下无法找到游戏
                Assert.That(game.Ids[(int)RssType.Vndb], Is.EqualTo("17"));
                Assert.That(game.Ids[(int)RssType.Ymgal], Is.EqualTo("10799"));
                Assert.That(game.Description.Value?.StartsWith("Ever17 is the tale of seven individuals"), Is.True); // 从VNDB中获取
                Assert.That(game.ImageUrl?.StartsWith("https://lain.bgm.tv/"), Is.True); // 从BGM中获取
                break;
        }
        
        Assert.Pass();
    }
    
    
    [Test]
    [TestCase("千恋＊万花")]
    public async Task ParseTestWithCustomEnabled(string name)
    {
        // Arrange
        Galgame? game = new(name);
        MixedPhraserEnabled enabled = new()
        {
            BangumiEnabled = false,
            VndbEnabled = false,
        };
        MixedPhraser phraser = new(_bgmPhraser, _vndbPhraser, _ymgalPhraser, _steamParser, _hikarinagiPhraser, new MixedPhraserData
        {
            Order = new MixedPhraserOrder().SetToDefault(),
            Enabled = enabled,
        });
        // Act
        game = await phraser.GetGalgameInfo(game);
        // Assert
        if(game == null)
        {
            Assert.Fail();
            return;
        }
        Assert.That(string.IsNullOrEmpty(game.Ids[(int)RssType.Bangumi]));
        Assert.That(string.IsNullOrEmpty(game.Ids[(int)RssType.Vndb]));
        Assert.That(game.Ids[(int)RssType.Ymgal], Is.EqualTo("22374"));
        Assert.That(game.Ids[(int)RssType.Steam], Is.EqualTo("1144400"));
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