using GalgameManager.Helpers.Phrase;

namespace GalgameManager.Test.Helpers.Phraser;

[TestFixture]
public class PhraseHelperTest
{
    [Test]
    [TestCase("恋爱成双", 32811)]
    [TestCase("ambitious mission", 33036)]
    [TestCase("近月少女的礼仪2.2 A×L+SA!!", 21501)]
    [TestCase("魔女的夜宴", 16044)]
    [TestCase("糖调！-sugarfull tempering-", 20196)]
    [TestCase("近月少女的礼仪", 10680)]
    [TestCase("恋爱×决胜战", 28633)]
    [TestCase("冥契的牧神节", 29383)]
    // [TestCase("大图书馆的牧羊人 -Dreaming Sheep-", 12480)] // 算法保守程度调整，暂时无法获取
    public async Task TryGetVndbIdAsyncTest(string name, int target)
    {
        // Arrange
        // Act
        var id = await PhraseHelper.TryGetVndbIdAsync(name);
        // Assert
        Assert.That(id, Is.EqualTo(target));
    }

    [Test]
    // [TestCase("青春好奇相伴的三角恋爱", 183145)] // 算法保守程度调整，暂时无法获取
    [TestCase("恋爱成双", 356907)]
    [TestCase("星光咖啡馆与死神之蝶", 289599)]
    [TestCase("少女理论及其之后的周边 -美好年代篇-", 143835)]
    // [TestCase("大图书馆的牧羊人 - Dreaming Sheep", 70817)] // 算法保守程度调整，暂时无法获取
    [TestCase("美少女万华镜 -理与迷宫的少女-", 295320)]
    [TestCase("近月少女的礼仪", 44123)]
    // [TestCase("千恋万花", 172612)] // 算法保守程度调整，暂时无法获取
    [TestCase("水仙", 1167)]
    public async Task TryGetBgmIdAsyncTest(string name, int target)
    {
        // Arrange
        // Act
        var id = await PhraseHelper.TryGetBgmIdAsync(name);
        // Assert
        Assert.That(id, Is.EqualTo(target));
    }
}