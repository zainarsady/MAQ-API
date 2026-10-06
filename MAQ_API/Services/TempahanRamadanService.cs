using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Models;
using Microsoft.EntityFrameworkCore;

namespace MAQ_API.Services;

public class TempahanRamadanService(AppDbContext db) : ITempahanRamadanService
{
    private const int MaxBookingsPerDay = 20;

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

    private async Task EnsureRulesAsync(TempahanRamadanRequest request, int excludeId)
    {
        var date = request.BookedDate!.Value;

        if (await db.TempahanRamadans.AnyAsync(t =>
                t.ICNumber == request.ICNumber && t.BookedDate == date && t.TempahanRamadanId != excludeId))
            throw new DuplicateBookingException();

        if (await db.TempahanRamadans.CountAsync(t =>
                t.BookedDate == date && t.TempahanRamadanId != excludeId) >= MaxBookingsPerDay)
            throw new DailyLimitReachedException(MaxBookingsPerDay);
    }

    private async Task SaveAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique (ICNumber, BookedDate) index hit between the check and the insert.
            throw new DuplicateBookingException();
        }
    }

    private static void Apply(TempahanRamadan booking, TempahanRamadanRequest request)
    {
        booking.BookedDate = request.BookedDate!.Value;
        booking.CustomerName = request.CustomerName;
        booking.ContactNumber = request.ContactNumber;
        booking.ICNumber = request.ICNumber;
        booking.Age = request.Age;
        booking.Address = request.Address;
        booking.Amount = request.Amount;
    }
}
