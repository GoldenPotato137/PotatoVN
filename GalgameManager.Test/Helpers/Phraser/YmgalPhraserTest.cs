using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
[Category("Phraser")]
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

    [Test]
    [TestCase("ambitious_mission", "41186")]
    [TestCase("月に寄りそう乙女の作法", "31147", "月に寄りそう乙女の作法")]
    [TestCase("近月少女的礼仪2", "22952")]
    [TestCase("千恋＊万花", "22374")]
    [TestCase("猫忍之心SPIN！", "56234")]
    // [TestCase("NEKO-NIN exHeart", "201102")] // 这个名字网站上也搜索不出来，没辙了
    [TestCase("猫忍えくすはーと", "26440")]
    [TestCase("あまいろショコラータ 3", "51599")] //容易和1混淆
    [TestCase("あまいろショコラータ２", "38042")] //2是全角字符，不然会和1混起来
    [TestCase("あまいろショコラータ2", "38042")] //2是半角字符，鲁棒性
    [TestCase("あまいろショコラータ 2", "38042")] //加空格
    public async Task ParseGameTest(string name, string targetId, string? targetName = null)
    {
        Galgame? game = new(name);
        game = await _ymgalPhraser.GetGalgameInfo(game);
        ParserTestUtil.CheckGame(game, targetId, expectedName: targetName);
    }
}
