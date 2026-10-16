using Microsoft.EntityFrameworkCore;
using Server.Domain;

namespace Server.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> TaskItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<TaskItem>();

        task.HasKey(x => x.Id);

        task.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        task.Property(x => x.Priority)
            .HasConversion<string>();

        task.Property(x => x.Version)
            .IsRowVersion();
    }
}
