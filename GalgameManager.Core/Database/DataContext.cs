using GalgameManager.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GalgameManager.Core.Database;

public class DataContext : DbContext
{
    private const string DbPath = "PotatoVN.sqlite3.db";

    public DbSet<Galgame> Galgames { get; set; }
    public DbSet<Category> Categories { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(new SqliteConnectionStringBuilder { DataSource = DbPath }.ToString());
    }
}