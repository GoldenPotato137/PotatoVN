using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
[Category("Phraser")]
public class BangumiPhraserTest
{
    private BgmPhraser _phraser = new(new BgmPhraserData());

    [SetUp]
    public void Init()
    {
        var token = Environment.GetEnvironmentVariable("BGM_TOKEN"); // 请在环境变量中设置 BGM_TOKEN
        Assert.That(token, Is.Not.Null.Or.Empty, "请在环境变量中设置 BGM_TOKEN");
        BgmPhraserData data = new()
        {
            Token = string.IsNullOrEmpty(token) ? null : token
        };
        _phraser = new BgmPhraser(data);
    }

    [Test]
    [TestCase("ambitious_mission", "360498")]
    [TestCase("月に寄りそう乙女の作法", "44123", "月に寄りそう乙女の作法")]
    [TestCase("近月少女的礼仪2", "105074")]
    [TestCase("NEKO-NIN exHeart", "201102")]
    [TestCase("千恋＊万花", "172612")]
    [TestCase("猫忍之心SPIN！", "441480")]
    [TestCase("あまいろショコラータ3", "413620")] //容易和1混淆
    [TestCase("あまいろショコラータ2", "325937")]
    public async Task ParseGameTest(string name, string targetId, string? targetName = null)
    {
        Galgame? game = new(name);
        game = await _phraser.GetGalgameInfo(game);
        ParserTestUtil.CheckGame(game, targetId, expectedName: targetName);
    }

    [Test]
    [TestCase("22423", "サクラノ詩 —櫻の森の上を舞う—"/*, "枕"*/)] // 樱之诗 - 在樱花之森上飞舞(bgm上这个条目的infobox没有开发商信息了)
    public async Task ParseGameWithIdTest(string id, string? targetName=null, string? targetDeveloper=null)
    {
        Galgame? game = new()
        {
            RssType = RssType.Bangumi,
            Id = id
        };
        game = await _phraser.GetGalgameInfo(game);
        ParserTestUtil.CheckGame(game, id, expectedName: targetName, expectedDeveloper: targetDeveloper);
    }

    [Test]
    [TestCase("34877", "八日なのか", null, "シナリオライター")]
    [TestCase("7214", "天都", "アマト", "ASa Projectのディレ")]
    [TestCase("11268", "柚子奈妃世", "柚子奈ひよ", "minori「夏空")]
    public async Task ParseStaffWithIdTest(string id, string? targetName, string? targetJapaneseName,
        string? targetDescription)
    {
        Staff? staff = new Staff { Ids = { [(int)RssType.Bangumi] = id } };
        staff = await _phraser.GetStaffAsync(staff);
        ParserTestUtil.CheckStaff(staff, RssType.Bangumi, expectedId: id, expectedDescription: targetDescription,
            expectedJapaneseName: targetJapaneseName);
    }

    [Test]
    [TestCase("ゆずソフト", "https://lain.bgm.tv/pic/crt/l/1c/fd/7175_prsn_k7z6x.jpg?r=1553269010")]
    public async Task GetDeveloperImageUrlAsyncTest(string name, string url)
    {
        var imageUrl = await _phraser.GetDeveloperImageUrlAsync(name);
        Assert.That(url, Is.EqualTo(imageUrl));
    }
}
