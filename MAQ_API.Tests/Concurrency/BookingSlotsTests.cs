using System.Net;
using System.Net.Http.Json;
using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Models;
using MAQ_API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MAQ_API.Tests.Concurrency;

/// <summary>A booking's Amount is slots x RM300; availability and the daily cap are counted in slots, not rows.</summary>
[Trait("Category", "Integration")]
public class BookingSlotsTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private const int Max = TempahanRamadanService.MaxBookingsPerDay;
    private const decimal Slot = TempahanRamadanService.SlotPrice;
    private static readonly DateOnly Base = new(2031, 1, 1);

    private readonly HttpClient _client = fx.Factory.CreateClient();

    [Fact]
    public async Task PaidThreeSlotBooking_CountsAsThreeSlots()
    {
        var d = Base;
        await ResetAsync(d);
        await SeedAsync(d, slots: 3, PaymentStatus.Paid);

        var a = await AvailabilityAsync(d);
        Assert.Equal((Max, 3, Max - 3), (a.MaxBookings, a.Booked, a.Remaining));
    }

    [Fact]
    public async Task OnlyPaidBookingsCount_AndABookingWithTwoPaymentsCountsOnce()
    {
        var d = Base.AddDays(1);
        await ResetAsync(d);
        await SeedAsync(d, 2, PaymentStatus.Pending);
        await SeedAsync(d, 2, PaymentStatus.Failed);
        await SeedAsync(d, 4, PaymentStatus.Failed, PaymentStatus.Paid); // failed attempt, then paid

        Assert.Equal(4, (await AvailabilityAsync(d)).Booked);
    }

    [Fact]
    public async Task TwoPaidTenSlotBookings_LeaveNothing_AndRemainingNeverGoesNegative()
    {
        var d = Base.AddDays(2);
        await ResetAsync(d);
        await SeedAsync(d, 10, PaymentStatus.Paid);
        await SeedAsync(d, 10, PaymentStatus.Paid);
        Assert.Equal(0, (await AvailabilityAsync(d)).Remaining);

        await SeedAsync(d, 5, PaymentStatus.Paid); // already overbooked data
        var a = await AvailabilityAsync(d);
        Assert.Equal((25, 0), (a.Booked, a.Remaining));
    }

    [Fact]
    public async Task Range_FillsEmptyDaysWithZeroBooked()
    {
        var from = Base.AddDays(10);
        var to = from.AddDays(4);
        for (var d = from; d <= to; d = d.AddDays(1)) await ResetAsync(d);
        await SeedAsync(from.AddDays(2), 6, PaymentStatus.Paid);

        var list = await _client.GetFromJsonAsync<List<DateAvailabilityResponse>>(
            $"/api/TempahanRamadan/availability?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

        Assert.Equal(5, list!.Count);
        Assert.Equal(new[] { 0, 0, 6, 0, 0 }, list.Select(x => x.Booked));
        Assert.Equal(new[] { Max, Max, Max - 6, Max, Max }, list.Select(x => x.Remaining));
    }

    [Fact]
    public async Task Post_FiveSlotsOnDayWith17Used_Is409_ThreeSlotsSucceeds()
    {
        var d = Base.AddDays(3);
        await ResetAsync(d);
        await SeedAsync(d, 10, PaymentStatus.Paid);
        await SeedAsync(d, 7, PaymentStatus.Pending); // unpaid slots still count toward the cap

        var five = await PostAsync(d, 5 * Slot);
        Assert.Equal(HttpStatusCode.Conflict, five.StatusCode);

        var three = await PostAsync(d, 3 * Slot);
        Assert.Equal(HttpStatusCode.Created, three.StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(d, Slot)).StatusCode); // now full
    }

    [Theory]
    [InlineData(0)]
    [InlineData(299)]
    [InlineData(350)]
    [InlineData(6300)]
    [InlineData(6600)]
    [InlineData(-300)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Post_InvalidAmount_Is400(int amount)
    {
        var d = Base.AddDays(4);
        await ResetAsync(d);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(d, amount)).StatusCode);
        Assert.Equal(0, await CountRowsAsync(d));
    }

    [Theory]
    [InlineData(300)]
    [InlineData(6000)]
    public async Task Post_BoundaryAmounts_AreAccepted(int amount)
    {
        var d = Base.AddDays(5);
        await ResetAsync(d);
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(d, amount)).StatusCode);
    }

    /// <summary>TEMPORARY RM2 test amount: accepted, but uses no slots.</summary>
    [Fact]
    public async Task TestAmount_RM2_IsAccepted_AndUsesNoCapacity()
    {
        var d = Base.AddDays(8);
        await ResetAsync(d);
        await SeedAsync(d, Max, PaymentStatus.Paid); // day is full

        var res = await PostAsync(d, 2m);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(Max, (await AvailabilityAsync(d)).Booked);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(d, Slot)).StatusCode);
    }

    [Fact]
    public async Task Put_ValidatesAmount_AndDoesNotCountItselfAgainstTheCap()
    {
        var d = Base.AddDays(6);
        await ResetAsync(d);
        var id = (await SeedAsync(d, 10, PaymentStatus.Pending)).Single();
        var admin = fx.CreateAdminClient();

        var bad = await admin.PutAsJsonAsync($"/api/TempahanRamadan/{id}", Request(d, 350m));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var grow = await admin.PutAsJsonAsync($"/api/TempahanRamadan/{id}", Request(d, 20 * Slot));
        Assert.Equal(HttpStatusCode.OK, grow.StatusCode);
    }

    /// <summary>Two 11-slot bookings on an empty day cannot both fit; the cap check must be atomic.</summary>
    [Fact]
    public async Task ParallelElevenSlotPosts_OnEmptyDay_AtMostOneSucceeds()
    {
        for (var i = 0; i < 20; i++)
        {
            var d = Base.AddDays(7);
            await ResetAsync(d);

            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            {
                await gate.Task;
                return (await PostAsync(d, 11 * Slot)).StatusCode;
            })).ToList();
            gate.SetResult();
            var codes = await Task.WhenAll(tasks);

            Assert.True(codes.Count(c => c == HttpStatusCode.Created) == 1, $"iteration {i}: {string.Join(", ", codes)}");
            Assert.Equal(1, await CountRowsAsync(d));
        }
    }

    // ---------- helpers ----------

    private static TempahanRamadanRequest Request(DateOnly d, decimal amount) =>
        new(d, "Slot Tester", "0123456789", "900101-10-1234", 30, null, "Test address", amount);

    private Task<HttpResponseMessage> PostAsync(DateOnly d, decimal amount) =>
        _client.PostAsJsonAsync("/api/TempahanRamadan", Request(d, amount));

    private async Task<DateAvailabilityResponse> AvailabilityAsync(DateOnly d)
    {
        var s = d.ToString("yyyy-MM-dd");
        var list = await _client.GetFromJsonAsync<List<DateAvailabilityResponse>>($"/api/TempahanRamadan/availability?from={s}&to={s}");
        return list!.Single();
    }

    private static AppDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task ResetAsync(DateOnly d)
    {
        using var scope = fx.Factory.Services.CreateScope();
        var db = Db(scope);
        var ids = await db.TempahanRamadans.Where(t => t.BookedDate == d).Select(t => t.TempahanRamadanId).ToListAsync();
        await db.Payments.Where(p => p.TempahanRamadanId != null && ids.Contains(p.TempahanRamadanId.Value)).ExecuteDeleteAsync();
        await db.TempahanRamadans.Where(t => t.BookedDate == d).ExecuteDeleteAsync();
    }

    private async Task<int> CountRowsAsync(DateOnly d)
    {
        using var scope = fx.Factory.Services.CreateScope();
        return await Db(scope).TempahanRamadans.CountAsync(t => t.BookedDate == d);
    }

    /// <summary>Inserts one booking of <paramref name="slots"/> slots with one payment row per given status.</summary>
    private async Task<List<int>> SeedAsync(DateOnly d, int slots, params PaymentStatus[] payments)
    {
        using var scope = fx.Factory.Services.CreateScope();
        var db = Db(scope);
        var now = DateTime.UtcNow;
        var booking = new TempahanRamadan
        {
            BookedDate = d, CustomerName = "Seed", ContactNumber = "0123456789", ICNumber = "900101-10-1234",
            Age = 30, Address = "Seed address", Amount = slots * Slot, CreatedAt = now,
        };
        db.TempahanRamadans.Add(booking);
        await db.SaveChangesAsync();

        db.Payments.AddRange(payments.Select(s => new Payment
        {
            TempahanRamadanId = booking.TempahanRamadanId, PaymentCode = $"SEED-{Guid.NewGuid():N}"[..30], Amount = booking.Amount,
            Description = "seed", PayerName = "Seed", PayerEmail = "seed@example.com", PayerPhone = "0123456789",
            BankPrefix = "TEST0021", PaymentMethod = "Online Banking", Status = s,
            CreatedAt = now, UpdatedAt = now, PaidAt = s == PaymentStatus.Paid ? now : null,
        }));
        await db.SaveChangesAsync();
        return [booking.TempahanRamadanId];
    }
}
