using GalgameManager.Server.Contracts;
using GalgameManager.Server.Enums;
using GalgameManager.Server.Exceptions;
using GalgameManager.Server.Models;
using GalgameManager.Server.Repositories;
using GalgameManager.Server.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GalgameManager.Server.Test.Services;

/// <summary>
/// UserService 登录注册、密码设置与bgm账户绑定测试
/// </summary>
[TestFixture]
public class UserServiceTests : TestBase
{
    private IUserRepository _userRepository = null!;
    private Mock<IBangumiService> _bgmServiceMock = null!;
    private Mock<IOssService> _ossServiceMock = null!;

    private const string Password = "password";
    private const string BgmToken = "bgm_token";
    private const int BgmId = 114514;

    public override void Setup()
    {
        base.Setup();
        _userRepository = new UserRepository(Context);
        _bgmServiceMock = new Mock<IBangumiService>();
        _bgmServiceMock.Setup(s => s.GetAccount(BgmToken)).ReturnsAsync(new BangumiAccount
        {
            Id = BgmId,
            UserName = "bgm_user",
            UserDisplayName = "Bgm User",
        });
        _ossServiceMock = new Mock<IOssService>();
    }

    private UserService CreateService(bool defaultLoginEnable = true, IUserRepository? userRepository = null)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppSettings:DefaultLoginEnable"] = defaultLoginEnable.ToString(),
            })
            .Build();
        return new UserService(config, userRepository ?? _userRepository, _bgmServiceMock.Object,
            _ossServiceMock.Object);
    }

    private async Task<User> AddUserAsync(string userName, string? password = null, int bangumiId = 0)
    {
        return await _userRepository.AddUserAsync(new User
        {
            UserName = userName,
            DisplayUserName = userName,
            PasswordHash = password is null ? string.Empty : BCrypt.Net.BCrypt.HashPassword(password),
            Type = UserType.User,
            TotalSpace = 0,
            BangumiId = bangumiId,
        });
    }

    #region 登录与设置密码

    [Test]
    public async Task LoginAsync_CorrectPassword_ReturnsUser()
    {
        // Arrange
        User user = await AddUserAsync("user", Password);
        UserService service = CreateService();

        // Act
        User result = await service.LoginAsync("user", Password);

        // Assert
        Assert.That(result.Id, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task LoginAsync_UserWithoutPassword_ThrowsArgumentException()
    {
        // Arrange
        await AddUserAsync("_bgm_bgm_user", bangumiId: BgmId);
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => service.LoginAsync("_bgm_bgm_user", "anything"));
    }

    [Test]
    public async Task ModifyAsync_UserWithoutPassword_SetsPasswordWithoutOldPassword()
    {
        // Arrange
        User user = await AddUserAsync("_bgm_bgm_user", bangumiId: BgmId);
        UserService service = CreateService();

        // Act
        await service.ModifyAsync(user.Id, new UserModifyDto { NewPassword = Password });
        User loginUser = await service.LoginAsync("_bgm_bgm_user", Password);

        // Assert
        Assert.That(loginUser.Id, Is.EqualTo(user.Id));
    }

    [Test]
    public async Task ModifyAsync_UserWithPassword_WithoutOldPassword_ThrowsArgumentException()
    {
        // Arrange
        User user = await AddUserAsync("user", Password);
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() =>
            service.ModifyAsync(user.Id, new UserModifyDto { NewPassword = "new" }));
        Assert.That(BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash), Is.True);
    }

    [Test]
    public async Task ModifyAsync_UserWithPassword_WrongOldPassword_ThrowsAndKeepsUserUnchanged()
    {
        // Arrange
        User user = await AddUserAsync("user", Password);
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => service.ModifyAsync(user.Id, new UserModifyDto
        {
            UserDisplayName = "new name",
            OldPassword = "wrong",
            NewPassword = "new",
        }));
        Assert.Multiple(() =>
        {
            Assert.That(user.DisplayUserName, Is.EqualTo("user"));
            Assert.That(BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash), Is.True);
        });
    }

    [Test]
    public async Task ModifyAsync_UserWithPassword_CorrectOldPassword_ChangesPassword()
    {
        // Arrange
        User user = await AddUserAsync("user", Password);
        UserService service = CreateService();

        // Act
        await service.ModifyAsync(user.Id, new UserModifyDto
        {
            OldPassword = Password,
            NewPassword = "new",
        });

        // Assert
        Assert.That(BCrypt.Net.BCrypt.Verify("new", user.PasswordHash), Is.True);
    }

    [Test]
    public async Task ModifyAsync_EmptyNewPassword_ThrowsArgumentException()
    {
        // Arrange
        User user = await AddUserAsync("_bgm_bgm_user", bangumiId: BgmId);
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() =>
            service.ModifyAsync(user.Id, new UserModifyDto { NewPassword = string.Empty }));
        Assert.That(user.PasswordHash, Is.Empty);
    }

    [Test]
    public void ModifyAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<KeyNotFoundException>(() => service.ModifyAsync(1, new UserModifyDto()));
    }

    #endregion

    #region 绑定bgm账户

    [Test]
    public async Task BindBgmAsync_UnboundUser_BindsAndCanLoginViaBgm()
    {
        // Arrange
        User user = await AddUserAsync("user", Password);
        UserService service = CreateService();

        // Act
        User result = await service.BindBgmAsync(user.Id, BgmToken);
        User loginUser = await service.LoginViaBgmAsync(BgmToken);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.BangumiId, Is.EqualTo(BgmId));
            Assert.That(loginUser.Id, Is.EqualTo(user.Id));
            Assert.That(Context.User.Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task BindBgmAsync_SameAccountAlreadyBound_ReturnsUser()
    {
        // Arrange
        User user = await AddUserAsync("user", Password, BgmId);
        UserService service = CreateService();

        // Act
        User result = await service.BindBgmAsync(user.Id, BgmToken);

        // Assert
        Assert.That(result.BangumiId, Is.EqualTo(BgmId));
    }

    [Test]
    public async Task BindBgmAsync_AccountBoundToOtherUser_ThrowsDuplicateUserException()
    {
        // Arrange
        await AddUserAsync("_bgm_bgm_user", bangumiId: BgmId);
        User user = await AddUserAsync("user", Password);
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<DuplicateUserException>(() => service.BindBgmAsync(user.Id, BgmToken));
        Assert.That(user.BangumiId, Is.Zero);
    }

    [Test]
    public async Task BindBgmAsync_BoundToDifferentAccount_ThrowsArgumentException()
    {
        // Arrange
        User user = await AddUserAsync("user", Password, 1);
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => service.BindBgmAsync(user.Id, BgmToken));
        Assert.That(user.BangumiId, Is.EqualTo(1));
    }

    [Test]
    public async Task BindBgmAsync_InvalidToken_ThrowsInvalidAuthorizationCodeException()
    {
        // Arrange
        _bgmServiceMock.Setup(s => s.GetAccount("invalid"))
            .ThrowsAsync(new InvalidAuthorizationCodeException("invalid token"));
        User user = await AddUserAsync("user", Password);
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<InvalidAuthorizationCodeException>(() => service.BindBgmAsync(user.Id, "invalid"));
        Assert.That(user.BangumiId, Is.Zero);
    }

    [Test]
    public async Task BindBgmAsync_BgmUnavailable_ThrowsBangumiUnavailableException()
    {
        // Arrange
        _bgmServiceMock.Setup(s => s.GetAccount(BgmToken)).ThrowsAsync(new HttpRequestException("timeout"));
        User user = await AddUserAsync("user", Password);
        UserService service = CreateService();

        // Act & Assert
        BangumiUnavailableException? e =
            Assert.ThrowsAsync<BangumiUnavailableException>(() => service.BindBgmAsync(user.Id, BgmToken));
        Assert.That(e?.InnerException, Is.InstanceOf<HttpRequestException>());
    }

    [Test]
    public void BindBgmAsync_ConcurrentBind_ThrowsDuplicateUserException()
    {
        // Arrange: 检查时bgm账户未被绑定，保存时已被其他账户并发绑定
        User user = new() { Id = 42, UserName = "user", TotalSpace = 0 };
        Mock<IUserRepository> repositoryMock = new();
        repositoryMock.Setup(r => r.GetUserAsync(user.Id)).ReturnsAsync(user);
        repositoryMock.Setup(r => r.UpdateUserAsync(user)).ThrowsAsync(new DuplicateUserException("duplicate"));
        UserService service = CreateService(userRepository: repositoryMock.Object);

        // Act & Assert
        Assert.ThrowsAsync<DuplicateUserException>(() => service.BindBgmAsync(user.Id, BgmToken));
    }

    #endregion

    #region 解绑bgm账户

    [Test]
    public async Task UnbindBgmAsync_UserWithPassword_Unbinds()
    {
        // Arrange
        User user = await AddUserAsync("user", Password, BgmId);
        UserService service = CreateService();

        // Act
        User result = await service.UnbindBgmAsync(user.Id);

        // Assert
        Assert.That(result.BangumiId, Is.Zero);
    }

    [Test]
    public async Task UnbindBgmAsync_UserWithoutPassword_ThrowsArgumentException()
    {
        // Arrange
        User user = await AddUserAsync("_bgm_bgm_user", bangumiId: BgmId);
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => service.UnbindBgmAsync(user.Id));
        Assert.That(user.BangumiId, Is.EqualTo(BgmId));
    }

    [Test]
    public async Task UnbindBgmAsync_DefaultLoginDisabled_ThrowsArgumentException()
    {
        // Arrange
        User user = await AddUserAsync("user", Password, BgmId);
        UserService service = CreateService(defaultLoginEnable: false);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => service.UnbindBgmAsync(user.Id));
        Assert.That(user.BangumiId, Is.EqualTo(BgmId));
    }

    [Test]
    public async Task UnbindBgmAsync_NotBound_ThrowsArgumentException()
    {
        // Arrange
        User user = await AddUserAsync("user", Password);
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => service.UnbindBgmAsync(user.Id));
    }

    #endregion

    #region 注册与bgm自动注册

    [Test]
    public async Task RegisterAsync_NewUser_CanLogin()
    {
        // Arrange
        UserService service = CreateService();

        // Act
        User user = await service.RegisterAsync("user", Password);
        User loginUser = await service.LoginAsync("user", Password);

        // Assert
        Assert.That(loginUser.Id, Is.EqualTo(user.Id));
    }

    [TestCase("_bgm_user")]
    [TestCase("_BGM_user")]
    public void RegisterAsync_BgmPrefix_ThrowsArgumentException(string userName)
    {
        // Arrange
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(userName, Password));
        Assert.That(Context.User.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task RegisterAsync_UserNameTaken_ThrowsArgumentException()
    {
        // Arrange
        await AddUserAsync("user", Password);
        UserService service = CreateService();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync("user", Password));
    }

    [Test]
    public void RegisterAsync_ConcurrentDuplicate_ThrowsArgumentException()
    {
        // Arrange: 查询时用户名未被占用，插入时已被并发注册
        Mock<IUserRepository> repositoryMock = new();
        repositoryMock.Setup(r => r.AddUserAsync(It.IsAny<User>()))
            .ThrowsAsync(new DuplicateUserException("duplicate"));
        UserService service = CreateService(userRepository: repositoryMock.Object);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync("user", Password));
    }

    [Test]
    public async Task LoginViaBgmAsync_NewAccount_RegistersWithBgmPrefix()
    {
        // Arrange
        UserService service = CreateService();

        // Act
        User user = await service.LoginViaBgmAsync(BgmToken);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(user.UserName, Is.EqualTo("_bgm_bgm_user"));
            Assert.That(user.BangumiId, Is.EqualTo(BgmId));
            Assert.That(user.PasswordHash, Is.Empty);
        });
    }

    [Test]
    public async Task LoginViaBgmAsync_UserNameTaken_RegistersWithSuffix()
    {
        // Arrange: 原账户解绑bgm后仍保留_bgm_用户名
        await AddUserAsync("_bgm_bgm_user", Password);
        await AddUserAsync("_bgm_bgm_user_2", Password);
        UserService service = CreateService();

        // Act
        User user = await service.LoginViaBgmAsync(BgmToken);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(user.UserName, Is.EqualTo("_bgm_bgm_user_3"));
            Assert.That(user.BangumiId, Is.EqualTo(BgmId));
            Assert.That(Context.User.Count(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task LoginViaBgmAsync_ConcurrentRegistration_ReturnsExistingUser()
    {
        // Arrange: 查询时还没有账户，插入时另一个请求已经注册了同一bgm账户
        User existing = new() { Id = 42, UserName = "_bgm_bgm_user", BangumiId = BgmId, TotalSpace = 0 };
        Mock<IUserRepository> repositoryMock = new();
        repositoryMock.SetupSequence(r => r.GetUserByBangumiIdAsync(BgmId))
            .ReturnsAsync((User?)null)
            .ReturnsAsync(existing);
        repositoryMock.Setup(r => r.AddUserAsync(It.IsAny<User>()))
            .ThrowsAsync(new DuplicateUserException("duplicate"));
        UserService service = CreateService(userRepository: repositoryMock.Object);

        // Act
        User user = await service.LoginViaBgmAsync(BgmToken);

        // Assert
        Assert.That(user.Id, Is.EqualTo(existing.Id));
    }

    [Test]
    public void LoginViaBgmAsync_ConflictWithoutExistingUser_ThrowsDuplicateUserException()
    {
        // Arrange: 插入冲突但仍查不到该bgm账户对应的用户（如冲突的是用户名）
        Mock<IUserRepository> repositoryMock = new();
        repositoryMock.Setup(r => r.AddUserAsync(It.IsAny<User>()))
            .ThrowsAsync(new DuplicateUserException("duplicate"));
        UserService service = CreateService(userRepository: repositoryMock.Object);

        // Act & Assert
        Assert.ThrowsAsync<DuplicateUserException>(() => service.LoginViaBgmAsync(BgmToken));
    }

    #endregion
}
