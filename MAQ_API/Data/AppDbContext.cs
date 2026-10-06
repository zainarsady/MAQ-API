using MAQ_API.Models;
using Microsoft.EntityFrameworkCore;

namespace MAQ_API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TempahanRamadan> TempahanRamadans => Set<TempahanRamadan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
