using MAQ_API.Dtos;

namespace MAQ_API.Services;

public interface IPaymentService
{
    Task<List<PaymentChannelResponse>> GetChannelsAsync(CancellationToken ct = default);
    Task<InitiatePaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken ct = default);
    Task HandleCallbackAsync(Dictionary<string, string> fields, CancellationToken ct = default);
    Task<PaymentStatusResponse?> GetStatusAsync(string paymentCode, CancellationToken ct = default);
}
