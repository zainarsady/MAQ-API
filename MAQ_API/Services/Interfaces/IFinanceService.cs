using MAQ_API.Dtos;
using MAQ_API.Models;

namespace MAQ_API.Services;

public interface IFinanceService
{
    /// <summary>Adds a ledger row and increases the balance. Joins the caller's transaction if one is open.</summary>
    Task<FinancialTransactionResponse> RecordIncomeAsync(
        decimal amount, FinanceCategory category, FinanceSource source, string description,
        int? paymentId = null, CancellationToken ct = default);

    Task<BalanceResponse> GetBalanceAsync(CancellationToken ct = default);

    Task<List<FinancialTransactionResponse>> ListTransactionsAsync(
        DateOnly? from, DateOnly? to, FinanceCategory? category, CancellationToken ct = default);
}
