using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
[Category("Phraser")]
public class VndbPhraserTest
{
    private VndbPhraser _vndbPhraser = null!;

    [SetUp]
    public void Init()
    {
        _vndbPhraser = new VndbPhraser();
    }

    [Test]
    public async Task ParseGameWithOnlineNameSearch_ShouldPreferSearchRank()
    {
        // 使用不存在的 release ID 绕过本地名称映射，强制回退到 VNDB 在线名称搜索。
        Galgame input = new("ISLAND");
        input.Ids[(int)RssType.Vndb] = "r999999";

        Galgame? game = await _vndbPhraser.GetGalgameInfo(input);

        Assert.That(game, Is.Not.Null);
        Assert.That(game!.Id, Is.EqualTo("18498"));
    }

    [Test]
    [TestCase("r2166", "v1129")]
    public async Task GetVndbIdFromReleaseId_ShouldResolve_ToVnId(string releaseId, string expectedVnId)
    {
        var vnId = await _vndbPhraser.GetVndbIdFromReleaseId(releaseId);
        Assert.That(vnId, Is.EqualTo(expectedVnId));
    }

    [Test]
    [TestCase("r2166", "メタモルファンタジーSP", "https://t.vndb.org/cv/15/89715.jpg", "エスクード")]
    public async Task ParseGameWithReleaseId_ShouldPreferReleaseTitleAndCover(
        string releaseId,
        string expectedReleaseTitleJa,
        string expectedReleaseCoverUrl,
        string expectedDeveloper)
    {
        // 这里用一个“无关名称”来确保解析是由 releaseId 驱动，而不是依赖 search/name。
        Galgame input = new("dummy");
        input.Ids[(int)RssType.Vndb] = releaseId;

        Galgame? game = await _vndbPhraser.GetGalgameInfo(input);

        Assert.That(game, Is.Not.Null);
        Assert.That(game!.Id, Is.EqualTo(releaseId));
        Assert.That(game.Name.Value, Is.EqualTo(expectedReleaseTitleJa));
        Assert.That(game.ImageUrl, Is.EqualTo(expectedReleaseCoverUrl));
        Assert.That(game.Developer.Value, Is.EqualTo(expectedDeveloper));
    }

    [Test]
    [TestCase("スタディ§ステディ", "24689", "スタディ§ステディ", null, new string[] { })]
    [TestCase("サノバウィッチ", "16044", null, null, new string[] { })]
    [TestCase("喫茶ステラと死神の蝶", "26414", null, "星光咖啡馆与死神之蝶", new[] { "明月 栞那" })]
    // 特例：Description为空
    [TestCase("妹調教日記～こんなツンデレが俺の妹なわけない!～", "9303", null, "妹调教日记", new string[] { })]
    [TestCase("あまいろショコラータ3", "41339", "あまいろショコラータ 3", null, new string[] { })]
    public async Task ParseGameWithNameTest(
        string inputGameName,
        string expectedId,
        string? expectedName,
        string? expectedCnName,
        string[] expectedCharacterNames)
    {
        Galgame? game = new(inputGameName);
        game = await _vndbPhraser.GetGalgameInfo(game);
        ParserTestUtil.CheckGame(game, expectedId, expectedName: expectedName, expectedCnName: expectedCnName,
            characterPhraser: _vndbPhraser, expectedCharacterNames: expectedCharacterNames);
    }

    [Test]
    [TestCase("八日 なのか", "s4808", null)]
    [TestCase("Amamiya Ritsu", "s2883", "Amamiya Ritsu is a Japanese")]
    public async Task ParseStaffWithNameTest(string name, string expectedId, string? expectedDescription)
    {
        Staff? staff = new() { JapaneseName = name };
        staff = await _vndbPhraser.GetStaffAsync(staff);
        ParserTestUtil.CheckStaff(staff, RssType.Vndb, expectedId, expectedDescription);
    }

    [Test]
    [TestCase("s4883", "冬壱 もんめ", "Fuyuichi Monme", null)]
    public async Task ParseStaffWithIdTest(string id, string? expectedJapaneseName, string? expectedEnglishName,
        string? expectedDescription)
    {
        Staff? staff = new() { Ids = { [(int)RssType.Vndb] = id } };
        staff = await _vndbPhraser.GetStaffAsync(staff);
        ParserTestUtil.CheckStaff(staff, RssType.Vndb, id, expectedDescription,
            expectedJapaneseName: expectedJapaneseName,
            expectedEnglishName: expectedEnglishName);
    }

    [Test]
    [TestCase("紙の上の魔法使い", "High School Student Heroine", "Brother/Sister Romance")]
    [TestCase("Gyakuten Saiban", "Mystery", "Falsely Accused")]
    public async Task ParseGameWithIdandEnglishTest(string name, string expectedTag1, string expectedTag2)
    {
        // 没有Token，不是中文环境
        VndbPhraserData data = new(null, false);
        _vndbPhraser.UpdateData(data);

        Galgame? game = new(name);
        game = await _vndbPhraser.GetGalgameInfo(game);


        Assert.That(game, Is.Not.Null);
        Assert.That(game.Tags.Value, Has.Member(expectedTag1));
        Assert.That(game.Tags.Value, Has.Member(expectedTag2));
    }

    [Test]
    [TestCase("紙の上の魔法使い", "傲娇女主角", "主人公的妹妹女主角")]
    [TestCase("Gyakuten Saiban", "智斗", "被冤枉")]
    public async Task ParseGameWithIdandChineseTest(string name, string expectedTag1, string expectedTag2)
    {
        // 没有Token，不是中文环境
        VndbPhraserData data = new(null, true);
        _vndbPhraser.UpdateData(data);

        Galgame? game = new(name);
        game = await _vndbPhraser.GetGalgameInfo(game);


        Assert.That(game, Is.Not.Null);
        Assert.That(game.Tags.Value, Has.Member(expectedTag1));
        Assert.That(game.Tags.Value, Has.Member(expectedTag2));
    }
}
