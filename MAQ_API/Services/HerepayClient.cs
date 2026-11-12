using System.Globalization;
using System.Text.Json;
using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Helpers;
using MAQ_API.Models;
using MAQ_API.Options;
using Microsoft.Extensions.Options;

namespace MAQ_API.Services;

public class HerepayClient(HttpClient http, IOptions<HerepayOptions> options, ILogger<HerepayClient> logger) : IHerepayClient
{
    private readonly HerepayOptions _opt = options.Value;

    public async Task<List<PaymentChannelResponse>> GetChannelsAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(() => http.GetAsync("api/v1/herepay/payment/channels", ct));
        if (!response.IsSuccessStatusCode) throw await ToExceptionAsync(response, ct);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var result = new List<PaymentChannelResponse>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return result;

        foreach (var m in data.EnumerateArray())
        {
            var charges = m.TryGetProperty("charges", out var c)
                ? new PaymentCharges(Str(c, "currency"), Str(c, "fixed"), Str(c, "percentage"))
                : new PaymentCharges("", "", "");
            var banks = new List<PaymentBank>();
            if (m.TryGetProperty("channels", out var chs) && chs.ValueKind == JsonValueKind.Array)
                foreach (var ch in chs.EnumerateArray())
                    if (string.Equals(Str(ch, "active"), "Active", StringComparison.OrdinalIgnoreCase))
                        banks.Add(new PaymentBank(Str(ch, "prefix"), Str(ch, "name"), Str(ch, "img_url")));
            result.Add(new PaymentChannelResponse(Str(m, "payment_method"), charges, banks));
        }
        return result;
    }

    public async Task<string> InitiateAsync(Payment p, CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string>
        {
            ["name"] = p.PayerName,
            ["email"] = p.PayerEmail,
            ["phone"] = p.PayerPhone,
            ["amount"] = p.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["description"] = p.Description,
            ["payment_code"] = p.PaymentCode,
            ["payment_method"] = p.PaymentMethod,
            ["bank_prefix"] = p.BankPrefix,
            ["callback_url"] = _opt.CallbackUrl,
            ["redirect_url"] = _opt.RedirectUrl,
        };
        fields["checksum"] = ChecksumGenerator.Generate(fields, _opt.PrivateKey);

        using var response = await SendAsync(() => http.PostAsync("api/v1/herepay/initiate", new FormUrlEncodedContent(fields), ct));
        if (!response.IsSuccessStatusCode) throw await ToExceptionAsync(response, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<HerepayTransaction?> GetTransactionAsync(string paymentCode, CancellationToken ct = default)
    {
        using var response = await SendAsync(() =>
            http.GetAsync($"api/v1/herepay/transactions/{Uri.EscapeDataString(paymentCode)}", ct));
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw await ToExceptionAsync(response, ct);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("data", out var d)) return null;
        return new HerepayTransaction(Str(d, "status"), Str(d, "status_code"), Str(d, "amount"));
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()
            : "";

    private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        try { return await send(); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Herepay request failed");
            throw new HerepayException("Payment gateway is unreachable.", isClientError: false);
        }
    }

    private async Task<HerepayException> ToExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        var status = (int)response.StatusCode;
        logger.LogWarning("Herepay returned {Status}: {Body}", status, body.Length > 500 ? body[..500] : body);

        var isClient = status is 400 or 422;
        var message = isClient ? ExtractMessage(body) : "Payment gateway rejected the request.";
        return new HerepayException(message, isClient);
    }

    private static string ExtractMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                return string.Join("; ", errors.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray().Select(m => $"{p.Name}: {m.GetString()}")));
            if (doc.RootElement.TryGetProperty("message", out var msg)) return msg.ToString();
        }
        catch (JsonException) { }
        return "Payment could not be created.";
    }
}
