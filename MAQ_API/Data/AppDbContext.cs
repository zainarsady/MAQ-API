using MAQ_API.Models;
using Microsoft.EntityFrameworkCore;

namespace MAQ_API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TempahanRamadan> TempahanRamadans => Set<TempahanRamadan>();

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<News> News => Set<News>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(e =>
        {
            e.Property(x => x.EventType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Image).HasColumnType("longblob");
            e.Property(x => x.Description).HasColumnType("text");
            e.HasMany(x => x.Registrations).WithOne().HasForeignKey(r => r.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<News>(n =>
        {
            n.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            n.Property(x => x.Image).HasColumnType("longblob");
            n.Property(x => x.BlogContent).HasColumnType("longtext");
        });

        var entity = modelBuilder.Entity<TempahanRamadan>();

        entity.HasIndex(t => new { t.ICNumber, t.BookedDate }).IsUnique();
        entity.HasIndex(t => t.BookedDate);

        entity.HasData(
            new TempahanRamadan
            {
                TempahanRamadanId = 1,
                BookedDate = new DateOnly(2027, 2, 20),
                CustomerName = "Ahmad bin Ali",
                ContactNumber = "0123456789",
                ICNumber = "900101-10-1234",
                Age = 36,
                Address = "No 1, Jalan Contoh, 50000 Kuala Lumpur",
                Amount = 350.00m,
                CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new TempahanRamadan
            {
                TempahanRamadanId = 2,
                BookedDate = new DateOnly(2027, 2, 21),
                CustomerName = "Siti binti Hassan",
                ContactNumber = "0198765432",
                ICNumber = "850505-14-5678",
                Age = 41,
                Address = "No 2, Jalan Contoh, 40000 Shah Alam",
                Amount = 500.00m,
                CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
            });
    }
}
