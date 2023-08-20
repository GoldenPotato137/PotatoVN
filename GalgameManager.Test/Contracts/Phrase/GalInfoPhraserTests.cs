using System.Diagnostics.CodeAnalysis;
using GalgameManager.Contracts.Phrase;

namespace GalgameManager.Test.Contracts.Phrase;

[TestFixture]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public class GalInfoPhraserTests
{
    [Test]
    [TestCase("bacde", "abed", 0.672)]
    [TestCase("FAREMVIEL", "FARMVILLE", 0.88425)]
    [TestCase("AMBITIOUS MISSION", "ambitious mission", 1)]
    public void SimilarityTest(string s1, string s2, double target)
    {
        // Arrange
        // Act
        var result = IGalInfoPhraser.Similarity(s1, s2);
        // Console.WriteLine(result);
        // Assert
        if (Math.Abs(result - target) > 0.001)
            Assert.Fail();
        else
            Assert.Pass();
    }
}