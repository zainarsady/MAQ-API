# Plan: Herepay Payment Gateway Integration

Sources: your pasted notes **and** the official Herepay reference (https://herepay.readme.io: Initiate Payment, Callback, Get Transaction by Payment Code, Get Payment Channels, Generating Checksum, Status Codes). Where they disagree, the official reference wins. See "Corrections vs. the pasted notes" at the bottom.

## Context
MAQ_API (.NET 8, MVC controllers, Pomelo MySQL, controller → service interface → `AppDbContext`, no repository layer) has no payment support. We integrate Herepay FPX: list channels → initiate → webhook callback (source of truth) → frontend redirect page that reads status from our DB. Follows the conventions of `plans/catering-booking-api.md` and the existing Events/Registrations code (DTO records, custom exceptions mapped to HTTP codes in the controller).

Uncommitted WIP already in the tree: `Models/Payment.cs` (entity + `PaymentStatus`) and `Helpers/ChecksumGenerator.cs`.

## Herepay facts this plan relies on
- Base URLs: UAT `https://uat.herepay.org`, Production `https://app.herepay.org`. API keys are per environment.
- Auth headers on every call: `SecretKey`, `XApiKey`. The Private Key is **never sent**, only used for the checksum.
- Channels: `GET /api/v1/herepay/payment/channels`. Response `data[]` = `{ payment_method, charges{currency,fixed,percentage}, channels[]{ prefix, name, img_url, active } }`. Bank code field is **`prefix`** (passed as `bank_prefix` on initiate). Hide `active != "Active"`. Fetch fresh each time (banks go offline).
- Initiate: `POST /api/v1/herepay/initiate`, **`application/x-www-form-urlencoded`**. Required: `name, email, phone, amount, description, payment_code, payment_method, bank_prefix, checksum`. Optional: `callback_url`, `redirect_url` (must start with `https://`; if sent they are **part of the checksum**; if omitted, the URLs saved in the Herepay dashboard Settings are used).
  - 200 → **HTML page** with an auto-submitting form to the FPX gateway (not JSON, not a 3xx). 401 `{status_code:"41"}` bad keys/checksum. 422 `{errors:{field:[msg]}}` validation. 400 payment couldn't be created.
- Callback: server-to-server `POST` (urlencoded) to our callback URL. Fields: `payment_code, reference_code, status, status_code, amount, currency, bank_name, transaction_id, message, payment_method, fpx_type, checksum`. Same checksum scheme. Must reply **HTTP 200, body `OK`**, within ~**5 s**. Duplicates can arrive → idempotent. No retry schedule documented.
- Status query: `GET /api/v1/herepay/transactions/{payment_code}` (same two headers, no checksum). Returns `data.status`, `data.status_code`, `data.amount`, `data.reference_code`, `data.transaction_id`, `data.message`, … 404 if unknown. If a `payment_code` is reused, returns the most recent one (so ours must be unique).
- Status codes are **strings**: `"00"` Success, `"29"` Pending, `"30"` Failed, `"41"` Unauthorized. Store/compare as strings (`"00"` ≠ `0`). `transaction_id` also as string.
- Checksum: drop `checksum`, sort fields by name (ordinal), join values with `,`, HMAC-SHA256 with the Private Key, lowercase hex. Sign the exact strings sent (`amount` = `"10.00"`, `phone` as text to keep the leading zero).
- Docs state **no max field lengths**. The limits in your notes (email 50, phone 15, description 150) are kept as conservative validation; confirm with Herepay.

## Design (SOLID, not overdone)

| Piece | Responsibility | Why separate |
|---|---|---|
| `IHerepayClient` / `HerepayClient` | HTTP to Herepay only: channels, initiate, get-transaction. Typed `HttpClient`. | External I/O isolated and mockable (DIP). |
| `ChecksumGenerator` (static) | Pure `Generate` / `Verify`. | Trivial to unit-test against the docs' sample. |
| `IPaymentService` / `PaymentService` | Rules: create pending payment, process callback idempotently, read status. Uses `AppDbContext` + `IHerepayClient`. | Thin controller, matches existing services. |

No repository, strategy, factory or MediatR.

## Steps

### 1. Configuration
- `Options/HerepayOptions.cs`: `BaseUrl`, `SecretKey`, `ApiKey`, `PrivateKey`, `CallbackUrl`, `RedirectUrl`.
- `appsettings.json`: `Herepay:BaseUrl` (UAT), `CallbackUrl`, `RedirectUrl`. **No secrets.** `SecretKey/ApiKey/PrivateKey` via `dotnet user-secrets` locally and env vars (`Herepay__PrivateKey`, …) in `compose.yaml`/deployment. `AddOptions<HerepayOptions>().Bind(...).ValidateOnStart()`.
- We **send `callback_url` and `redirect_url` on every initiate** (from options) rather than relying on dashboard Settings, so environments can't drift. Both go into the checksum.
- Callback URL must be public HTTPS (tunnel for local UAT). Make sure `UseHttpsRedirection` doesn't bounce it.

### 2. Checksum helper: `Helpers/ChecksumGenerator.cs` (fix existing)
The existing class matches Herepay's official sample in spirit but **doesn't compile** (uses `JsonConvert` with no Newtonsoft reference, missing usings, culture-aware sort, doesn't skip `checksum`). All Herepay fields are flat strings, so drop the nested-JSON branch.
```csharp
public static class ChecksumGenerator
{
    public static string Generate(IReadOnlyDictionary<string,string> data, string privateKey);
    // exclude "checksum", OrderBy(Key, StringComparer.Ordinal), string.Join(",", values),
    // HMACSHA256(UTF8 key) over UTF8 payload, Convert.ToHexString(...).ToLowerInvariant()

    public static bool Verify(IReadOnlyDictionary<string,string> data, string? received, string privateKey);
    // CryptographicOperations.FixedTimeEquals on the two lowercase hex strings' bytes
}
```
Unit test with the docs' example: initiate values `10.00,ABB0233,Order #1001,ali@example.com,Ali bin Abu,ORDER-1001,Online Banking,0123456789` and the callback example `10.00,Affin Bank,MYR,FPX,Approved,ORDER-1001,FPX,HP-INVAPI-6720F1A2B3C4D,Success,00,2410281530450123` (assert the joined string; the docs give no expected hash).

### 3. Entity and DbContext
- `Models/Payment.cs`: keep. Changes: add `[MaxLength]`/`[Column]` attributes; add `StatusCode` (string, e.g. `"00"`) only if we want to keep Herepay's raw code, otherwise the enum is enough (keep it simple: enum only); update the `CreatedAt` comment (it is **not** sent to Herepay, see corrections).
- `AppDbContext`: `DbSet<Payment> Payments`; unique index on `PaymentCode`; `Status` `HasConversion<string>().HasMaxLength(20)`; index on `ReferenceCode`; `Amount` `decimal(10,2)`.
- Migration `AddPayments` (Program.cs already auto-migrates).

### 4. DTOs (`Dtos/`)
- `CreatePaymentRequest`: `Amount` (>0, 2 dp), `Name`, `Email` (≤50), `Phone` (string, ≤15, digits), `Description` (≤150), `BankPrefix`, `PaymentMethod` (`Online Banking` | `Corporate Banking`). Annotations → automatic 400.
- `PaymentChannelResponse`: mirrors channels (`PaymentMethod`, `Charges`, `Channels[]{Prefix, Name, ImgUrl}`), already filtered to active banks.
- `InitiatePaymentResponse`: `PaymentCode`, `FormHtml` (the auto-submit HTML from Herepay).
- `PaymentStatusResponse`: `PaymentCode`, `Status`, `Amount`, `PaidAt`. No PII.
- Callback is bound as `Request.Form` → `Dictionary<string,string>` (not a fixed class) so the checksum covers **every** field Herepay sends, including ones we don't model.

### 5. Herepay client: `Services/IHerepayClient.cs`, `HerepayClient.cs`
- `AddHttpClient<IHerepayClient, HerepayClient>()`; `BaseAddress` from options; default headers `SecretKey`, `XApiKey`. No redirect handling needed (initiate returns 200 HTML), so default `HttpClient` settings are fine.
- `GetChannelsAsync()` → deserialize, return channels.
- `InitiateAsync(Payment)` → build string dictionary `{name,email,phone,amount("0.00" invariant),description,payment_code,payment_method,bank_prefix,callback_url,redirect_url}`, add `checksum`, send `FormUrlEncodedContent`. 200 → return HTML body. 401/422/400 → throw `HerepayException` carrying Herepay's message (422 field errors are surfaced to the caller as 400 ProblemDetails; 401 means our keys/checksum are wrong → 502 and log).
- `GetTransactionAsync(paymentCode)` → returns `{Status, StatusCode, Amount}` or `null` on 404.
- Never log keys or full checksums.

### 6. Payment service: `Services/IPaymentService.cs`, `PaymentService.cs`
- `GetChannelsAsync()` → client, filter `Active`.
- `CreateAsync(CreatePaymentRequest)`:
  1. Generate unique `PaymentCode` server-side (e.g. `MAQ-{yyyyMMddHHmmss}-{6 random chars}`).
  2. Insert `Pending`, save (so the callback can always find it).
  3. `client.InitiateAsync`. On `HerepayException`: mark `Failed` + `StatusMessage`, rethrow.
  4. Return `InitiatePaymentResponse`.
- `HandleCallbackAsync(Dictionary<string,string> fields)`:
  1. `ChecksumGenerator.Verify` → else throw `InvalidChecksumException` (controller → 401 `Invalid checksum`, as in the docs' example).
  2. Find by `payment_code`; not found → log, still reply 200 `OK` (retries can't help).
  3. Already `Paid` → return (idempotent; never downgrade).
  4. `amount` must equal stored amount (string-compared as `0.00`) else log and leave unchanged.
  5. Map `status_code`: `"00"`→`Paid` (+`PaidAt`), `"30"`→`Failed`, `"29"`→stay `Pending`. Store `ReferenceCode`, `TransactionId`, `BankName`, `FpxType`, `StatusMessage` (`message`), `UpdatedAt`.
  6. **Backstop:** before setting `Paid`, call `GetTransactionAsync(paymentCode)` and require `status_code == "00"` and matching amount. (Costs one HTTP call inside the 5 s window; fine. If it throws/times out, leave `Pending` and rely on reconciliation below.)
- `GetStatusAsync(paymentCode)`: reads the DB; if the row is still `Pending` and older than a few minutes, **reconcile once** via `GetTransactionAsync` (covers a lost callback, as the docs recommend). No background job for now.

### 7. Controller: `Controllers/PaymentsController.cs`
`[ApiController] [Route("api/payments")]`, injects `IPaymentService` only.
- `GET api/payments/channels` → 200.
- `POST api/payments` → 200 `InitiatePaymentResponse` / 400 (Herepay 422 detail) / 502 (Herepay unreachable or 401).
- `POST api/payments/callback` → `[Consumes("application/x-www-form-urlencoded")]`, anonymous; 200 with plain text `OK` / 401 on bad checksum. Keep it fast (< 5 s).
- `GET api/payments/{paymentCode}` → 200 status / 404.
- The redirect page is a frontend (Astro) page at `RedirectUrl`; it reads `paymentCode` and polls `GET api/payments/{paymentCode}`. It never marks anything paid.

### 8. Frontend contract
Initiate returns HTML as a string. The frontend should render it with `document.open(); document.write(formHtml); document.close();` (assigning via `innerHTML` will not run the auto-submit script). Alternative if that's awkward: have `POST api/payments` return `text/html` and let the frontend submit a real top-level form POST. Choose when wiring the frontend.

### 9. Program.cs
Register options, typed client, `IPaymentService`. Optional later: rate-limit `POST api/payments` (anonymous).

### 10. UAT verification
1. Checksum unit tests (step 2) pass.
2. Channels returns active banks with `prefix`.
3. Initiate with a UAT bank → 200 HTML → paying in the FPX simulator.
4. Callback arrives, checksum verifies (confirms same scheme on callback; adjust `Verify` only if not), DB `Paid`, reply `OK`.
5. Failed (`30`) and pending (`29`) paths; replay same callback (no change); tamper one field (401); amount mismatch (unchanged).
6. Kill the callback (don't expose it) → `GET api/payments/{code}` reconciles via the status endpoint.
7. Re-check: bad payment/redirect URL (http://) → 422 surfaces as 400.

### 11. Tests (lightweight, xUnit)
`ChecksumGenerator` (ordering, `checksum` excluded, `"10"` vs `"10.00"`, tamper) and `PaymentService.HandleCallbackAsync` (idempotency, amount mismatch, no `Paid` downgrade, backstop mismatch) with SQLite/InMemory and a mocked `IHerepayClient`.

## Files touched
New: `Options/HerepayOptions.cs`, `Dtos/{CreatePaymentRequest,PaymentChannelResponse,InitiatePaymentResponse,PaymentStatusResponse}.cs`, `Services/{IHerepayClient,HerepayClient,IPaymentService,PaymentService}.cs`, `Exceptions/{HerepayException,InvalidChecksumException}.cs`, `Controllers/PaymentsController.cs`, migration `AddPayments`, test project.
Modified: `Helpers/ChecksumGenerator.cs`, `Models/Payment.cs`, `Data/AppDbContext.cs`, `Program.cs`, `appsettings.json`, `compose.yaml`.

## Corrections vs. the pasted notes (from the official docs)
| Pasted notes | Official reference | Effect on plan |
|---|---|---|
| Initiate is `multipart/form-data` | `application/x-www-form-urlencoded` | Use `FormUrlEncodedContent` |
| Send `created_at` | No `created_at` field exists | Dropped; not in checksum |
| Redirect or HTML? (unknown) | 200 HTML auto-submit form, no `AllowAutoRedirect=false` needed | Resolved; return `FormHtml` |
| `status_code` 0/1/2 | `"00"`/`"29"`/`"30"` strings | Map as strings |
| Channel field `bank_prefix` | `prefix` (sent as `bank_prefix`); filter `active` | DTO + filter |
| Callback checksum scheme unverified | Same scheme, constant-time compare | Resolved |
| Status query endpoint unknown | `GET /transactions/{payment_code}` exists | Backstop + reconciliation implemented |
| Callback reply "return 200" | 200 with body `OK`, ≤ ~5 s | Specified |
| URL config unclear | Optional per request (https only), included in checksum | Send both every time |
| Field length limits | Not documented | Keep conservative limits, confirm |

## Open questions
1. **What is being paid for?** Standalone, or tied to `TempahanRamadan` / `Registration`? If tied, add a nullable FK on `Payment`, compute the amount server-side, and count a booking toward the 20/day cap only once `Paid`.
2. Frontend redirect URL and public callback domain for UAT and production.
3. Confirm with Herepay: max field lengths, and callback retry behavior.

## Out of scope
Refunds, recurring payments, admin dashboards, auth on payment endpoints, background reconciliation job, settlement/fee reporting (`Get Transactions` list endpoint).
