using GalgameManager.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalgameManager.Server.Data;

public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
{
    public DbSet<User> User { get; init; } = null!;
    public DbSet<Galgame> Galgame { get; init; } = null!;
    public DbSet<GalgameDeleted> GalgameDeleted { get; init; } = null!;
    public DbSet<PlayLog> GalPlayLog { get; init; } = null!;
    public DbSet<Category> Category { get; init; } = null!;
    public DbSet<OssRecord> OssRecords { get; set; } = null!;
}