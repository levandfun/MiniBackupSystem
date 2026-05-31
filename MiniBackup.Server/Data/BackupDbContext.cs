using Microsoft.EntityFrameworkCore;

namespace MiniBackup.Server.Data;

public class BackupDbContext : DbContext
{
    public DbSet<BackupSession> Sessions { get; set; }
    public DbSet<BackupFile> Files { get; set; }

    public BackupDbContext(DbContextOptions<BackupDbContext> options) : base(options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BackupSession>()
            .Property(s => s.Status)
            .HasConversion<string>();
    }
}