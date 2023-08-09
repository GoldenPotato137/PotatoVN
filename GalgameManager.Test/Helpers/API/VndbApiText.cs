using System.Text.Json;
using GalgameManager.Helpers.API;

namespace GalgameManager.Test.Helpers.API;

[TestFixture]
public class VndbApiText
{
    [Test]
    [TestCase("SnakeNamingPolicy", "snake_naming_policy")]
    public void SnakeNamingPolicyTest(string name, string target)
    {
        JsonNamingPolicy snakeNamingPolicy = new SnakeNamingPolicy();
        Assert.That(snakeNamingPolicy.ConvertName(name), Is.EqualTo(target));
    }
}