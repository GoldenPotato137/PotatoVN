using GalgameManager.Server.Data;
using GalgameManager.Server.Enums;
using GalgameManager.Server.Exceptions;
using GalgameManager.Server.Models;
using GalgameManager.Server.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GalgameManager.Server.Test.Repositories;

/// <summary>
/// UserRepository 唯一索引冲突处理测试<br/>
/// 内存数据库不会检查唯一索引，这里通过让SaveChanges抛出PostgreSQL错误来模拟
/// </summary>
[TestFixture]
public class UserRepositoryTests
{
    private FailingDataContext _context = null!;
    private UserRepository _repository = null!;

    [SetUp]
    public void Setup()
    {
        DbContextOptions<DataContext> options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new FailingDataContext(options);
        _repository = new UserRepository(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private static User NewUser(string userName, int bangumiId = 0) => new()
    {
        UserName = userName,
        DisplayUserName = userName,
        Type = UserType.User,
        TotalSpace = 0,
        BangumiId = bangumiId,
    };

    [Test]
    public void AddUserAsync_UniqueViolation_ThrowsDuplicateUserExceptionAndDetaches()
    {
        // Arrange
        User user = NewUser("user");
        _context.SaveError = PostgresErrorCodes.UniqueViolation;

        // Act & Assert
        Assert.ThrowsAsync<DuplicateUserException>(() => _repository.AddUserAsync(user));
        Assert.That(_context.Entry(user).State, Is.EqualTo(EntityState.Detached));
    }

    [Test]
    public async Task UpdateUserAsync_UniqueViolation_ThrowsDuplicateUserExceptionAndReverts()
    {
        // Arrange
        User user = await _repository.AddUserAsync(NewUser("user"));
        user.BangumiId = 114514;
        _context.SaveError = PostgresErrorCodes.UniqueViolation;

        // Act & Assert
        Assert.ThrowsAsync<DuplicateUserException>(() => _repository.UpdateUserAsync(user));
        Assert.Multiple(() =>
        {
            Assert.That(user.BangumiId, Is.EqualTo(0));
            Assert.That(_context.Entry(user).State, Is.EqualTo(EntityState.Unchanged));
        });
    }

    [Test]
    public void AddUserAsync_OtherDbError_IsNotTranslated()
    {
        // Arrange
        _context.SaveError = PostgresErrorCodes.ForeignKeyViolation;

        // Act & Assert
        Assert.ThrowsAsync<DbUpdateException>(() => _repository.AddUserAsync(NewUser("user")));
    }

    private class FailingDataContext(DbContextOptions<DataContext> options) : DataContext(options)
    {
        /// <summary>不为null时，SaveChanges抛出带有该SqlState的PostgreSQL错误</summary>
        public string? SaveError { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (SaveError is null)
                return base.SaveChangesAsync(cancellationToken);
            throw new DbUpdateException("save failed",
                new PostgresException("duplicate key value", "ERROR", "ERROR", SaveError));
        }
    }
}
