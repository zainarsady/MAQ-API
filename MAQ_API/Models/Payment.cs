using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MAQ_API.Models;

public class Payment
{
    public int Id { get; set; }

    /// <summary>Booking being paid for (nullable so a booking deletion does not erase payment history).</summary>
    public int? TempahanRamadanId { get; set; }

    [Required, MaxLength(50)]
    public string PaymentCode { get; set; } = "";      // unique index; sent to Herepay

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }                 // in RM, copied from the booking

    [Required, MaxLength(150)] public string Description { get; set; } = "";
    [Required, MaxLength(100)] public string PayerName { get; set; } = "";
    [Required, MaxLength(50)] public string PayerEmail { get; set; } = "";
    [Required, MaxLength(15)] public string PayerPhone { get; set; } = "";
    [Required, MaxLength(50)] public string BankPrefix { get; set; } = "";
    [Required, MaxLength(30)] public string PaymentMethod { get; set; } = "";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    // filled by callback
    [MaxLength(100)] public string? ReferenceCode { get; set; }   // Herepay's HP-INVAPI-xxx
    [MaxLength(100)] public string? TransactionId { get; set; }
    [MaxLength(100)] public string? BankName { get; set; }
    [MaxLength(30)] public string? FpxType { get; set; }
    [MaxLength(500)] public string? StatusMessage { get; set; }

    public DateTime CreatedAt { get; set; }             // UTC, local only (not sent to Herepay)
    public DateTime? PaidAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum PaymentStatus
{
    Pending = 0, Paid = 1, Failed = 2
}
