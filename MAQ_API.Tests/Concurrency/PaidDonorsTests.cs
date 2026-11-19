using System.Net;
using System.Text.Json;
using MAQ_API.Data;
using MAQ_API.Models;
using MAQ_API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MAQ_API.Tests.Concurrency;

/// <summary>Public paid-donor endpoint and the aggregated payment status on the admin list.</summary>
[Trait("Category", "Integration")]
public class PaidDonorsTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private const decimal Slot = TempahanRamadanService.SlotPrice;
    private static readonly DateOnly Day1 = new(2032, 2, 1);
    private static readonly DateOnly Day2 = new(2032, 2, 2);
    private static readonly DateOnly Day3 = new(2032, 2, 3);

    private readonly HttpClient _anon = fx.Factory.CreateClient();

    [Fact]
    public async Task Donors_ReturnsOnlyPaidBookings_AndOnlyIdAndDisplayName()
    {
        await ResetAsync(Day1);
        var paid = Assert.Single(await SeedAsync(Day1, "Ahmad Abdullah", PaymentStatus.Paid));
        await SeedAsync(Day1, "Pending Person", PaymentStatus.Pending);
        await SeedAsync(Day1, "Failed Person", PaymentStatus.Failed);

        var res = await _anon.GetAsync($"/api/TempahanRamadan/donors?date={Day1:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var donor = Assert.Single(json.RootElement.EnumerateArray().ToList());

        Assert.Equal(2, donor.EnumerateObject().Count());
        Assert.Equal(paid.ToString(), donor.GetProperty("id").GetString());
        Assert.Equal("Ahmad Abdullah", donor.GetProperty("displayName").GetString());
        Assert.False(donor.TryGetProperty("contactNumber", out _));
        Assert.False(donor.TryGetProperty("arwahName", out _));
    }

    [Fact]
    public async Task Donors_CollapsesMultiplePaymentAttemptsIntoOneRow()
    {
        await ResetAsync(Day2);
        var id = Assert.Single(await SeedAsync(Day2, "Multi Payer",
            PaymentStatus.Failed, PaymentStatus.Pending, PaymentStatus.Paid));

        var res = await _anon.GetAsync($"/api/TempahanRamadan/donors?date={Day2:yyyy-MM-dd}");
        res.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var donor = Assert.Single(json.RootElement.EnumerateArray().ToList());
        Assert.Equal(id.ToString(), donor.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Donors_ReturnsEmptyArray_WhenNobodyPaid()
    {
        await ResetAsync(Day3);
        await SeedAsync(Day3, "Pending Person", PaymentStatus.Pending);
        await SeedAsync(Day3, "Failed Person", PaymentStatus.Failed);

        var res = await _anon.GetAsync($"/api/TempahanRamadan/donors?date={Day3:yyyy-MM-dd}");
        res.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Empty(json.RootElement.EnumerateArray());
    }

    [Theory]
    [InlineData("/api/TempahanRamadan/donors")]
    [InlineData("/api/TempahanRamadan/donors?date=not-a-date")]
    [InlineData("/api/TempahanRamadan/donors?date=2032-02-30")]
    public async Task Donors_BadDate_Returns400(string url)
    {
        var res = await _anon.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task AdminList_ReportsAggregatedPaymentStatus()
    {
        var d = Day3.AddDays(1);
        await ResetAsync(d);
        var paid = Assert.Single(await SeedAsync(d, "Paid One", PaymentStatus.Pending, PaymentStatus.Paid));
        var pending = Assert.Single(await SeedAsync(d, "Pending One", PaymentStatus.Failed, PaymentStatus.Pending));
        var failed = Assert.Single(await SeedAsync(d, "Failed One", PaymentStatus.Failed));
        var none = Assert.Single(await SeedAsync(d, "No Payment"));

        var admin = fx.CreateAdminClient();
        var res = await admin.GetAsync("/api/TempahanRamadan");
        res.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var rows = json.RootElement.EnumerateArray().ToList();

        Assert.Equal("Paid", StatusOf(rows, paid));
        Assert.Equal("Pending", StatusOf(rows, pending));
        Assert.Equal("Failed", StatusOf(rows, failed));
        Assert.Null(StatusOf(rows, none));
    }

    // ---------- helpers ----------

    private static string? StatusOf(List<JsonElement> rows, int id)
    {
        var row = Assert.Single(rows.Where(r => r.GetProperty("tempahanRamadanId").GetInt32() == id));
        var status = row.GetProperty("paymentStatus");
        return status.ValueKind == JsonValueKind.Null ? null : status.GetString();
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

    /// <summary>Inserts one booking with one payment row per given status (none when empty).</summary>
    private async Task<List<int>> SeedAsync(DateOnly d, string name, params PaymentStatus[] payments)
    {
        using var scope = fx.Factory.Services.CreateScope();
        var db = Db(scope);
        var now = DateTime.UtcNow;
        var booking = new TempahanRamadan
        {
            BookedDate = d, CustomerName = name, ContactNumber = "0123456789", ICNumber = "900101-10-1234",
            Age = 30, Address = "Seed address", Amount = Slot, CreatedAt = now,
        };
        db.TempahanRamadans.Add(booking);
        await db.SaveChangesAsync();

        db.Payments.AddRange(payments.Select(s => new Payment
        {
            TempahanRamadanId = booking.TempahanRamadanId, PaymentCode = $"SEED-{Guid.NewGuid():N}"[..30],
            Amount = booking.Amount, Description = "seed", PayerName = "Seed", PayerEmail = "seed@example.com",
            PayerPhone = "0123456789", BankPrefix = "TEST0021", PaymentMethod = "Online Banking", Status = s,
            CreatedAt = now, UpdatedAt = now, PaidAt = s == PaymentStatus.Paid ? now : null,
        }));
        await db.SaveChangesAsync();
        return [booking.TempahanRamadanId];
    }
}
