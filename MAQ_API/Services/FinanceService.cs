using MAQ_API.Data;
using MAQ_API.Dtos;
using MAQ_API.Models;
using Microsoft.EntityFrameworkCore;

namespace MAQ_API.Services;

public class FinanceService(AppDbContext db) : IFinanceService
{
    public async Task<FinancialTransactionResponse> RecordIncomeAsync(
        decimal amount, FinanceCategory category, FinanceSource source, string description,
        int? paymentId = null, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

        var ownsTx = db.Database.CurrentTransaction is null;
        await using var tx = ownsTx ? await db.Database.BeginTransactionAsync(ct) : null;

        var now = DateTime.UtcNow;
        var row = new FinancialTransaction
        {
            Amount = amount, Category = category, Source = source,
            PaymentId = paymentId, Description = description, CreatedAt = now,
        };
        db.FinancialTransactions.Add(row);
        await db.SaveChangesAsync(ct);

        // atomic increment: concurrent requests cannot overwrite each other's balance
        await db.CompanyFinances.Where(f => f.Id == CompanyFinance.SingletonId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Balance, f => f.Balance + amount)
                .SetProperty(f => f.UpdatedAt, now), ct);

        if (tx is not null) await tx.CommitAsync(ct);
        return ToResponse(row);
    }

    public async Task<BalanceResponse> GetBalanceAsync(CancellationToken ct = default)
    {
        var f = await db.CompanyFinances.AsNoTracking().FirstAsync(x => x.Id == CompanyFinance.SingletonId, ct);
        return new BalanceResponse(f.Balance, f.UpdatedAt);
    }

    public async Task<List<FinancialTransactionResponse>> ListTransactionsAsync(
        DateOnly? from, DateOnly? to, FinanceCategory? category, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = (from ?? new DateOnly(today.Year, today.Month, 1)).ToDateTime(TimeOnly.MinValue);
        var end = (to ?? today).AddDays(1).ToDateTime(TimeOnly.MinValue); // inclusive end date

        var q = db.FinancialTransactions.AsNoTracking().Where(t => t.CreatedAt >= start && t.CreatedAt < end);
        if (category is not null) q = q.Where(t => t.Category == category);

        var rows = await q.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).ToListAsync(ct);
        return rows.Select(ToResponse).ToList();
    }

    private static FinancialTransactionResponse ToResponse(FinancialTransaction t) =>
        new(t.Id, t.Amount, t.Category.ToString(), t.Source.ToString(), t.PaymentId, t.Description, t.CreatedAt);
}
