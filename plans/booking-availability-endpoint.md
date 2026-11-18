# Plan: Remaining-bookings-per-date endpoint

## Context
Frontend needs to know how many slots are left on each date. Remaining = `MaxBookingsPerDay` (20, currently a private const in `TempahanRamadanService`) minus bookings made that day whose payment is approved.

"Booking made that day" = a `TempahanRamadan` row with that `BookedDate` that has a `Payment` (`Payment.TempahanRamadanId`) with a successful status.

## Open points (confirm before implementing)
1. **"Approved" status**: `PaymentStatus` only has `Pending, Paid, Failed`. There is no `Approved`. Assumed `Paid` (set by the Herepay callback). If you want a new enum value, it needs a model change (the column is stored as a string, so no migration for the enum itself).
2. **Consistency with the existing cap**: `EnsureRulesAsync` currently counts *all* bookings (paid or not) toward the 20/day limit. If this endpoint only counts Paid, a date can show slots left but POST returns 409 (unpaid bookings fill the cap). Options: (a) leave as is, (b) change `EnsureRulesAsync` to use the same paid-only count (risk: overbooking while payments are pending). Recommend (a) for now and flagging it; decide later.
3. Count each booking once even if it has several Payment rows (e.g. a failed attempt then a paid one) — use `Any`, not a join+count.

## Steps

1. **Share the limit** — `Services/TempahanRamadanService.cs`
   - Change `private const int MaxBookingsPerDay = 20` to `public const int MaxBookingsPerDay = 20` (or move to a small `BookingRules` static class) so DTO/service use one value.

2. **DTO** — `Dtos/DateAvailabilityResponse.cs`
   ```csharp
   public record DateAvailabilityResponse(DateOnly Date, int MaxBookings, int Booked, int Remaining);
   ```

3. **Interface** — `Services/Interfaces/ITempahanRamadanService.cs`
   - `Task<List<DateAvailabilityResponse>> GetAvailabilityAsync(DateOnly? from, DateOnly? to);`

4. **Service** — `Services/TempahanRamadanService.cs`
   ```csharp
   var q = db.TempahanRamadans.AsNoTracking()
       .Where(t => db.Payments.Any(p => p.TempahanRamadanId == t.TempahanRamadanId
                                        && p.Status == PaymentStatus.Paid));
   if (from is not null) q = q.Where(t => t.BookedDate >= from);
   if (to   is not null) q = q.Where(t => t.BookedDate <= to);

   var counts = await q.GroupBy(t => t.BookedDate)
       .Select(g => new { Date = g.Key, Booked = g.Count() })
       .ToListAsync();
   ```
   - Map to `DateAvailabilityResponse` with `Remaining = Math.Max(0, Max - Booked)`, ordered by date.
   - Dates with zero paid bookings are absent from the query. Two options: return only dates with bookings (frontend treats missing as 20 left), or, when both `from` and `to` are given, fill every date in the range with `Booked = 0`. Recommend the fill when a range is supplied, so the frontend gets a complete calendar (Ramadan range is ~30 days).
   - Cap range at e.g. 366 days to avoid abuse; return 400 if `from > to` or too wide.

5. **Controller** — `Controllers/TempahanRamadanController.cs`
   ```csharp
   [HttpGet("availability")]
   public async Task<ActionResult<List<DateAvailabilityResponse>>> GetAvailability(
       [FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
       Ok(await service.GetAvailabilityAsync(from, to));
   ```
   - Literal route `availability` does not clash with `{id:int}` because of the int constraint.
   - Optionally also support a single date: `GET api/TempahanRamadan/availability/{date}`. Probably unnecessary; the range with `from == to` covers it.

6. **Index** (optional, only if slow) — `Payments(TempahanRamadanId, Status)` composite index in `AppDbContext`, plus migration. Data volume is small, so skip unless needed.

7. **Tests** — `MAQ_API.Tests/` (project exists, currently untracked)
   - Paid booking counts, Pending/Failed does not.
   - Booking with two payments (Failed + Paid) counts once.
   - Full day returns `Remaining = 0`, never negative.
   - Range fill returns empty days with 20.

## Verification
- `dotnet build`, `dotnet test`.
- Run API, `GET /api/TempahanRamadan/availability?from=2027-02-18&to=2027-03-19`; check numbers against the DB by hand for one date.
