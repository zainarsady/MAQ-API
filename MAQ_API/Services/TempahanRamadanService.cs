using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Models;
using Microsoft.EntityFrameworkCore;

namespace MAQ_API.Services;

public class TempahanRamadanService(AppDbContext db) : ITempahanRamadanService
{
    public const int MaxBookingsPerDay = 20;

    public Task<List<TempahanRamadan>> GetAllAsync() =>
        db.TempahanRamadans.AsNoTracking().OrderBy(t => t.BookedDate).ThenBy(t => t.TempahanRamadanId).ToListAsync();

    public Task<TempahanRamadan?> GetByIdAsync(int id) =>
        db.TempahanRamadans.AsNoTracking().FirstOrDefaultAsync(t => t.TempahanRamadanId == id);

    public async Task<TempahanRamadan> CreateAsync(TempahanRamadanRequest request)
    {
        await EnsureRulesAsync(request, 0);

        var booking = new TempahanRamadan { CreatedAt = DateTime.UtcNow };
        Apply(booking, request);
        db.TempahanRamadans.Add(booking);
        await SaveAsync();
        return booking;
    }

    public async Task<TempahanRamadan?> UpdateAsync(int id, TempahanRamadanRequest request)
    {
        var booking = await db.TempahanRamadans.FindAsync(id);
        if (booking is null) return null;

        await EnsureRulesAsync(request, id);

        Apply(booking, request);
        await SaveAsync();
        return booking;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var booking = await db.TempahanRamadans.FindAsync(id);
        if (booking is null) return false;

        db.TempahanRamadans.Remove(booking);
        await db.SaveChangesAsync();
        return true;
    }

    public const int MaxAvailabilityRangeDays = 366;

    public async Task<List<DateAvailabilityResponse>> GetAvailabilityAsync(DateOnly? from, DateOnly? to)
    {
        var query = db.TempahanRamadans.AsNoTracking()
            .Where(t => db.Payments.Any(p =>
                p.TempahanRamadanId == t.TempahanRamadanId && p.Status == PaymentStatus.Paid));
        if (from is not null) query = query.Where(t => t.BookedDate >= from);
        if (to is not null) query = query.Where(t => t.BookedDate <= to);

        var counts = (await query
                .GroupBy(t => t.BookedDate)
                .Select(g => new { Date = g.Key, Booked = g.Count() })
                .ToListAsync())
            .ToDictionary(x => x.Date, x => x.Booked);

        IEnumerable<DateOnly> dates = counts.Keys.Order();
        if (from is not null && to is not null)
            dates = Enumerable.Range(0, to.Value.DayNumber - from.Value.DayNumber + 1)
                .Select(i => from.Value.AddDays(i));

        return dates.Select(d =>
        {
            var booked = counts.GetValueOrDefault(d);
            return new DateAvailabilityResponse(d, MaxBookingsPerDay, booked, Math.Max(0, MaxBookingsPerDay - booked));
        }).ToList();
    }

    private async Task EnsureRulesAsync(TempahanRamadanRequest request, int excludeId)
    {
        var date = request.BookedDate!.Value;

        if (await db.TempahanRamadans.CountAsync(t =>
                t.BookedDate == date && t.TempahanRamadanId != excludeId) >= MaxBookingsPerDay)
            throw new DailyLimitReachedException(MaxBookingsPerDay);
    }

    private async Task SaveAsync()
    {
        await db.SaveChangesAsync();
    }

    private static void Apply(TempahanRamadan booking, TempahanRamadanRequest request)
    {
        booking.BookedDate = request.BookedDate!.Value;
        booking.CustomerName = request.CustomerName;
        booking.ContactNumber = request.ContactNumber;
        booking.ICNumber = request.ICNumber;
        booking.Age = request.Age;
        booking.ArwahName = request.ArwahName ?? string.Empty;
        booking.Address = request.Address;
        booking.Amount = request.Amount;
    }
}
