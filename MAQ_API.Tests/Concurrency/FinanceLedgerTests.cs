using System.Globalization;
using System.Net.Http.Json;
using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Helpers;
using MAQ_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MAQ_API.Tests.Concurrency;

/// <summary>The stored balance must always equal the sum of ledger rows, even under concurrency.</summary>
[Trait("Category", "Integration")]
public class FinanceLedgerTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = fx.CreateAdminClient();

    [Fact]
    public async Task ManualIncome_AddsToBalanceAndList()
    {
        await ResetAsync();
        var res = await _client.PostAsJsonAsync("/api/finance/transactions",
            new ManualIncomeRequest { Amount = 300m, Category = FinanceCategory.Donation, Description = "Cash" });
        res.EnsureSuccessStatusCode();

        var balance = await _client.GetFromJsonAsync<BalanceResponse>("/api/finance/balance");
        var list = await _client.GetFromJsonAsync<List<FinancialTransactionResponse>>("/api/finance/transactions");
        Assert.Equal(300m, balance!.Balance);
        Assert.Single(list!);
    }

    [Theory]
    [InlineData("\"Donation\"")] // what the finance client sends
    [InlineData("1")]            // numeric still accepted
    public async Task ManualIncome_AcceptsCategoryAsNameOrNumber(string category)
    {
        await ResetAsync();
        var body = new StringContent($$"""{"amount":50,"category":{{category}},"description":"Cash"}""",
            System.Text.Encoding.UTF8, "application/json");
        var res = await _client.PostAsync("/api/finance/transactions", body);
        Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("\"category\":\"Donation\"", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Finance_WithoutToken_Returns401()
    {
        using var anon = fx.Factory.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/finance/balance")).StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        using var anon = fx.Factory.CreateClient();
        var res = await anon.PostAsJsonAsync("/api/account/login", new LoginRequest("admin@test.local", "wrong"));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ManualIncome_RejectsNonPositiveAmount()
    {
        var res = await _client.PostAsJsonAsync("/api/finance/transactions",
            new ManualIncomeRequest { Amount = 0m, Description = "x" });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ConcurrentManualIncome_BalanceEqualsSumOfRows()
    {
        await ResetAsync();
        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() =>
            _client.PostAsJsonAsync("/api/finance/transactions",
                new ManualIncomeRequest { Amount = 10m, Category = FinanceCategory.Other, Description = "parallel" })));
        foreach (var r in await Task.WhenAll(tasks)) r.EnsureSuccessStatusCode();

        Assert.Equal(200m, (await _client.GetFromJsonAsync<BalanceResponse>("/api/finance/balance"))!.Balance);
        Assert.Equal(200m, await SumRowsAsync());
    }

    [Fact]
    public async Task DuplicateCallbacks_CountPaymentOnce()
    {
        await ResetAsync();
        var code = await SeedPendingPaymentAsync(150m);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => CallbackAsync(code, 150m))));
        Assert.All(results, s => Assert.Equal(System.Net.HttpStatusCode.OK, s));

        Assert.Equal(150m, (await _client.GetFromJsonAsync<BalanceResponse>("/api/finance/balance"))!.Balance);
        using var scope = fx.Factory.Services.CreateScope();
        Assert.Equal(1, await Db(scope).FinancialTransactions.CountAsync(t => t.Source == FinanceSource.Herepay));
    }

    private async Task<decimal> SumRowsAsync()
    {
        using var scope = fx.Factory.Services.CreateScope();
        return await Db(scope).FinancialTransactions.SumAsync(t => t.Amount);
    }

    private async Task<string> SeedPendingPaymentAsync(decimal amount)
    {
        using var scope = fx.Factory.Services.CreateScope();
        var db = Db(scope);
        var now = DateTime.UtcNow;
        var code = $"FIN-{Guid.NewGuid():N}"[..30];
        db.Payments.Add(new Payment
        {
            PaymentCode = code, Amount = amount, Description = "finance test", PayerName = "T",
            PayerEmail = "t@example.com", PayerPhone = "0123456789", BankPrefix = "TEST0021",
            PaymentMethod = "Online Banking", Status = PaymentStatus.Pending, CreatedAt = now, UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        // FakeHerepayClient only confirms codes it saw in InitiateAsync, so register it
        await fx.Factory.Services.GetRequiredService<MAQ_API.Services.IHerepayClient>()
            .InitiateAsync(db.Payments.Single(p => p.PaymentCode == code));
        return code;
    }

    private async Task<System.Net.HttpStatusCode> CallbackAsync(string code, decimal amount)
    {
        var fields = new Dictionary<string, string>
        {
            ["payment_code"] = code,
            ["amount"] = amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["status_code"] = "00",
            ["reference_code"] = "HP-TEST",
            ["transaction_id"] = Guid.NewGuid().ToString("N"),
            ["bank_name"] = "Test Bank",
            ["fpx_type"] = "B2C",
            ["message"] = "Success",
        };
        fields["checksum"] = ChecksumGenerator.Generate(fields, ApiFixture.PrivateKey);
        return (await _client.PostAsync("/api/payments/callback", new FormUrlEncodedContent(fields))).StatusCode;
    }

    private async Task ResetAsync()
    {
        using var scope = fx.Factory.Services.CreateScope();
        var db = Db(scope);
        await db.FinancialTransactions.ExecuteDeleteAsync();
        await db.Payments.ExecuteDeleteAsync();
        await db.CompanyFinances.ExecuteUpdateAsync(s => s.SetProperty(f => f.Balance, 0m));
    }

    private static AppDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();
}
