using System.Collections.ObjectModel;
using AutoMapper;
using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public class PvnServiceTest : ServiceTestBase
{
    [Test]
    public async Task GalgameMutated_PvnAdded_DoesNotTriggerEchoUpload()
    {
        await Settings.SaveSettingAsync(KeyValues.SyncGames, true);
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls:PotatoVNOfficialServer"] = "https://example.com",
        }).Build();
        Galgame game = new("同步游戏");
        GalgameCollectionService.SetupGet(x => x.Galgames).Returns(new ObservableCollection<Galgame> { game });
        _ = new PvnService(Settings, config, new Mock<IBgmOAuthService>().Object, BgTaskService.Object,
            GalgameCollectionService.Object, new Mock<IStaffService>().Object, InfoService.Object,
            new Mock<IMapper>().Object);

        GalgameCollectionService.Raise(x => x.GalgameMutated += null,
            new GalgameMutationEventArgs(game, GalgameChangeKind.Added, GalgameChangeOrigin.PvnSync));

        Assert.That(game.PvnUpdate, Is.False);

        GalgameCollectionService.Raise(x => x.GalgameMutated += null,
            new GalgameMutationEventArgs(game, GalgameChangeKind.Added, GalgameChangeOrigin.LocalOperation));

        Assert.Multiple(() =>
        {
            Assert.That(game.PvnUpdate, Is.True);
            Assert.That(game.PvnUploadProperties, Is.EqualTo(PvnUploadProperties.All));
        });
    }
}
