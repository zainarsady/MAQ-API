using System.ComponentModel.DataAnnotations;

namespace MAQ_API.Dtos;

/// <summary>Amount, payer name, phone and description are derived from the booking server-side.</summary>
public record CreatePaymentRequest(
    [Required] int? TempahanRamadanId,
    [Required, EmailAddress, MaxLength(50)] string Email,
    [Required, MaxLength(50)] string BankPrefix,
    [Required, RegularExpression("^(Online Banking|Corporate Banking)$")] string PaymentMethod);
