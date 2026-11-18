# ADR 0001: Herepay payment gateway integration

- Status: Accepted (implemented, not yet verified against Herepay UAT)
- Date: 2026-11-12
- Plan: `plans/herepay-payment-gateway.md`

## Decisions from the up-front questions

| # | Question | Choice | Consequence |
|---|---|---|---|
| 1 | What is a payment tied to? | **`TempahanRamadan` booking** | `Payment.TempahanRamadanId` nullable FK (`ON DELETE SET NULL`, keeps payment history). The client sends only the booking id; **amount, payer name, phone and description are derived server-side** from the booking, so the amount cannot be tampered with. Email, bank prefix and payment method come from the client (the booking has no email). |
| 2 | Tests | **Checksum tests only** | New `MAQ_API.Tests` (xUnit, 6 tests incl. the docs' initiate and callback sample strings). No `PaymentService` tests (no SQLite/mock harness). |
| 3 | Initiate output | **JSON `{ paymentCode, formHtml }`** | Frontend renders with `document.open(); document.write(formHtml); document.close();`. |
| 4 | Config | **Placeholders + env vars** | `appsettings.json` holds UAT `BaseUrl` and placeholder https Callback/Redirect URLs (`api.example.com` must be replaced). Keys come from user-secrets/env: `compose.yaml` maps `HEREPAY_*` variables to `Herepay__*`. `ValidateOnStart` means **the app will not boot until all five values are set**. |

## Implementation choices (mine, beyond the plan's text)

1. **Inactive-bank filtering lives in `HerepayClient`, not `PaymentService`** (the plan said service). It keeps `PaymentBank` free of an `Active` field and the service trivial. Behaviour is identical.
2. **`ChecksumGenerator`** rewritten as planned (flat strings, ordinal sort, `checksum` skipped, `FixedTimeEquals`). Added public `BuildPayload` so tests can assert the joined string. The docs give no expected hash, so only the payload is asserted.
3. **Payment code**: `MAQ-{UTC yyyyMMddHHmmss}-{6 chars}` using `RandomNumberGenerator` and an unambiguous alphabet. Unique index backs it.
4. **Timestamps are UTC** (`CreatedAt`, `PaidAt`, `UpdatedAt`), matching `TempahanRamadanService`. The plan's "Malaysia time" comment was dropped because `created_at` is not sent to Herepay.
5. **`Payment` status enum only** (no raw `StatusCode` column), stored as a string; `[MaxLength]` attributes added. `Payment.cs` was kept from the WIP.
6. **Phone** is the booking's contact number with non-digits stripped (Herepay needs digits, booking allows free text up to 20 chars). Not truncated; Herepay will 422 if too long and it surfaces as 400.
7. **Error mapping**: `HerepayException.IsClientError` (Herepay 400/422) → API 400 with Herepay's field messages; 401/network/other → 502 with a generic message (details only in logs, no keys or checksums logged). Unknown booking → 404; already-paid or zero-amount booking → 409.
8. **Duplicate-payment guard**: a booking with a `Paid` payment cannot start another. Multiple `Pending`/`Failed` attempts are allowed (user may retry with another bank).
9. **Callback** binds `Request.Form` into a dictionary so the checksum covers every field. Replies `200 OK` text on success, unknown code, amount mismatch and unconfirmed Paid (retries cannot help / reconciliation handles it); `401` only on bad checksum.
10. **Backstop + reconciliation** as planned: `Paid` requires Herepay's get-transaction to confirm status `"00"` and the same amount. `GET api/payments/{code}` reconciles a `Pending` payment older than 3 minutes (fixed constant, no background job). `Failed` is also applied by reconciliation.
11. **HttpClient timeout 4 s** so a callback that makes the backstop call stays under Herepay's ~5 s window. A timeout during the backstop leaves the payment `Pending`.
12. **Single migration `AddPayments`** generated with `dotnet ef`; auto-applied on start by existing `Migrate()`.
13. **Controller route** is fixed `api/payments` (not `[controller]` convention) as in the plan. No auth or rate limiting (out of scope per plan).

## Deliberately not done

- **20/day booking cap counting only `Paid` bookings.** The plan suggested it when tied to a booking, but bookings are currently created before and independently of payment, and existing rows have no payment. Changing the cap would alter existing behaviour; needs a product decision (e.g. a booking `PaymentStatus`).
- `PaymentService` / `HerepayClient` unit tests, UAT verification (steps 10), rate limiting, background reconciliation.
- `UseHttpsRedirection` left as is; behind a TLS-terminating proxy confirm it does not bounce the callback.

## Open items for the owner

1. Set real `Herepay:CallbackUrl` / `RedirectUrl` (public HTTPS) and the three secrets (`dotnet user-secrets` locally; `HEREPAY_*` in the deployment env).
2. Run the UAT checklist in the plan (step 10); in particular confirm the callback checksum scheme.
3. Confirm with Herepay: max field lengths (we enforce email 50, phone 15 via the model) and callback retry behaviour.
4. Decide on the cap-counting question above.
5. Note: `compose.yaml` already had uncommitted WIP (a `mysql` service); this work only added the `environment` block to `maq_api`.

## Files

New: `Options/HerepayOptions.cs`, `Dtos/{CreatePaymentRequest,PaymentDtos}.cs`, `Services/{IHerepayClient,HerepayClient,IPaymentService,PaymentService}.cs`, `Exceptions/{HerepayException,InvalidChecksumException,BookingNotFoundException,PaymentNotAllowedException}.cs`, `Controllers/PaymentsController.cs`, `Migrations/*_AddPayments*`, `MAQ_API.Tests/`.
Modified: `Helpers/ChecksumGenerator.cs`, `Models/Payment.cs`, `Data/AppDbContext.cs`, `Program.cs`, `appsettings.json`, `compose.yaml`, `MAQ_API.sln`.
