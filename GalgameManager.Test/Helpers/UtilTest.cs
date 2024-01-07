using System.Diagnostics.CodeAnalysis;
using GalgameManager.Helpers;

namespace GalgameManager.Test.Helpers;

[TestFixture]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public class UtilTest
{
    [Test]
    [TestCase("废萌","fm",true)]
    [TestCase("废萌","feimeng",true)]
    [TestCase("废萌","飞梦",false)]
    public void ContainXTest(string self, string target, bool result)
    {
        Assert.That(self.ContainX(target), Is.EqualTo(result));
    }
}