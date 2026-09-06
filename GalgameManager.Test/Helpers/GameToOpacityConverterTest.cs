using GalgameManager.Helpers.Converter;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class GameToOpacityConverterTest
{
    [TearDown]
    public void TearDown()
    {
        GameToOpacityConverter.SpecialDisplayVirtualGame = false;
    }

    [TestCase(true, 1d)]
    [TestCase(false, 0.5d)]
    public void Convert_UsesCurrentLocalGameState(bool isLocalGame, double expectedOpacity)
    {
        GameToOpacityConverter.SpecialDisplayVirtualGame = true;
        GameToOpacityConverter converter = new();

        object result = converter.Convert(isLocalGame, typeof(double), null!, string.Empty);

        Assert.That(result, Is.EqualTo(expectedOpacity));
    }
}
