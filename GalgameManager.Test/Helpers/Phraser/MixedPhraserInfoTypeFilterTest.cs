using GalgameManager.Enums;
using GalgameManager.Helpers.Phrase;
using GalgameManager.Models;

namespace GalgameManager.Test.Helpers.Phraser;

/// <summary>
/// Tests for MixedPhraser information type filtering functionality.
/// Verifies that each information type (Name, Description, Developer, Tags, etc.)
/// can be individually enabled/disabled through MixedPhraserEnabled configuration.
/// </summary>
[TestFixture]
[Category("Phraser")]
public class MixedPhraserInfoTypeFilterTest
{
    private MixedPhraser? _mixedPhraser;
    private BgmPhraser _bgmPhraser = null!;
    private VndbPhraser _vndbPhraser = null!;
    private YmgalPhraser _ymgalPhraser = null!;
    private SteamParser _steamParser = null!;
    private HikarinagiPhraser _hikarinagiPhraser = null!;

    [SetUp]
    public void Init()
    {
        var token = Environment.GetEnvironmentVariable("BGM_TOKEN");
        BgmPhraserData data = new()
        {
            Token = string.IsNullOrEmpty(token) ? null : token
        };
        _bgmPhraser = new(data);
        _vndbPhraser = new VndbPhraser();
        _ymgalPhraser = new YmgalPhraser();
        _steamParser = new SteamParser("schinese");
        _hikarinagiPhraser = new HikarinagiPhraser();
    }

    /// <summary>
    /// Test that Name field is scraped when NameEnabled is true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task NameEnabled_WhenTrue_ShouldScrapeName(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { NameEnabled = true };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name.Value, Is.Not.Null);
        Assert.That(result.Name.Value, Is.Not.Empty);
        Assert.That(result.Name.Value, Is.EqualTo("月に寄りそう乙女の作法"));
    }

    /// <summary>
    /// Test that Name field is not scraped when NameEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task NameEnabled_WhenFalse_ShouldNotScrapeName(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { NameEnabled = false };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Name should remain empty since it wasn't scraped
        Assert.That(result!.Name.Value, Is.EqualTo(string.Empty));
    }

    /// <summary>
    /// Test that Description field is scraped when DescriptionEnabled is true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task DescriptionEnabled_WhenTrue_ShouldScrapeDescription(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { DescriptionEnabled = true };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Description.Value, Is.Not.Null);
        Assert.That(result.Description.Value, Is.Not.Empty);
        // 不断言具体文本：混合源默认已关闭Bangumi，简介来源会按DescriptionOrder在
        // Hikarinagi/Ymgal等源间回落（如限流时），各源的简介措辞不同且会随远端变化
    }

    /// <summary>
    /// Test that Description field is not scraped when DescriptionEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task DescriptionEnabled_WhenFalse_ShouldNotScrapeDescription(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { DescriptionEnabled = false };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Description should be empty/default when not scraped
        Assert.That(string.IsNullOrEmpty(result!.Description.Value), Is.True);
    }

    /// <summary>
    /// Test that Developer field is scraped when DeveloperEnabled is true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task DeveloperEnabled_WhenTrue_ShouldScrapeDeveloper(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { DeveloperEnabled = true };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Developer.Value, Is.Not.Null);
        Assert.That(result.Developer.Value, Is.EqualTo("Navel"));
    }

    /// <summary>
    /// Test that Developer field is not scraped when DeveloperEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task DeveloperEnabled_WhenFalse_ShouldNotScrapeDeveloper(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { DeveloperEnabled = false };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Developer should be default value when not scraped
        Assert.That(result!.Developer.Value, Is.EqualTo(Galgame.DefaultString));
    }

    /// <summary>
    /// Test that Tags field is scraped when TagsEnabled is true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task TagsEnabled_WhenTrue_ShouldScrapeTags(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { TagsEnabled = true };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Tags.Value, Is.Not.Null);
        Assert.That(result.Tags.Value!.Count, Is.GreaterThan(0));
    }

    /// <summary>
    /// Test that Tags field is not scraped when TagsEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task TagsEnabled_WhenFalse_ShouldNotScrapeTags(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { TagsEnabled = false };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Tags should be empty when not scraped
        Assert.That(result!.Tags.Value, Is.Null.Or.Empty);
    }

    /// <summary>
    /// Test that Rating field is scraped when RatingEnabled is true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task RatingEnabled_WhenTrue_ShouldScrapeRating(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { RatingEnabled = true };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Rating.Value, Is.GreaterThan(0));
    }

    /// <summary>
    /// Test that Rating field is not scraped when RatingEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task RatingEnabled_WhenFalse_ShouldNotScrapeRating(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { RatingEnabled = false };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Rating should be 0 (default) when not scraped
        Assert.That(result!.Rating.Value, Is.EqualTo(0));
    }

    /// <summary>
    /// Test that ExpectedPlayTime field is scraped when ExpectedPlayTimeEnabled is true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task ExpectedPlayTimeEnabled_WhenTrue_ShouldScrapeExpectedPlayTime(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { ExpectedPlayTimeEnabled = true };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // ExpectedPlayTime should have a value when scraped
        Assert.That(result!.ExpectedPlayTime.Value, Is.Not.EqualTo(Galgame.DefaultString));
    }

    /// <summary>
    /// Test that ExpectedPlayTime field is not scraped when ExpectedPlayTimeEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task ExpectedPlayTimeEnabled_WhenFalse_ShouldNotScrapeExpectedPlayTime(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { ExpectedPlayTimeEnabled = false };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // ExpectedPlayTime should be default when not scraped
        Assert.That(result!.ExpectedPlayTime.Value, Is.EqualTo(Galgame.DefaultString));
    }

    /// <summary>
    /// Test that ReleaseDate field is scraped when ReleaseDateEnabled is true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task ReleaseDateEnabled_WhenTrue_ShouldScrapeReleaseDate(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { ReleaseDateEnabled = true };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ReleaseDate.Value, Is.Not.EqualTo(DateTime.MinValue));
    }

    /// <summary>
    /// Test that ReleaseDate field is not scraped when ReleaseDateEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task ReleaseDateEnabled_WhenFalse_ShouldNotScrapeReleaseDate(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { ReleaseDateEnabled = false };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // ReleaseDate should be MinValue when not scraped
        Assert.That(result!.ReleaseDate.Value, Is.EqualTo(DateTime.MinValue));
    }

    /// <summary>
    /// Test that CnName field is scraped when CnNameEnabled is true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task CnNameEnabled_WhenTrue_ShouldScrapeCnName(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { CnNameEnabled = true };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.CnName, Is.Not.Null);
        Assert.That(result.CnName, Is.Not.Empty);
    }

    /// <summary>
    /// Test that CnName field is not scraped when CnNameEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task CnNameEnabled_WhenFalse_ShouldNotScrapeCnName(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { CnNameEnabled = false };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // CnName should be empty when not scraped
        Assert.That(string.IsNullOrEmpty(result!.CnName), Is.True);
    }

    /// <summary>
    /// Test that ImageUrl field is scraped when ImageUrlEnabled is true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task ImageUrlEnabled_WhenTrue_ShouldScrapeImageUrl(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { ImageUrlEnabled = true };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ImageUrl, Is.Not.Null);
        Assert.That(result.ImageUrl, Is.Not.Empty);
        Assert.That(result.ImageUrl!.StartsWith("http"), Is.True);
    }

    /// <summary>
    /// Test that ImageUrl field is not scraped when ImageUrlEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task ImageUrlEnabled_WhenFalse_ShouldNotScrapeImageUrl(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { ImageUrlEnabled = false };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // ImageUrl should be null or empty when not scraped
        Assert.That(string.IsNullOrEmpty(result!.ImageUrl), Is.True);
    }

    /// <summary>
    /// Test that Characters field is scraped when CharactersEnabled is true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task CharactersEnabled_WhenTrue_ShouldScrapeCharacters(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { CharactersEnabled = true };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Characters, Is.Not.Null);
        Assert.That(result.Characters.Count, Is.GreaterThan(0));
    }

    /// <summary>
    /// Test that Characters field is not scraped when CharactersEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task CharactersEnabled_WhenFalse_ShouldNotScrapeCharacters(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled { CharactersEnabled = false };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Characters should be empty when not scraped
        Assert.That(result!.Characters, Is.Null.Or.Empty);
    }

    /// <summary>
    /// Test that multiple information types can be disabled simultaneously
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task MultipleTypesDisabled_ShouldNotScrapeDisabledTypes(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled
        {
            DescriptionEnabled = false,
            DeveloperEnabled = false,
            TagsEnabled = false
        };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Disabled fields should not be scraped
        Assert.That(string.IsNullOrEmpty(result!.Description.Value), Is.True);
        Assert.That(result.Developer.Value, Is.EqualTo(Galgame.DefaultString));
        Assert.That(result.Tags.Value, Is.Null.Or.Empty);

        // Enabled fields (by default) should still be scraped
        Assert.That(result.Name.Value, Is.Not.Empty);
        Assert.That(result.Rating.Value, Is.GreaterThan(0));
    }

    /// <summary>
    /// Test that all information types can be disabled simultaneously
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task AllTypesDisabled_ShouldNotScrapeAnyType(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled
        {
            NameEnabled = false,
            DescriptionEnabled = false,
            DeveloperEnabled = false,
            TagsEnabled = false,
            RatingEnabled = false,
            ExpectedPlayTimeEnabled = false,
            ReleaseDateEnabled = false,
            CnNameEnabled = false,
            ImageUrlEnabled = false,
            CharactersEnabled = false
        };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // All fields should have default/empty values
        Assert.That(result!.Name.Value, Is.EqualTo(string.Empty));
        Assert.That(string.IsNullOrEmpty(result.Description.Value), Is.True);
        Assert.That(result.Developer.Value, Is.EqualTo(Galgame.DefaultString));
        Assert.That(result.Tags.Value, Is.Null.Or.Empty);
        Assert.That(result.Rating.Value, Is.EqualTo(0));
        Assert.That(result.ExpectedPlayTime.Value, Is.EqualTo(Galgame.DefaultString));
        Assert.That(result.ReleaseDate.Value, Is.EqualTo(DateTime.MinValue));
        Assert.That(string.IsNullOrEmpty(result.CnName), Is.True);
        Assert.That(string.IsNullOrEmpty(result.ImageUrl), Is.True);
        Assert.That(result.Characters, Is.Null.Or.Empty);
    }

    /// <summary>
    /// Test developer extraction from tags when both DeveloperEnabled and TagsEnabled are true
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task DeveloperFromTags_WhenBothEnabled_ShouldExtractFromTags(string gameName)
    {
        // Arrange - Use a configuration where developer info might not be available directly
        var enabled = new MixedPhraserEnabled
        {
            DeveloperEnabled = true,
            TagsEnabled = true
        };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Developer should be scraped (either directly or from tags)
        Assert.That(result!.Developer.Value, Is.Not.EqualTo(Galgame.DefaultString));
        // Tags should be available for extraction
        Assert.That(result.Tags.Value, Is.Not.Null);
    }

    /// <summary>
    /// Test developer extraction from tags when TagsEnabled is false
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task DeveloperFromTags_WhenTagsDisabled_ShouldNotExtractFromTags(string gameName)
    {
        // Arrange
        var enabled = new MixedPhraserEnabled
        {
            DeveloperEnabled = true,
            TagsEnabled = false
        };
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Tags should not be scraped
        Assert.That(result!.Tags.Value, Is.Null.Or.Empty);
        // Developer should still be scraped from direct sources (not from tags)
        // The actual value will depend on whether direct developer info is available
    }

    /// <summary>
    /// Test backward compatibility - all types enabled by default
    /// </summary>
    [Test]
    [TestCase("近月少女的礼仪")]
    public async Task DefaultConfiguration_AllTypesEnabled_ShouldScrapeAllTypes(string gameName)
    {
        // Arrange - Use default MixedPhraserEnabled (all true by default)
        var enabled = new MixedPhraserEnabled();
        _mixedPhraser = CreateMixedPhraser(enabled);
        var game = new Galgame(gameName);

        // Act
        var result = await _mixedPhraser.GetGalgameInfo(game);

        // Assert
        Assert.That(result, Is.Not.Null);
        // All fields should be scraped with default configuration
        Assert.That(result!.Name.Value, Is.Not.Empty);
        Assert.That(result.Description.Value, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Developer.Value, Is.Not.EqualTo(Galgame.DefaultString));
        Assert.That(result.Tags.Value, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Rating.Value, Is.GreaterThan(0));
        Assert.That(result.ImageUrl, Is.Not.Null.And.Not.Empty);
    }

    /// <summary>
    /// Helper method to create a MixedPhraser with custom enabled configuration
    /// </summary>
    private MixedPhraser CreateMixedPhraser(MixedPhraserEnabled enabled)
    {
        return new MixedPhraser(_bgmPhraser, _vndbPhraser, _ymgalPhraser, _steamParser, _hikarinagiPhraser, new MixedPhraserData
        {
            Order = new MixedPhraserOrder().SetToDefault(),
            Enabled = enabled
        });
    }
}
