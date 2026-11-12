namespace MAQ_API.Dtos;

public record PaymentChannelResponse(string PaymentMethod, PaymentCharges Charges, List<PaymentBank> Channels);
public record PaymentCharges(string Currency, string Fixed, string Percentage);
public record PaymentBank(string Prefix, string Name, string ImgUrl);

public record InitiatePaymentResponse(string PaymentCode, string FormHtml);

public record PaymentStatusResponse(string PaymentCode, string Status, decimal Amount, DateTime? PaidAt);

/// <summary>Result of Herepay's get-transaction query.</summary>
public record HerepayTransaction(string Status, string StatusCode, string Amount);
