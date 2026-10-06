# Plan: Catering Booking API (MVC controllers + Pomelo MySQL)

## Context
`MAQ_API` is a fresh .NET 8 minimal-API template (only the WeatherForecast sample in `MAQ_API/Program.cs`). We need a controller-based (MVC-style) API whose main controller is `TempahanRamadanController` and inside there will be PostTempahanRamadan, GetTempahanRamadan, GetTempahanRamadanById, UpdateTempahanRamadan, DeleteTempahanRamadan, a booking system backed by MySQL via Pomelo EF Core. No users/auth yet. Business rule: ** - no two booking with the same IC number on the same day - only 20 booking per day **. Apply SOLID pragmatically — controller → service (interface) → DbContext — without a repository layer or other ceremony (EF's DbContext already is the repository/unit of work).

## Assumptions (change at approval if wrong)
- "Dummy data" = placeholder credentials in the connection string **plus** 2 seeded bookings via `HasData`.
- TempahanRamadan fields: `TempahanRamadanId`, `BookedDate` (DateOnly),  `CustomerName`, `ContactNumber`, `ICNumber`, `Age`, `Address`, `Amount` (money, RM), `CreatedAt`. Customer is just a string field, not a user entity.
- **Note:** `BookedDate` is *not* unique on its own (that would cap a day at 1 booking, contradicting the 20/day rule). Uniqueness is the composite `(ICNumber, BookedDate)`; the 20/day cap is enforced in the service.

## Steps

1. **Packages** (in `MAQ_API/`)
   - `dotnet add package Pomelo.EntityFrameworkCore.MySql --version 8.0.*` (latest 8.x, matches net8.0/EF Core 8)
   - `dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.*` (for migrations)
   - `dotnet tool install --global dotnet-ef` if not already installed

2. **Connection string** — `MAQ_API/appsettings.json`
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=localhost;Port=3306;Database=maq_catering;User=root;Password=P@ssw0rd123;"
   }
   ```

3. **Entity** — `MAQ_API/Models/TempahanRamadan.cs`
   - `[Key] int TempahanRamadanId`, `DateOnly BookedDate`, `[Required, MaxLength(100)] string CustomerName`, `[Required, MaxLength(20)] string ContactNumber`, `[Required, MaxLength(20)] string ICNumber`, `int Age` (`[Range(1, 120)]`), `[Required, MaxLength(200)] string Address`, `decimal Amount` (`[Column(TypeName = "decimal(10,2)")]`, RM, `[Range(0, double.MaxValue)]`), `DateTime CreatedAt`.

4. **DTO** — `MAQ_API/Dtos/TempahanRamadanRequest.cs`
   - Record used for POST/PUT with data annotations (no `TempahanRamadanId`/`CreatedAt`), so clients can't set server-owned fields.

5. **DbContext** — `MAQ_API/Data/AppDbContext.cs`
   - `DbSet<TempahanRamadan> TempahanRamadans`
   - `OnModelCreating`: `HasIndex(t => new { t.ICNumber, t.BookedDate }).IsUnique()` (DB-level guarantee of the one-booking-per-IC-per-day rule) + non-unique `HasIndex(t => t.BookedDate)` for the daily count + `HasData(...)` with 2 dummy bookings (fixed `CreatedAt` values so migrations stay deterministic).

6. **Service** (SRP + DIP)
   - `MAQ_API/Services/ITempahanRamadanService.cs`: `GetAllAsync`, `GetByIdAsync`, `CreateAsync(TempahanRamadanRequest)`, `UpdateAsync(int, TempahanRamadanRequest)`, `DeleteAsync(int)`.
   - `MAQ_API/Services/TempahanRamadanService.cs`: owns the business rules — before create/update:
     - same-IC check: `AnyAsync(t => t.ICNumber == ic && t.BookedDate == date && t.TempahanRamadanId != id)`; if true, throw `DuplicateBookingException`.
     - daily cap: `CountAsync(t => t.BookedDate == date && t.TempahanRamadanId != id) >= 20`; if true, throw `DailyLimitReachedException` (limit as a `const int MaxBookingsPerDay = 20`).
     - Returns `null`/`false` for not-found.
   - `MAQ_API/Exceptions/DuplicateBookingException.cs` and `DailyLimitReachedException.cs`: tiny custom exceptions.
   - Also catch `DbUpdateException` from the unique index (race between check and insert) and rethrow as `DuplicateBookingException`. The 20/day count can race under concurrent inserts; accepted for now (could wrap in a serializable transaction later).

7. **Controller** — `MAQ_API/Controllers/TempahanRamadanController.cs`
   - `[ApiController] [Route("api/[controller]")]`, injects `ITempahanRamadanService` only (thin, no EF code).
   - `GetTempahanRamadan` — `GET api/tempahanramadan` → 200 list
   - `GetTempahanRamadanById` — `GET api/tempahanramadan/{id}` → 200 / 404
   - `PostTempahanRamadan` — `POST api/tempahanramadan` → 201 `CreatedAtAction` / 409 on duplicate IC or full day
   - `UpdateTempahanRamadan` — `PUT api/tempahanramadan/{id}` → 200 / 404 / 409
   - `DeleteTempahanRamadan` — `DELETE api/tempahanramadan/{id}` → 204 / 404
   - `[ApiController]` gives automatic 400 for invalid DTOs.

8. **Program.cs** — replace WeatherForecast sample:
   - `builder.Services.AddControllers();`
   - `builder.Services.AddDbContext<AppDbContext>(o => o.UseMySql(connStr, new MySqlServerVersion(new Version(8, 0, 36))));` — explicit version instead of `ServerVersion.AutoDetect` so the app starts/migrations build without a live DB.
   - `builder.Services.AddScoped<ITempahanRamadanService, TempahanRamadanService>();`
   - Keep Swagger; add `app.MapControllers();`; remove `summaries`, `/weatherforecast`, and `WeatherForecast` record.
   - Update `MAQ_API/MAQ_API.http` to hit `/api/tempahanramadan` instead of weatherforecast.

9. **Migration**
   - `dotnet ef migrations add InitialCreate` → creates `MAQ_API/Migrations/`.

## Out of scope (deliberately)
Repository pattern, AutoMapper, generic base services, auth/users, pagination, adding MySQL to `compose.yaml`.

## Verification
1. `dotnet build` succeeds.
2. With a local MySQL running on the dummy credentials: `dotnet ef database update` → `maq_catering` DB with `TempahanRamadans` table, unique composite index on `(ICNumber, BookedDate)`, 2 seed rows.
3. `dotnet run`, open Swagger:
   - `GET /api/tempahanramadan` returns the 2 seeded bookings.
   - `POST` a booking with a new IC on a date → 201.
   - `POST` another booking with a *different* IC on the same date → 201 (multiple per day allowed).
   - `POST` again with the *same* IC on the same date → 409.
   - Fill a date to 20 bookings (different ICs), then `POST` a 21st → 409.
   - `PUT` a booking to a date where its IC already has a booking → 409; to a full date → 409; keeping its own date/IC → 200.
   - `POST` with `age: 0` or missing `customerName` → 400.
   - `DELETE` → 204, then `GET {id}` → 404.
