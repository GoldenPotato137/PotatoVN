using CommunityToolkit.Mvvm.Messaging;
using GalgameManager.Contracts.Services;
using GalgameManager.Models;
using GalgameManager.Models.BgTasks;
using GalgameManager.Models.Sources;
using GalgameManager.Services;
using Moq;

namespace GalgameManager.Test.Services;

[TestFixture]
public class GameLaunchServiceTest : ServiceTestBase
{
    private GameLaunchService CreateService()
    {
        IJumpListService jumpListService = Mock.Of<IJumpListService>();
        IGalgameSourceCollectionService sourceService = Mock.Of<IGalgameSourceCollectionService>();
        IMessenger messenger = new WeakReferenceMessenger();
        GalgameCollectionService gameService = new(Settings, jumpListService, sourceService,
            InfoService.Object, BgTaskService.Object, messenger);
        return new GameLaunchService(gameService, sourceService, Settings, jumpListService,
            BgTaskService.Object, InfoService.Object, messenger);
    }

    private GalgameAndPath CreateInstallation(Galgame game) =>
        new(game, Path.Combine(TestDir, Guid.NewGuid().ToString("N")), new GalgameFolderSource());

    [Test]
    public async Task AddPlayTimeTask_ActiveLogicalGame_DoesNotAddDuplicate()
    {
        Galgame game = new();
        RecordPlayTimeTask active = new(Settings, GalgameCollectionService.Object) { Galgame = game };
        RecordPlayTimeTask duplicate = new(Settings, GalgameCollectionService.Object)
        {
            Galgame = new Galgame { Uuid = game.Uuid },
            InstallationId = Guid.NewGuid(),
        };
        BgTaskService.Setup(x => x.GetBgTask<RecordPlayTimeTask>(game.Uuid.ToString("D"))).Returns(active);

        await CreateService().AddPlayTimeTaskAsync(duplicate);

        BgTaskService.Verify(x => x.AddBgTask(It.IsAny<BgTaskBase>()), Times.Never);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AddPlayTimeTask_NoActiveTask_ReturnsBackgroundTaskCompletion(bool missingGame)
    {
        RecordPlayTimeTask task = new(Settings, GalgameCollectionService.Object)
        {
            Galgame = missingGame ? null : new Galgame(),
        };
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        BgTaskService.Setup(x => x.AddBgTask(task)).Returns(completion.Task);

        Task result = CreateService().AddPlayTimeTaskAsync(task);

        Assert.That(result, Is.SameAs(completion.Task));
        BgTaskService.Verify(x => x.AddBgTask(task), Times.Once);
        BgTaskService.Verify(x => x.GetBgTask<RecordPlayTimeTask>(It.IsAny<string>()),
            missingGame ? Times.Never() : Times.Once());
        completion.SetResult();
    }

    [Test]
    public async Task LaunchAsync_ActivePlayTimeTask_RejectsBeforeStartingProcess()
    {
        Galgame game = new();
        RecordPlayTimeTask active = new(Settings, GalgameCollectionService.Object) { Galgame = game };
        BgTaskService.Setup(x => x.GetBgTask<RecordPlayTimeTask>(game.Uuid.ToString("D"))).Returns(active);
        GameLaunchService service = CreateService();

        await service.LaunchAsync(game, CreateInstallation(game));

        InfoService.Verify(x => x.Log(It.IsAny<Microsoft.UI.Xaml.Controls.InfoBarSeverity>(),
            It.Is<string>(message => message.Contains("play-time task already active"))), Times.Once);
        BgTaskService.Verify(x => x.AddBgTask(It.IsAny<BgTaskBase>()), Times.Never);
    }

    [Test]
    public async Task LaunchAsync_ConcurrentInstallationsOfSameGame_OnlyOneEntersLaunch()
    {
        Galgame game = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        BgTaskService.Setup(x => x.GetBgTask<RecordPlayTimeTask>(game.Uuid.ToString("D")))
            .Returns(() =>
            {
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return null;
            });
        GameLaunchService service = CreateService();
        GalgameAndPath first = CreateInstallation(game);
        GalgameAndPath second = CreateInstallation(game);
        Task launch = Task.Run(() => service.LaunchAsync(game, first));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.LaunchAsync(game, second);
            BgTaskService.Verify(x => x.GetBgTask<RecordPlayTimeTask>(game.Uuid.ToString("D")), Times.Once);
            InfoService.Verify(x => x.Log(It.IsAny<Microsoft.UI.Xaml.Controls.InfoBarSeverity>(),
                It.Is<string>(message => message.Contains("launch request already in progress"))), Times.Once);
        }
        finally
        {
            release.TrySetResult();
            await launch;
        }
    }

    [Test]
    public async Task LaunchAsync_DifferentGames_CanEnterIndependently()
    {
        Galgame firstGame = new();
        Galgame secondGame = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        BgTaskService.Setup(x => x.GetBgTask<RecordPlayTimeTask>(firstGame.Uuid.ToString("D")))
            .Returns(() =>
            {
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
                return null;
            });
        GameLaunchService service = CreateService();
        GalgameAndPath first = CreateInstallation(firstGame);
        GalgameAndPath second = CreateInstallation(secondGame);
        Task launch = Task.Run(() => service.LaunchAsync(firstGame, first));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await service.LaunchAsync(secondGame, second);
            BgTaskService.Verify(x => x.GetBgTask<RecordPlayTimeTask>(secondGame.Uuid.ToString("D")), Times.Once);
        }
        finally
        {
            release.TrySetResult();
            await launch;
        }
    }

    [Test]
    public async Task LaunchAsync_UnavailablePath_ReleasesLaunchState()
    {
        GameLaunchService service = CreateService();
        Galgame game = new();
        GalgameAndPath installation = CreateInstallation(game);

        await service.LaunchAsync(game, installation);
        await service.LaunchAsync(game, installation);

        BgTaskService.Verify(x => x.GetBgTask<RecordPlayTimeTask>(game.Uuid.ToString("D")), Times.Exactly(2));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LaunchAsync_ExceptionOrCancellation_ReleasesLaunchState(bool cancellation)
    {
        GameLaunchService service = CreateService();
        Galgame game = new();
        GalgameAndPath installation = CreateInstallation(game);
        Exception exception = cancellation ? new TaskCanceledException() : new InvalidOperationException();
        BgTaskService.Setup(x => x.GetBgTask<RecordPlayTimeTask>(game.Uuid.ToString("D"))).Throws(exception);
        Assert.That(async () => await service.LaunchAsync(game, installation), Throws.TypeOf(exception.GetType()));

        BgTaskService.Setup(x => x.GetBgTask<RecordPlayTimeTask>(game.Uuid.ToString("D"))).Returns((RecordPlayTimeTask?)null);
        await service.LaunchAsync(game, installation);
        BgTaskService.Verify(x => x.GetBgTask<RecordPlayTimeTask>(game.Uuid.ToString("D")), Times.Exactly(2));
    }
}
