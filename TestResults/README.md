# Last-slot race test results (2026-11-18)

**Question:** What happens when a date has 1 slot left (19 of 20 bookings paid) and two payments happen at the same time?

**Answer:** The date ends up overbooked. The API never refuses the second customer.

| Scenario | Result | Details |
|---|---|---|
| Sanity: second booking sent after the first finishes | ✅ second gets 409 | the limit works when requests are not concurrent |
| S1: two bookings created at the same moment | ❌ 20/20 runs reach 21 bookings | [S1](LastSlotRace/S1-concurrent-booking-creation.md) |
| S2: two unpaid bookings, both paid at the same moment | ❌ 20/20 runs reach 21 paid; availability shows `Booked 21, Remaining 0` | [S2](LastSlotRace/S2-two-bookings-pay-concurrently.md) |
| S3: full flow (book → pay → callback) for both customers in parallel | ❌ 19/20 runs reach 21 paid | [S3](LastSlotRace/S3-end-to-end.md) |
| S4: one booking paid twice at once (double-click) | ❌ 20/20 runs record 2 paid payments, so the customer is charged twice | [S4](LastSlotRace/S4-same-booking-paid-twice.md) |

## Causes
1. `TempahanRamadanService.EnsureRulesAsync` counts the bookings and then inserts, without a lock or transaction. Two requests can both see 19 and both insert.
2. `PaymentService.CreateAsync` and `HandleCallbackAsync` never check how many slots are left on the date, so any booking that got in will be marked Paid.
3. `PaymentService.CreateAsync` only rejects a booking that is already **Paid**. It lets several **Pending** payments exist for the same booking (S4).
4. `GetAvailabilityAsync` clamps `Remaining` at 0, which hides the overbooking (`Booked` shows 21).

## How to rerun
Docker must be running.
```
dotnet test MAQ_API.Tests --filter Category=Integration --logger "trx;LogFileName=LastSlotRace.trx" --results-directory TestResults
```
Each scenario rewrites its file in `TestResults/LastSlotRace/`. `LastSlotRace.trx` is the raw xUnit result.
