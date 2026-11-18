using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MAQ_API.Models;

/// <summary>One permanent ledger row per money event (income only for now).</summary>
public class FinancialTransaction
{
    public int Id { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }                 // RM, always > 0

    public FinanceCategory Category { get; set; }
    public FinanceSource Source { get; set; }

    /// <summary>Set for Herepay income; unique so a payment is never counted twice.</summary>
    public int? PaymentId { get; set; }

    [Required, MaxLength(200)] public string Description { get; set; } = "";

    public DateTime CreatedAt { get; set; }             // UTC
}

public enum FinanceCategory { TempahanRamadan, Donation, Event, Other }

public enum FinanceSource { Herepay, Manual }
