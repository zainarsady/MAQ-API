using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MAQ_API.Dtos;
using MAQ_API.Models;
using MAQ_API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MySql;
using Xunit;

namespace MAQ_API.Tests.Concurrency;

/// <summary>Real MySQL 8.0.36 in Docker + the API in-process, with Herepay faked.</summary>
public class ApiFixture : IAsyncLifetime
{
    public const string PrivateKey = "test-private-key";
    private const string AdminEmail = "admin@test.local";
    private const string AdminPassword = "Test@12345";

    private readonly MySqlContainer _mysql = new MySqlBuilder().WithImage("mysql:8.0.36").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    private string _adminToken = null!;

    /// <summary>A client carrying the seeded admin's Bearer token.</summary>
    public HttpClient CreateAdminClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
        return client;
    }

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:DefaultConnection", _mysql.GetConnectionString());
            b.UseSetting("Herepay:BaseUrl", "https://herepay.test");
            b.UseSetting("Herepay:SecretKey", "x");
            b.UseSetting("Herepay:ApiKey", "x");
            b.UseSetting("Herepay:PrivateKey", PrivateKey);
            b.UseSetting("Herepay:CallbackUrl", "https://api.test/api/payments/callback");
            b.UseSetting("Herepay:RedirectUrl", "https://web.test/return");
            b.UseSetting("JwtSettings:SecretKey", "test-secret-key-that-is-at-least-32-bytes-long!!");
            b.UseSetting("JwtSettings:Issuer", "maq-api");
            b.UseSetting("JwtSettings:Audience", "maq-frontend");
            b.UseSetting("JwtSettings:ExpiryInMinutes", "60");
            b.UseSetting("SeedAdmin:Email", AdminEmail);
            b.UseSetting("SeedAdmin:UserName", "testadmin");
            b.UseSetting("SeedAdmin:Password", AdminPassword);
            b.UseSetting("Logging:LogLevel:Default", "Warning");
            b.ConfigureTestServices(s => s.AddSingleton<IHerepayClient, FakeHerepayClient>());
        });
        using var anon = Factory.CreateClient(); // boots the host: Database.Migrate() + admin seeding
        var login = await anon.PostAsJsonAsync("/api/account/login", new LoginRequest(AdminEmail, AdminPassword));
        login.EnsureSuccessStatusCode();
        _adminToken = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _mysql.DisposeAsync();
    }
}

/// <summary>Herepay stand-in: accepts every payment and confirms it as paid ("00").</summary>
public class FakeHerepayClient : IHerepayClient
{
    private readonly ConcurrentDictionary<string, decimal> _amounts = new();

    public Task<List<PaymentChannelResponse>> GetChannelsAsync(CancellationToken ct = default) => Task.FromResult(new List<PaymentChannelResponse>());

    public Task<string> InitiateAsync(Payment payment, CancellationToken ct = default)
    {
        _amounts[payment.PaymentCode] = payment.Amount;
        return Task.FromResult("<form></form>");
    }

    public Task<HerepayTransaction?> GetTransactionAsync(string paymentCode, CancellationToken ct = default) =>
        Task.FromResult(_amounts.TryGetValue(paymentCode, out var amount)
            ? new HerepayTransaction("Success", "00", amount.ToString("0.00", CultureInfo.InvariantCulture))
            : null);
}
