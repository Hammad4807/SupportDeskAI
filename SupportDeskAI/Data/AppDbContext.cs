using Microsoft.EntityFrameworkCore;
using SupportDeskAI.Models;

namespace SupportDeskAI.Data;

public class AppDbContext : DbContext
{
    // The options (which database, connection string) come from Program.cs via dependency injection.
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.Property(t => t.Title).IsRequired().HasMaxLength(200);
            entity.Property(t => t.Description).IsRequired().HasMaxLength(4000);

            // Enums are stored as integers by default (Open = 0, InProgress = 1, ...).
            entity.Property(t => t.Status).IsRequired();
            entity.Property(t => t.Priority).IsRequired();

            // SQLite does not remember that a date is UTC, so mark it as UTC when reading.
            // Without this the JSON has no "Z" and the browser treats the time as local.
            entity.Property(t => t.CreatedAt)
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.Property(t => t.UpdatedAt)
                .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.Priority);
        });
    }

    // Timestamps are set here so no caller has to remember to do it:
    // CreatedAt when a ticket is first saved, UpdatedAt only when it is modified later.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Ticket>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = null;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
