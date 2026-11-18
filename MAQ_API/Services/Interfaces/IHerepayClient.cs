using MAQ_API.Dtos;
using MAQ_API.Models;

namespace MAQ_API.Services;

public interface IHerepayClient
{
    Task<List<PaymentChannelResponse>> GetChannelsAsync(CancellationToken ct = default);
    /// <summary>Returns Herepay's auto-submit HTML form.</summary>
    Task<string> InitiateAsync(Payment payment, CancellationToken ct = default);
    /// <summary>Null when Herepay does not know the payment code (404).</summary>
    Task<HerepayTransaction?> GetTransactionAsync(string paymentCode, CancellationToken ct = default);
}
