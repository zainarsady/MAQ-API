using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Helpers;
using MAQ_API.Models;
using MAQ_API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace MAQ_API.Tests.Concurrency;

/// <summary>
/// A date has 1 slot left (19 paid of MaxBookingsPerDay = 20) and two customers act at the same time.
/// Each scenario repeats <see cref="Iterations"/> times, writes a report to TestResults/LastSlotRace/,
/// then asserts the intended rule: never more than 20 paid bookings per date.
/// </summary>
[Trait("Category", "Integration")]
public class LastSlotRaceTests(ApiFixture fx, ITestOutputHelper output) : IClassFixture<ApiFixture>
{
    private const int Iterations = 20;
    private const int Max = TempahanRamadanService.MaxBookingsPerDay;
    private static readonly DateOnly Date = new(2030, 3, 1);

    private readonly HttpClient _client = fx.Factory.CreateClient();

    [Fact]
    public async Task Sanity_SequentialSecondBooking_IsRejected()
    {
        await ResetAsync();
        await SeedAsync(paid: Max - 1, unpaid: 0);

        var first = await PostBookingAsync("A");
        var second = await PostBookingAsync("B");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>S1: both customers POST a booking at the same instant.</summary>
    [Fact]
    public async Task S1_ConcurrentBookingCreation()
    {
        var rows = new List<string>();
        var overbooked = 0;
        for (var i = 1; i <= Iterations; i++)
        {
            await ResetAsync();
            await SeedAsync(paid: Max - 1, unpaid: 0);

            var (a, b) = await TogetherAsync(() => PostBookingAsync("A"), () => PostBookingAsync("B"));
            var total = await CountBookingsAsync();
            if (total > Max) overbooked++;
            rows.Add($"| {i} | {(int)a.StatusCode} | {(int)b.StatusCode} | {total} |");
        }

        Report("S1-concurrent-booking-creation",
            "19 paid bookings exist. Two `POST /api/TempahanRamadan` sent at the same instant.",
            "| # | User A | User B | Booking rows for date |\n|---|---|---|---|", rows, overbooked);
        Assert.True(overbooked == 0, $"{overbooked}/{Iterations} iterations created more than {Max} bookings");
    }

    /// <summary>S2: two bookings already got in (e.g. via S1); both pay at the same instant.</summary>
    [Fact]
    public async Task S2_TwoBookingsPayConcurrently()
    {
        var rows = new List<string>();
        var overbooked = 0;
        for (var i = 1; i <= Iterations; i++)
        {
            await ResetAsync();
            var ids = await SeedAsync(paid: Max - 1, unpaid: 2);

            var (ia, ib) = await TogetherAsync(() => InitiateAsync(ids[0]), () => InitiateAsync(ids[1]));
            var (ca, cb) = await TogetherAsync(() => CallbackAsync(ia.Code!), () => CallbackAsync(ib.Code!));
            var paid = await CountPaidBookingsAsync();
            var avail = await AvailabilityAsync();
            if (paid > Max) overbooked++;
            rows.Add($"| {i} | {(int)ia.Status} / {(int)ca} | {(int)ib.Status} / {(int)cb} | {paid} | {avail.Booked} | {avail.Remaining} |");
        }

        Report("S2-two-bookings-pay-concurrently",
            "19 paid + 2 unpaid bookings exist. Both customers `POST /api/payments` at once, then both Herepay success callbacks arrive at once.",
            "| # | A initiate / callback | B initiate / callback | Paid bookings | Availability.Booked | Availability.Remaining |\n|---|---|---|---|---|---|",
            rows, overbooked);
        Assert.True(overbooked == 0, $"{overbooked}/{Iterations} iterations ended with more than {Max} paid bookings");
    }

    /// <summary>S3: both customers run the whole flow (book → pay → callback) in parallel.</summary>
    [Fact]
    public async Task S3_EndToEnd()
    {
        var rows = new List<string>();
        var overbooked = 0;
        for (var i = 1; i <= Iterations; i++)
        {
            await ResetAsync();
            await SeedAsync(paid: Max - 1, unpaid: 0);

            var (a, b) = await TogetherAsync(() => FullFlowAsync("A"), () => FullFlowAsync("B"));
            var paid = await CountPaidBookingsAsync();
            var avail = await AvailabilityAsync();
            if (paid > Max) overbooked++;
            rows.Add($"| {i} | {a} | {b} | {paid} | {avail.Remaining} |");
        }

        Report("S3-end-to-end",
            "19 paid bookings exist. Two customers each run book → pay → success callback, in parallel.",
            "| # | User A | User B | Paid bookings | Availability.Remaining |\n|---|---|---|---|---|", rows, overbooked);
        Assert.True(overbooked == 0, $"{overbooked}/{Iterations} iterations ended with more than {Max} paid bookings");
    }

    /// <summary>S4: one booking, payment submitted twice at once (double-click / two tabs).</summary>
    [Fact]
    public async Task S4_SameBookingPaidTwice()
    {
        var rows = new List<string>();
        var doubleCharged = 0;
        for (var i = 1; i <= Iterations; i++)
        {
            await ResetAsync();
            var ids = await SeedAsync(paid: Max - 1, unpaid: 1);

            var (ia, ib) = await TogetherAsync(() => InitiateAsync(ids[0]), () => InitiateAsync(ids[0]));
            HttpStatusCode? ca = null, cb = null;
            if (ia.Code is not null && ib.Code is not null)
                (ca, cb) = await TogetherAsync(() => CallbackAsync(ia.Code), () => CallbackAsync(ib.Code));
            var paidPayments = await CountPaidPaymentsAsync(ids[0]);
            if (paidPayments > 1) doubleCharged++;
            rows.Add($"| {i} | {(int)ia.Status} / {(int?)ca} | {(int)ib.Status} / {(int?)cb} | {paidPayments} | {await CountPaidBookingsAsync()} |");
        }

        Report("S4-same-booking-paid-twice",
            "19 paid + 1 unpaid booking. The unpaid booking's owner submits payment twice at once; both success callbacks arrive at once.",
            "| # | Attempt 1 initiate / callback | Attempt 2 initiate / callback | Paid payments for booking | Paid bookings for date |\n|---|---|---|---|---|",
            rows, doubleCharged, "double-charged");
        Assert.True(doubleCharged == 0, $"{doubleCharged}/{Iterations} iterations charged the same booking twice");
    }

    // ---------- flow helpers ----------

    private async Task<string> FullFlowAsync(string who)
    {
        var res = await PostBookingAsync(who);
        if (res.StatusCode != HttpStatusCode.Created) return $"booking {(int)res.StatusCode}";
        var booking = (await res.Content.ReadFromJsonAsync<TempahanRamadan>())!;
        var init = await InitiateAsync(booking.TempahanRamadanId);
        if (init.Code is null) return $"booking 201, initiate {(int)init.Status}";
        var cb = await CallbackAsync(init.Code);
        var status = await _client.GetFromJsonAsync<PaymentStatusResponse>($"/api/payments/{init.Code}");
        return $"booking 201, initiate 200, callback {(int)cb}, payment {status!.Status}";
    }

    private Task<HttpResponseMessage> PostBookingAsync(string who) =>
        _client.PostAsJsonAsync("/api/TempahanRamadan", new TempahanRamadanRequest(
            Date, $"Customer {who}", "0123456789", "900101-10-1234", 30, null, "Test address", 300m));

    private async Task<(HttpStatusCode Status, string? Code)> InitiateAsync(int bookingId)
    {
        var res = await _client.PostAsJsonAsync("/api/payments",
            new CreatePaymentRequest(bookingId, "test@example.com", "TEST0021", "Online Banking"));
        if (!res.IsSuccessStatusCode) return (res.StatusCode, null);
        return (res.StatusCode, (await res.Content.ReadFromJsonAsync<InitiatePaymentResponse>())!.PaymentCode);
    }

    private async Task<HttpStatusCode> CallbackAsync(string paymentCode)
    {
        decimal amount;
        using (var scope = fx.Factory.Services.CreateScope())
            amount = await Db(scope).Payments.Where(p => p.PaymentCode == paymentCode).Select(p => p.Amount).SingleAsync();

        var fields = new Dictionary<string, string>
        {
            ["payment_code"] = paymentCode,
            ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["status_code"] = "00",
            ["reference_code"] = "HP-TEST",
            ["transaction_id"] = Guid.NewGuid().ToString("N"),
            ["bank_name"] = "Test Bank",
            ["fpx_type"] = "B2C",
            ["message"] = "Success",
        };
        fields["checksum"] = ChecksumGenerator.Generate(fields, ApiFixture.PrivateKey);
        var res = await _client.PostAsync("/api/payments/callback", new FormUrlEncodedContent(fields));
        return res.StatusCode;
    }

    private async Task<DateAvailabilityResponse> AvailabilityAsync()
    {
        var d = Date.ToString("yyyy-MM-dd");
        var list = await _client.GetFromJsonAsync<List<DateAvailabilityResponse>>($"/api/TempahanRamadan/availability?from={d}&to={d}");
        return list!.Single();
    }

    /// <summary>Starts both calls, then releases them at the same moment.</summary>
    private static async Task<(T, T)> TogetherAsync<T>(Func<Task<T>> a, Func<Task<T>> b)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ta = Task.Run(async () => { await gate.Task; return await a(); });
        var tb = Task.Run(async () => { await gate.Task; return await b(); });
        gate.SetResult();
        return (await ta, await tb);
    }

    // ---------- data helpers ----------

    private static AppDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task ResetAsync()
    {
        using var scope = fx.Factory.Services.CreateScope();
        var db = Db(scope);
        await db.Payments.ExecuteDeleteAsync();
        await db.TempahanRamadans.Where(t => t.BookedDate == Date).ExecuteDeleteAsync();
    }

    /// <summary>Inserts paid and unpaid bookings for <see cref="Date"/>; returns the unpaid booking ids.</summary>
    private async Task<List<int>> SeedAsync(int paid, int unpaid)
    {
        using var scope = fx.Factory.Services.CreateScope();
        var db = Db(scope);
        var now = DateTime.UtcNow;
        TempahanRamadan NewBooking(string name) => new()
        {
            BookedDate = Date, CustomerName = name, ContactNumber = "0123456789", ICNumber = "900101-10-1234",
            Age = 30, Address = "Seed address", Amount = 300m, CreatedAt = now,
        };

        var paidBookings = Enumerable.Range(1, paid).Select(i => NewBooking($"Paid {i}")).ToList();
        var unpaidBookings = Enumerable.Range(1, unpaid).Select(i => NewBooking($"Unpaid {i}")).ToList();
        db.TempahanRamadans.AddRange(paidBookings);
        db.TempahanRamadans.AddRange(unpaidBookings);
        await db.SaveChangesAsync();

        db.Payments.AddRange(paidBookings.Select(b => new Payment
        {
            TempahanRamadanId = b.TempahanRamadanId, PaymentCode = $"SEED-{Guid.NewGuid():N}"[..30], Amount = b.Amount,
            Description = "seed", PayerName = b.CustomerName, PayerEmail = "seed@example.com", PayerPhone = "0123456789",
            BankPrefix = "TEST0021", PaymentMethod = "Online Banking", Status = PaymentStatus.Paid,
            CreatedAt = now, UpdatedAt = now, PaidAt = now,
        }));
        await db.SaveChangesAsync();
        return unpaidBookings.Select(b => b.TempahanRamadanId).ToList();
    }

    private async Task<int> CountBookingsAsync()
    {
        using var scope = fx.Factory.Services.CreateScope();
        return await Db(scope).TempahanRamadans.CountAsync(t => t.BookedDate == Date);
    }

    private async Task<int> CountPaidBookingsAsync()
    {
        using var scope = fx.Factory.Services.CreateScope();
        var db = Db(scope);
        return await db.TempahanRamadans.CountAsync(t => t.BookedDate == Date &&
            db.Payments.Any(p => p.TempahanRamadanId == t.TempahanRamadanId && p.Status == PaymentStatus.Paid));
    }

    private async Task<int> CountPaidPaymentsAsync(int bookingId)
    {
        using var scope = fx.Factory.Services.CreateScope();
        return await Db(scope).Payments.CountAsync(p => p.TempahanRamadanId == bookingId && p.Status == PaymentStatus.Paid);
    }

    // ---------- report ----------

    private void Report(string name, string setup, string header, List<string> rows, int failures, string failureLabel = "over capacity")
    {
        var md = new StringBuilder()
            .AppendLine($"# {name}")
            .AppendLine()
            .AppendLine($"Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss} · MySQL 8.0.36 (Testcontainers) · {Iterations} iterations · limit {Max}/day")
            .AppendLine()
            .AppendLine($"**Setup:** {setup}")
            .AppendLine()
            .AppendLine($"**Result:** {failures}/{Iterations} iterations {failureLabel}.")
            .AppendLine()
            .AppendLine(header);
        rows.ForEach(r => md.AppendLine(r));

        var dir = Path.Combine(RepoRoot(), "TestResults", "LastSlotRace");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{name}.md"), md.ToString());
        output.WriteLine(md.ToString());
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "MAQ_API.sln"))) dir = dir.Parent!;
        return dir.FullName;
    }
}
