using GalgameManager.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace GalgameManager.Server.Data;

public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
{
    public DbSet<User> User { get; set; } = null!;
}