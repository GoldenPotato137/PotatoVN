using System.Diagnostics.CodeAnalysis;
using GalgameManager.Contracts.Phrase;

namespace GalgameManager.Test.Contracts.Phrase;

[TestFixture]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
public class GalInfoPhraserTests
{
    [Test]
    public void SimilarityTest1()
    {
        // Arrange
        var s1 = "bacde";
        var s2 = "abed";
        // Act
        var result = IGalInfoPhraser.Similarity(s1, s2);
        // Console.WriteLine(result);
        // Assert
        if(Math.Abs(result - 0.672) > 0.001)
            Assert.Fail();
        else
            Assert.Pass();
    }
    
    [Test]
    public void SimilarityTest2()
    {
        // Arrange
        var s1 = "FAREMVIEL";
        var s2 = "FARMVILLE";
        // Act
        var result = IGalInfoPhraser.Similarity(s1, s2);
        //Console.WriteLine(result);
        // Assert
        if(Math.Abs(result - 0.88425) > 0.001)
            Assert.Fail();
        else
            Assert.Pass();
    }
}