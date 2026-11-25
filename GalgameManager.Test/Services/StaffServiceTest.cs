using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Services;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public class StaffServiceTest : ServiceTestBase
{
    [Test]
    public void GalgameMutated_RefreshesStaffOnlyForMetadataAdditionOrGameInfoParse()
    {
        Mock<IGalInfoPhraser> phraser = new();
        Mock<IGalStaffParser> staffParser = phraser.As<IGalStaffParser>();
        staffParser.Setup(x => x.GetStaffsAsync(It.IsAny<Galgame>())).ReturnsAsync([]);
        GalgameCollectionService.SetupGet(x => x.PhraserList).Returns(new Dictionary<int, IGalInfoPhraser>
        {
            [(int)RssType.Mixed] = phraser.Object,
        });
        _ = new StaffService(GalgameCollectionService.Object, BgTaskService.Object, Settings, InfoService.Object);
        Galgame game = new("测试游戏");

        GalgameCollectionService.Raise(x => x.GalgameMutated += null,
            new GalgameMutationEventArgs(game, GalgameChangeKind.Images, GalgameChangeOrigin.Parser,
                GameParseType.HeaderImage));

        staffParser.Verify(x => x.GetStaffsAsync(It.IsAny<Galgame>()), Times.Never);

        GalgameCollectionService.Raise(x => x.GalgameMutated += null,
            new GalgameMutationEventArgs(game, GalgameChangeKind.Added | GalgameChangeKind.Metadata,
                GalgameChangeOrigin.LocalOperation));

        staffParser.Verify(x => x.GetStaffsAsync(game), Times.Once);

        GalgameCollectionService.Raise(x => x.GalgameMutated += null,
            new GalgameMutationEventArgs(game, GalgameChangeKind.Metadata, GalgameChangeOrigin.Parser,
                GameParseType.GameInfo));

        staffParser.Verify(x => x.GetStaffsAsync(game), Times.Exactly(2));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GalgameMutated_FetchesStaffDetailsOnlyWhenDownloadStaffsEnabled(bool downloadStaffs)
    {
        Mock<IGalInfoPhraser> phraser = new();
        Mock<IGalStaffParser> staffParser = phraser.As<IGalStaffParser>();
        staffParser.Setup(x => x.GetStaffsAsync(It.IsAny<Galgame>()))
            .ReturnsAsync([new StaffRelation { JapaneseName = "テストスタッフ" }]);
        GalgameCollectionService.SetupGet(x => x.PhraserList).Returns(new Dictionary<int, IGalInfoPhraser>
        {
            [(int)RssType.Mixed] = phraser.Object,
        });
        await Settings.SaveSettingAsync(KeyValues.DownloadStaffs, downloadStaffs);
        StaffService service = new(GalgameCollectionService.Object, BgTaskService.Object, Settings,
            InfoService.Object);
        await service.InitAsync();
        var staffChanged = false;
        service.OnGameStaffChanged += _ => staffChanged = true;
        Galgame game = new("测试游戏");

        GalgameCollectionService.Raise(x => x.GalgameMutated += null,
            new GalgameMutationEventArgs(game, GalgameChangeKind.Metadata, GalgameChangeOrigin.Parser,
                GameParseType.GameInfo));

        await WaitUntilAsync(() => staffChanged, "staff list was not refreshed");
        Assert.That(service.GetStaffs(game), Has.Count.EqualTo(1));
        BgTaskService.Verify(x => x.AddBgTask(It.IsAny<GetStaffFromRssTask>()),
            downloadStaffs ? Times.Once() : Times.Never());
    }
}
