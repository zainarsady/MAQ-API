# Company finance ledger with a stored balance

## Context
We want one place that tracks the company's money. Every Herepay payment that succeeds should add to it automatically, for example +RM300. Admins also need to add income received outside the platform, such as cash or a direct transfer.

Choices you made:
- Use a ledger table plus a stored balance.
- Record income only, with a category on each entry.
- Admins can read the current balance and a list of transactions.
- Authentication will be handled later.

## Design

### New entities (`Models/`)
- **`FinancialTransaction`**
  - `Id`
  - `Amount` (`decimal(10,2)`, must be > 0)
  - `Category` (enum stored as a string: `TempahanRamadan`, `Donation`, `Event`, `Other`)
  - `Source` (enum: `Herepay`, `Manual`)
  - `PaymentId` (`int?`, foreign key to `Payment`, with a **unique index** so one payment is never counted twice)
  - `Description` (max 200)
  - `CreatedAt` (UTC)
- **`CompanyFinance`**
  - A single row with `Id = 1`, created through `HasData`
  - `Balance` (`decimal(12,2)`)
  - `UpdatedAt`

### Keeping the balance correct
Every new entry goes through one method, `FinanceService.RecordIncomeAsync(...)`. Inside a single database transaction it:
1. Adds the `FinancialTransaction` row.
2. Increases the balance with an atomic SQL update (`ExecuteUpdateAsync(s => s.SetProperty(f => f.Balance, f => f.Balance + amount))`). This avoids the race where two requests both read the old balance and one write is lost.
3. Commits.

For Herepay payments, the caller saves the change to the `Payment` row and the ledger row in the **same transaction**. If the callback and the reconcile path both mark the same payment as Paid at the same moment, the unique index on `PaymentId` rejects the second insert. We catch that `DbUpdateException` and treat it as already recorded.

### Changes to `Services/PaymentService.cs`
There are two places where a payment becomes `Paid`. Both should call the ledger:
- In the callback handler (around line 106), at the point where `newStatus == Paid`.
- In `ReconcileAsync` (around line 143).

Each place opens a transaction with `db.Database.BeginTransactionAsync`, sets the payment fields, calls `finance.RecordIncomeAsync(..., paymentId, Category.TempahanRamadan, Source.Herepay)`, saves, and commits.

`RecordIncomeAsync` will take the shared `AppDbContext`, which is scoped, and it will not commit on its own when a transaction is already open. The simplest way to do that is for `RecordIncomeAsync` to start a transaction only when `db.Database.CurrentTransaction` is null.

### New files
- `Services/Interfaces/IFinanceService.cs` and `Services/FinanceService.cs`. The service has three methods:
  - `RecordIncomeAsync`
  - `GetBalanceAsync`
  - `ListTransactionsAsync(from?, to?, category?)`
- `Dtos/FinanceDtos.cs`:
  - `ManualIncomeRequest` (Amount with `[Range(0.01, …)]`, Category, Description with `[Required]`)
  - `FinancialTransactionResponse`
  - `BalanceResponse`
- `Controllers/FinanceController.cs`, with routes under `api/finance`:
  - `GET  /balance` returns `{ balance, updatedAt }`
  - `GET  /transactions?from=&to=&category=`, newest first. `from`/`to` are dates and default to the current month.
  - `POST /transactions` adds manual income, with `Source = Manual`
- Register the service in `Program.cs` with `AddScoped<IFinanceService, FinanceService>()`, next to the other services.
- Add `DbSet`s and `OnModelCreating` configuration in `Data/AppDbContext.cs`. Follow the existing `Payment` block style: enums use `HasConversion<string>().HasMaxLength(20)`.
- Add an EF migration with `dotnet ef migrations add AddFinanceLedger`. Migrations are applied automatically at startup (`Program.cs:52`).

### Not included in this work
- Authentication. Until you add it, **`POST /api/finance/transactions` and the read endpoints are public**, so don't deploy them to production before auth is in place.
- Expenses, reversals, and backfilling existing Paid payments.

## Verification
1. Run `dotnet build` and apply the migration to the local MySQL database on port 3307. Check that `CompanyFinances` has one row with a balance of 0.
2. Call `POST /api/finance/transactions` with `{amount: 300, category: "Donation", description: "Cash"}`. The balance should become 300 and the list should show the entry.
3. Run an end-to-end payment on UAT, or send a signed callback to `/api/payments/callback`. The balance should go up by the booking amount. Send the same callback again and confirm the balance doesn't change, because the call is idempotent.
4. Add a concurrency test in `MAQ_API.Tests/Concurrency/` that runs the existing harness pattern. It should fire 20 manual inserts in parallel and assert that balance equals SUM(transactions). A second case should run the callback and reconcile for the same payment at the same time and assert that exactly one ledger row is created.
