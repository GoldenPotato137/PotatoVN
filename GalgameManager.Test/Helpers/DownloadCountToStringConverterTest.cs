using GalgameManager.Helpers.Converter;

namespace GalgameManager.Test.Helpers;

[TestFixture]
public class DownloadCountToStringConverterTest
{
    [TestCase(0L, "0")]
    [TestCase(999L, "999")]
    [TestCase(1000L, "1K")]
    [TestCase(1499L, "1.4K")]
    [TestCase(12688L, "12.6K")]
    [TestCase(999_999L, "999.9K")]
    [TestCase(1_000_000L, "1M")]
    [TestCase(2_560_000L, "2.5M")]
    public void Convert_FormatsCompactly(long count, string expected)
    {
        Assert.That(DownloadCountToStringConverter.Convert(count), Is.EqualTo(expected));
    }

    [Test]
    public void Convert_NonLongValue_ReturnsEmpty()
    {
        DownloadCountToStringConverter converter = new();

        object result = converter.Convert("123", typeof(string), null!, string.Empty);

        Assert.That(result, Is.EqualTo(string.Empty));
    }
}
