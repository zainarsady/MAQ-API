using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Models;
using Microsoft.EntityFrameworkCore;

namespace MAQ_API.Services;

public class TempahanRamadanService(AppDbContext db) : ITempahanRamadanService
{
    /// <summary>Slots (not booking rows) available per date.</summary>
    public const int MaxBookingsPerDay = 20;
    /// <summary>RM per slot; a booking's slots = Amount / SlotPrice.</summary>
    public const int SlotPrice = 300;
    /// <summary>TEMPORARY test amount for the test button. Counts as 0 slots (2 / 300 truncates to 0), so it never uses capacity. Remove before go-live.</summary>
    public const decimal TestAmount = 2m;

    public async Task<List<TempahanRamadanResponse>> GetAllAsync()
    {
        var bookings = await db.TempahanRamadans.AsNoTracking()
            .OrderBy(t => t.BookedDate).ThenBy(t => t.TempahanRamadanId)
            .ToListAsync();

        var statuses = await GetPaymentStatusesAsync(bookings.Select(t => t.TempahanRamadanId).ToList());

        return bookings
            .Select(t => TempahanRamadanResponse.From(t, statuses.GetValueOrDefault(t.TempahanRamadanId)))
            .ToList();
    }

    public async Task<TempahanRamadanResponse?> GetByIdAsync(int id)
    {
        var booking = await db.TempahanRamadans.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TempahanRamadanId == id);
        if (booking is null) return null;

        var statuses = await GetPaymentStatusesAsync([id]);
        return TempahanRamadanResponse.From(booking, statuses.GetValueOrDefault(id));
    }

    /// <summary>
    /// Anonymous endpoint: names of bookings for <paramref name="date"/> that have at least one Paid
    /// payment. One row per booking (multiple attempts collapse); only id and name are exposed.
    /// </summary>
    public async Task<List<DonorResponse>> GetPaidDonorsAsync(DateOnly date)
    {
        var donors = await db.TempahanRamadans.AsNoTracking()
            .Where(t => t.BookedDate == date && db.Payments.Any(p =>
                p.TempahanRamadanId == t.TempahanRamadanId && p.Status == PaymentStatus.Paid))
            .OrderBy(t => t.TempahanRamadanId)
            .Select(t => new { t.TempahanRamadanId, t.CustomerName })
            .ToListAsync();

        return donors
            .Select(d => new DonorResponse(d.TempahanRamadanId.ToString(), d.CustomerName))
            .ToList();
    }

    /// <summary>Payment status per booking: Paid wins, then Pending, then Failed, null if none.</summary>
    private async Task<Dictionary<int, PaymentStatus?>> GetPaymentStatusesAsync(IReadOnlyCollection<int> bookingIds)
    {
        if (bookingIds.Count == 0) return [];

        var rows = await db.Payments.AsNoTracking()
            .Where(p => p.TempahanRamadanId != null && bookingIds.Contains(p.TempahanRamadanId.Value))
            .Select(p => new { p.TempahanRamadanId, p.Status })
            .ToListAsync();

        return rows
            .GroupBy(r => r.TempahanRamadanId!.Value)
            .ToDictionary(g => g.Key, g => AggregateStatus(g.Select(r => r.Status)));
    }

    private static PaymentStatus? AggregateStatus(IEnumerable<PaymentStatus> statuses)
    {
        PaymentStatus? result = null;
        foreach (var status in statuses)
        {
            switch (status)
            {
                case PaymentStatus.Paid:
                    return PaymentStatus.Paid; // a successful attempt is final
                case PaymentStatus.Pending:
                    result = PaymentStatus.Pending; // a retry in flight beats an older failure
                    break;
                case PaymentStatus.Failed:
                    result ??= PaymentStatus.Failed;
                    break;
            }
        }
        return result;
    }

    public async Task<TempahanRamadan> CreateAsync(TempahanRamadanRequest request)
    {
        return await WithDateLockAsync(request.BookedDate!.Value, async () =>
        {
            await EnsureRulesAsync(request, 0);

            var booking = new TempahanRamadan { CreatedAt = DateTime.UtcNow };
            Apply(booking, request);
            db.TempahanRamadans.Add(booking);
            await SaveAsync();
            return booking;
        });
    }

    public async Task<TempahanRamadan?> UpdateAsync(int id, TempahanRamadanRequest request)
    {
        var booking = await db.TempahanRamadans.FindAsync(id);
        if (booking is null) return null;

        return await WithDateLockAsync(request.BookedDate!.Value, async () =>
        {
            await EnsureRulesAsync(request, id);

            Apply(booking, request);
            await SaveAsync();
            return booking;
        });
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

        // Sum slots per booking (Amount / SlotPrice), not rows. Amount is validated as a multiple of SlotPrice.
        var counts = (await query
                .GroupBy(t => t.BookedDate)
                .Select(g => new { Date = g.Key, Booked = g.Sum(t => (int)(t.Amount / SlotPrice)) })
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

    /// <summary>
    /// Serialises "check the cap, then insert" per date with a MySQL named lock (works across API instances).
    /// The lock belongs to the connection, so the connection is held open for the whole check + save.
    /// </summary>
    private async Task<T> WithDateLockAsync<T>(DateOnly date, Func<Task<T>> action)
    {
        var name = $"maq:booking:{date:yyyy-MM-dd}";
        await db.Database.OpenConnectionAsync();
        try
        {
            var conn = db.Database.GetDbConnection();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT GET_LOCK(@n, 10)";
                var p = cmd.CreateParameter();
                p.ParameterName = "@n";
                p.Value = name;
                cmd.Parameters.Add(p);
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) != 1)
                    throw new TimeoutException("Could not acquire the booking lock for that date; try again.");
            }
            try
            {
                return await action();
            }
            finally
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT RELEASE_LOCK(@n)";
                var p = cmd.CreateParameter();
                p.ParameterName = "@n";
                p.Value = name;
                cmd.Parameters.Add(p);
                await cmd.ExecuteScalarAsync(CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Counts slots of ALL existing bookings on the date (paid or not, so pending payments cannot cause
    /// overbooking) plus the slots in this request. The calendar shows paid slots only.
    /// </summary>
    private async Task EnsureRulesAsync(TempahanRamadanRequest request, int excludeId)
    {
        var date = request.BookedDate!.Value;

        var used = await db.TempahanRamadans
            .Where(t => t.BookedDate == date && t.TempahanRamadanId != excludeId)
            .SumAsync(t => (int)(t.Amount / SlotPrice));
        var requested = (int)(request.Amount / SlotPrice);

        if (used + requested > MaxBookingsPerDay)
            throw new DailyLimitReachedException(MaxBookingsPerDay, Math.Max(0, MaxBookingsPerDay - used));
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
        booking.ArwahName = request.ArwahName;
        booking.Address = request.Address;
        booking.Amount = request.Amount;
    }
}
