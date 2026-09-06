using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;
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
}
