using System.ComponentModel.DataAnnotations;
using MAQ_API.Models;

namespace MAQ_API.Dtos;

public class ManualIncomeRequest
{
    [Range(0.01, 9_999_999.99)] public decimal Amount { get; set; }
    public FinanceCategory Category { get; set; }
    [Required, MaxLength(200)] public string Description { get; set; } = "";
}

public record FinancialTransactionResponse(
    int Id, decimal Amount, string Category, string Source, int? PaymentId, string Description, DateTime CreatedAt);

public record BalanceResponse(decimal Balance, DateTime UpdatedAt);
