using System.Security.Claims;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Controllers;
using GalgameManager.Server.Exceptions;
using GalgameManager.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GalgameManager.Server.Test.Controllers;

/// <summary>
/// UserController 测试：只测试功能开关与service异常到HTTP状态码的转换，业务规则见UserServiceTests
/// </summary>
[TestFixture]
public class UserControllerTests
{
    private Mock<IUserService> _userServiceMock = null!;
    private Mock<IBangumiService> _bgmServiceMock = null!;

    private const int UserId = 42;
    private const string BgmToken = "bgm_token";
    private static readonly UserLoginViaBgmDto BgmPayload = new() { BgmToken = BgmToken };

    [SetUp]
    public void Setup()
    {
        _userServiceMock = new Mock<IUserService>();
        _userServiceMock.Setup(s => s.IsDefaultLoginEnable).Returns(true);
        _userServiceMock.Setup(s => s.GetToken(It.IsAny<User>())).Returns("token");
        _bgmServiceMock = new Mock<IBangumiService>();
        _bgmServiceMock.Setup(s => s.IsLoginEnable).Returns(true);
    }

    private UserController CreateController()
    {
        UserController controller = new(_userServiceMock.Object, _bgmServiceMock.Object,
            new Mock<IOssService>().Object, NullLogger<UserController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, UserId.ToString())
                ])),
            },
        };
        return controller;
    }

    private static int? GetStatusCode<T>(ActionResult<T> result) =>
        (result.Result as IStatusCodeActionResult)?.StatusCode;

    private static TestCaseData ExceptionCase(Exception e, int statusCode) =>
        new TestCaseData(e).Returns(statusCode).SetArgDisplayNames(e.GetType().Name);

    [Test]
    public async Task LoginAsync_Success_ReturnsUserWithToken()
    {
        // Arrange
        _userServiceMock.Setup(s => s.LoginAsync("user", "password"))
            .ReturnsAsync(new User { Id = UserId, UserName = "user", TotalSpace = 0 });

        // Act
        ActionResult<UserWithTokenDto> result =
            await CreateController().LoginAsync(new UserLoginDto { UserName = "user", Password = "password" });

        // Assert
        var dto = (UserWithTokenDto?)(result.Result as OkObjectResult)?.Value;
        Assert.Multiple(() =>
        {
            Assert.That(dto?.User.Id, Is.EqualTo(UserId));
            Assert.That(dto?.Token, Is.EqualTo("token"));
        });
    }

    [Test]
    public async Task LoginAsync_DefaultLoginDisabled_Returns503()
    {
        // Arrange
        _userServiceMock.Setup(s => s.IsDefaultLoginEnable).Returns(false);

        // Act
        ActionResult<UserWithTokenDto> result =
            await CreateController().LoginAsync(new UserLoginDto { UserName = "user", Password = "password" });

        // Assert
        Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
        _userServiceMock.Verify(s => s.LoginAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task LoginAsync_InvalidCredentials_ReturnsBadRequest()
    {
        // Arrange
        _userServiceMock.Setup(s => s.LoginAsync("user", "wrong")).ThrowsAsync(new ArgumentException("wrong"));

        // Act
        ActionResult<UserWithTokenDto> result =
            await CreateController().LoginAsync(new UserLoginDto { UserName = "user", Password = "wrong" });

        // Assert
        Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    private static IEnumerable<TestCaseData> LoginViaBgmExceptionCases()
    {
        yield return ExceptionCase(new InvalidAuthorizationCodeException("invalid"), StatusCodes.Status400BadRequest);
        yield return ExceptionCase(new BangumiUnavailableException("unavailable", new HttpRequestException()),
            StatusCodes.Status502BadGateway);
        yield return ExceptionCase(new DuplicateUserException("duplicate"), StatusCodes.Status409Conflict);
    }

    [TestCaseSource(nameof(LoginViaBgmExceptionCases))]
    public async Task<int?> LoginViaBgmAsync_ServiceThrows_ReturnsMappedStatusCode(Exception exception)
    {
        // Arrange
        _userServiceMock.Setup(s => s.LoginViaBgmAsync(BgmToken)).ThrowsAsync(exception);

        // Act
        ActionResult<UserWithTokenDto> result = await CreateController().LoginViaBgmAsync(BgmPayload);

        // Assert
        return GetStatusCode(result);
    }

    [Test]
    public async Task RegisterAsync_InvalidUserName_ReturnsBadRequest()
    {
        // Arrange
        _userServiceMock.Setup(s => s.RegisterAsync("_bgm_user", "password"))
            .ThrowsAsync(new ArgumentException("reserved"));

        // Act
        ActionResult<UserWithTokenDto> result =
            await CreateController().RegisterAsync(new UserRegisterDto { UserName = "_bgm_user", Password = "password" });

        // Assert
        Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    private static IEnumerable<TestCaseData> UserExceptionCases()
    {
        yield return ExceptionCase(new KeyNotFoundException("not found"), StatusCodes.Status404NotFound);
        yield return ExceptionCase(new ArgumentException("invalid"), StatusCodes.Status400BadRequest);
    }

    [TestCaseSource(nameof(UserExceptionCases))]
    public async Task<int?> ModifyAsync_ServiceThrows_ReturnsMappedStatusCode(Exception exception)
    {
        // Arrange
        UserModifyDto payload = new() { NewPassword = "new" };
        _userServiceMock.Setup(s => s.ModifyAsync(UserId, payload)).ThrowsAsync(exception);

        // Act
        ActionResult<UserDto> result = await CreateController().ModifyAsync(payload);

        // Assert
        return GetStatusCode(result);
    }

    [Test]
    public async Task BindBgmAsync_BgmLoginDisabled_Returns503()
    {
        // Arrange
        _bgmServiceMock.Setup(s => s.IsLoginEnable).Returns(false);

        // Act
        ActionResult<UserDto> result = await CreateController().BindBgmAsync(BgmPayload);

        // Assert
        Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status503ServiceUnavailable));
        _userServiceMock.Verify(s => s.BindBgmAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    private static IEnumerable<TestCaseData> BindBgmExceptionCases()
    {
        yield return ExceptionCase(new KeyNotFoundException("not found"), StatusCodes.Status404NotFound);
        yield return ExceptionCase(new InvalidAuthorizationCodeException("invalid"), StatusCodes.Status400BadRequest);
        yield return ExceptionCase(new ArgumentException("bound"), StatusCodes.Status400BadRequest);
        yield return ExceptionCase(new BangumiUnavailableException("unavailable", new HttpRequestException()),
            StatusCodes.Status502BadGateway);
        yield return ExceptionCase(new DuplicateUserException("duplicate"), StatusCodes.Status409Conflict);
    }

    [TestCaseSource(nameof(BindBgmExceptionCases))]
    public async Task<int?> BindBgmAsync_ServiceThrows_ReturnsMappedStatusCode(Exception exception)
    {
        // Arrange
        _userServiceMock.Setup(s => s.BindBgmAsync(UserId, BgmToken)).ThrowsAsync(exception);

        // Act
        ActionResult<UserDto> result = await CreateController().BindBgmAsync(BgmPayload);

        // Assert
        return GetStatusCode(result);
    }

    [TestCaseSource(nameof(UserExceptionCases))]
    public async Task<int?> UnbindBgmAsync_ServiceThrows_ReturnsMappedStatusCode(Exception exception)
    {
        // Arrange
        _userServiceMock.Setup(s => s.UnbindBgmAsync(UserId)).ThrowsAsync(exception);

        // Act
        ActionResult<UserDto> result = await CreateController().UnbindBgmAsync();

        // Assert
        return GetStatusCode(result);
    }
}
