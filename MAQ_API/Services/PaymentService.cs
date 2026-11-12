using System.Globalization;
using System.Security.Cryptography;
using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Exceptions;
using MAQ_API.Helpers;
using MAQ_API.Models;
using MAQ_API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MAQ_API.Services;

public class PaymentService(
    AppDbContext db,
    IHerepayClient herepay,
    IOptions<HerepayOptions> options,
    ILogger<PaymentService> logger) : IPaymentService
{
    private static readonly TimeSpan ReconcileAfter = TimeSpan.FromMinutes(3);
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public Task<List<PaymentChannelResponse>> GetChannelsAsync(CancellationToken ct = default) =>
        herepay.GetChannelsAsync(ct);

    public async Task<InitiatePaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        var booking = await db.TempahanRamadans.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TempahanRamadanId == request.TempahanRamadanId, ct)
            ?? throw new BookingNotFoundException();

        if (booking.Amount <= 0)
            throw new PaymentNotAllowedException("This booking has no amount to pay.");
        if (await db.Payments.AnyAsync(p =>
                p.TempahanRamadanId == booking.TempahanRamadanId && p.Status == PaymentStatus.Paid, ct))
            throw new PaymentNotAllowedException("This booking has already been paid.");

        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            TempahanRamadanId = booking.TempahanRamadanId,
            PaymentCode = NewPaymentCode(now),
            Amount = booking.Amount,
            Description = $"Tempahan Ramadan {booking.BookedDate:yyyy-MM-dd}",
            PayerName = booking.CustomerName,
            PayerEmail = request.Email,
            PayerPhone = new string(booking.ContactNumber.Where(char.IsDigit).ToArray()),
            BankPrefix = request.BankPrefix,
            PaymentMethod = request.PaymentMethod,
            Status = PaymentStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct); // persisted first so the callback can always find it

        try
        {
            var html = await herepay.InitiateAsync(payment, ct);
            return new InitiatePaymentResponse(payment.PaymentCode, html);
        }
        catch (HerepayException ex)
        {
            payment.Status = PaymentStatus.Failed;
            payment.StatusMessage = Truncate(ex.Message, 500);
            payment.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task HandleCallbackAsync(Dictionary<string, string> fields, CancellationToken ct = default)
    {
        fields.TryGetValue("checksum", out var checksum);
        if (!ChecksumGenerator.Verify(fields, checksum, options.Value.PrivateKey))
            throw new InvalidChecksumException();

        var code = Get(fields, "payment_code");
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.PaymentCode == code, ct);
        if (payment is null)
        {
            logger.LogWarning("Callback for unknown payment code {PaymentCode}", code);
            return; // retries cannot help; controller still replies OK
        }

        if (payment.Status == PaymentStatus.Paid) return; // idempotent, never downgrade

        if (Get(fields, "amount") != Money(payment.Amount))
        {
            logger.LogWarning("Callback amount mismatch for {PaymentCode}", code);
            return;
        }

        var newStatus = Get(fields, "status_code") switch
        {
            "00" => PaymentStatus.Paid,
            "30" => PaymentStatus.Failed,
            _ => PaymentStatus.Pending, // "29" or unknown
        };

        if (newStatus == PaymentStatus.Paid && !await ConfirmWithHerepayAsync(payment, ct))
            return; // left Pending; reconciliation will retry

        var now = DateTime.UtcNow;
        payment.Status = newStatus;
        if (newStatus == PaymentStatus.Paid) payment.PaidAt = now;
        payment.ReferenceCode = Truncate(Get(fields, "reference_code"), 100);
        payment.TransactionId = Truncate(Get(fields, "transaction_id"), 100);
        payment.BankName = Truncate(Get(fields, "bank_name"), 100);
        payment.FpxType = Truncate(Get(fields, "fpx_type"), 30);
        payment.StatusMessage = Truncate(Get(fields, "message"), 500);
        payment.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PaymentStatusResponse?> GetStatusAsync(string paymentCode, CancellationToken ct = default)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.PaymentCode == paymentCode, ct);
        if (payment is null) return null;

        if (payment.Status == PaymentStatus.Pending && DateTime.UtcNow - payment.CreatedAt > ReconcileAfter)
            await ReconcileAsync(payment, ct);

        return new PaymentStatusResponse(payment.PaymentCode, payment.Status.ToString(), payment.Amount, payment.PaidAt);
    }

    private async Task ReconcileAsync(Payment payment, CancellationToken ct)
    {
        try
        {
            var tx = await herepay.GetTransactionAsync(payment.PaymentCode, ct);
            if (tx is null || tx.Amount != Money(payment.Amount)) return;

            var status = tx.StatusCode switch
            {
                "00" => PaymentStatus.Paid,
                "30" => PaymentStatus.Failed,
                _ => PaymentStatus.Pending,
            };
            if (status == PaymentStatus.Pending) return;

            payment.Status = status;
            if (status == PaymentStatus.Paid) payment.PaidAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (HerepayException ex)
        {
            logger.LogWarning(ex, "Reconcile failed for {PaymentCode}", payment.PaymentCode);
        }
    }

    private async Task<bool> ConfirmWithHerepayAsync(Payment payment, CancellationToken ct)
    {
        try
        {
            var tx = await herepay.GetTransactionAsync(payment.PaymentCode, ct);
            var ok = tx is { StatusCode: "00" } && tx.Amount == Money(payment.Amount);
            if (!ok) logger.LogWarning("Backstop check failed for {PaymentCode}", payment.PaymentCode);
            return ok;
        }
        catch (HerepayException ex)
        {
            logger.LogWarning(ex, "Backstop check errored for {PaymentCode}", payment.PaymentCode);
            return false;
        }
    }

    private static string Get(Dictionary<string, string> f, string key) => f.TryGetValue(key, out var v) ? v : "";
    private static string Money(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);
    private static string? Truncate(string? s, int max) => s is { Length: > 0 } ? (s.Length <= max ? s : s[..max]) : null;

    private static string NewPaymentCode(DateTime utc)
    {
        var suffix = string.Create(6, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        });
        return $"MAQ-{utc:yyyyMMddHHmmss}-{suffix}";
    }
}
