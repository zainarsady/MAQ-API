# Plan: Frontend for Herepay payments (Astro)

Backend reference: `adr/0001-herepay-payment-gateway.md`, `plans/herepay-payment-gateway.md`.
Scope: a Ramadan catering booking is paid through Herepay FPX. The frontend picks a bank, starts the payment, hands the user to the bank, then shows the result.

All JSON is **camelCase** (ASP.NET default). Base path: `{API}/api/payments`. Browser origins allowed by CORS: `http://localhost:4321`, `https://www.masjidalquddus.my`. The frontend never calls Herepay directly and never holds Herepay keys.

## User flow

```
Booking form ──POST /api/TempahanRamadan──▶ booking {tempahanRamadanId, amount, ...}
      │
Payment step ──GET /api/payments/channels──▶ bank list
      │  user picks method + bank, enters email
      ├──POST /api/payments──▶ {paymentCode, formHtml}
      │  save paymentCode, then render formHtml  (auto-submits to FPX)
      ▼
Bank (FPX) ──▶ user pays ──▶ Herepay redirects browser to RedirectUrl
      ▼
Result page ──poll GET /api/payments/{paymentCode}──▶ Paid | Failed | Pending
```

The callback (`POST /api/payments/callback`) is **server to server only**. The frontend never calls it. The result page is only a display: the status comes from our DB, and the frontend must never decide that something is paid.

## 1. Endpoints and data shapes

### 1.1 List banks: `GET /api/payments/channels`
Response `200`, array (only banks that are currently active are returned):
```json
[
  {
    "paymentMethod": "Online Banking",
    "charges": { "currency": "MYR", "fixed": "1.00", "percentage": "0" },
    "channels": [
      { "prefix": "ABB0233", "name": "Affin Bank", "imgUrl": "https://..." }
    ]
  }
]
```
- `charges` values are strings. Show as an info line if desired; the charge is not added to our amount.
- `paymentMethod` is the value to send back on create (`"Online Banking"` or `"Corporate Banking"`).
- `prefix` is the value to send back as `bankPrefix`.
- Fetch fresh every time the payment step opens (banks go offline). Do not cache for long.
- Errors: `502` ProblemDetails, so show "Payment is temporarily unavailable, please try later" and let the user retry.

### 1.2 Start a payment: `POST /api/payments`
Request, `Content-Type: application/json`:
```json
{
  "tempahanRamadanId": 12,
  "email": "ali@example.com",
  "bankPrefix": "ABB0233",
  "paymentMethod": "Online Banking"
}
```
| Field | Rule |
|---|---|
| `tempahanRamadanId` | required int, the `tempahanRamadanId` returned when the booking was created |
| `email` | required, valid email, max 50 chars |
| `bankPrefix` | required, a `prefix` from the channels list, max 50 |
| `paymentMethod` | required, exactly `Online Banking` or `Corporate Banking` |

The amount, name, phone and description are **not sent**; the server takes them from the booking. Do not let the user edit an amount on the payment page. The amount is `slots × RM300` (1-20 slots, so RM300-RM6000); the booking has no quantity field, so show slots as `amount / 300` if needed. Display the booking's amount (already known from the booking response) read-only.

Response `200`:
```json
{ "paymentCode": "MAQ-20261112103000-K7P2QX", "formHtml": "<html>...auto-submitting form...</html>" }
```

Error responses (all `application/problem+json` unless noted):
| Status | Meaning | UI |
|---|---|---|
| 400 with `errors` object | Field validation failed (standard ASP.NET `ValidationProblemDetails`: `{ "errors": { "Email": ["..."] } }`) | Show messages next to fields |
| 400 with `detail` | Herepay rejected the data (e.g. phone/email format) | Show `detail` as a form-level message |
| 404 | Booking does not exist | "Booking not found", send user back to booking |
| 409 | Booking already paid, or has no amount | Show `detail`; if already paid, link to its status |
| 502 | Gateway unreachable/misconfigured | "Payment is temporarily unavailable", allow retry |

A failed start creates a `Failed` payment record server-side. Retrying is fine: each attempt gets a new `paymentCode`.

### 1.3 Payment status: `GET /api/payments/{paymentCode}`
Response `200`:
```json
{ "paymentCode": "MAQ-20261112103000-K7P2QX", "status": "Paid", "amount": 350.00, "paidAt": "2026-11-12T02:31:20Z" }
```
- `status` is one of `Pending`, `Paid`, `Failed` (strings).
- `paidAt` is UTC (ISO 8601) or `null`. Convert to Malaysia time (`Asia/Kuala_Lumpur`) for display.
- No personal data is returned. `404` means unknown code.
- Calling this on a payment still `Pending` after about 3 minutes makes the server re-check with Herepay, so polling also recovers from a lost callback.

## 2. Rendering the Herepay form (critical)

`formHtml` is a full HTML page containing a form that auto-submits to the FPX gateway via a script. **Assigning it with `innerHTML` will not run the script.** Do this instead, after saving the payment code (see 3):

```ts
document.open();
document.write(response.formHtml);
document.close();
```
This replaces the current page with the bank hand-off, which is expected. Do it only in direct response to the user's "Pay" click, and only once (disable the button after click to prevent double submit).

Alternative if `document.write` is blocked or awkward: open an empty same-tab document through `Blob`/`srcdoc`. Not recommended; use `document.write` first. Note: don't render `formHtml` anywhere that is visible as content (it is trusted server-originated gateway HTML, but never concatenate user input into it).

## 3. Remembering the payment across the redirect
The user leaves the site and comes back at the redirect URL. Herepay may or may not append the payment code to the redirect URL (not confirmed in docs; verify in UAT). To be safe:
- Before `document.write`, store `localStorage.setItem("maq.lastPaymentCode", paymentCode)` (wrap in try/catch).
- The result page reads the code from the query string (`paymentCode` or `payment_code`) first, then falls back to the stored one.
- If neither exists, show "We could not find your payment" with a link back to the booking page.

## 4. Pages and components to build

1. **Payment step** (on the booking confirmation, or `/payment?booking={id}`)
   - Shows booking summary and amount (read-only).
   - Email input (required, validated, max 50).
   - Payment method radio from `channels[].paymentMethod`; bank `select`/grid from the chosen method's `channels` (show `imgUrl` logo and `name`).
   - "Pay RM {amount}" button; disabled until valid; loading state while the POST runs; error area per section 1.2.
2. **Result page at `RedirectUrl`** (a path like `/payment/result`; this exact absolute https URL is configured in the API as `Herepay:RedirectUrl`).
   - Resolve payment code (section 3), then poll `GET /api/payments/{code}`.
   - Polling: every 3 s for up to about 60 s while `Pending`. Stop on `Paid`/`Failed`.
   - `Paid`: success message, amount, paid time, a reference to keep (`paymentCode`).
   - `Failed`: failure message with "Try again" back to the payment step for the same booking.
   - Still `Pending` after the timeout: "Your payment is still being confirmed. You do not need to pay again; check back shortly" and keep a "Check again" button. Do not tell the user it failed.
   - Network/5xx while polling: keep trying until timeout, then show the same "still confirming" message.
3. **API helper module** (e.g. `src/lib/payments.ts`) with typed functions `getChannels()`, `startPayment(body)`, `getPaymentStatus(code)`, and TypeScript types matching section 1. API base URL from an env var (`PUBLIC_API_URL`).

## 5. Rules and edge cases
- Never mark anything paid on the client, and never trust query parameters on the result page for the outcome (Herepay may add `status` params; ignore them).
- A booking that is already `Paid` gets 409 on start; the booking/payment UI should check that and show it as paid instead.
- Prevent double-clicks on Pay; if the user returns with the browser Back button from the bank, show the payment step again (a new attempt is allowed).
- Money: display with 2 decimals and `RM` prefix. The `amount` is a number in ringgit.
- Accessibility: logos need `alt` = bank name; status updates in an `aria-live` region.
- Do not log or display the raw `formHtml`.

## 6. Configuration the frontend/ops must agree on
| Item | Value |
|---|---|
| API base URL | `PUBLIC_API_URL` (dev: local API, prod: deployed API) |
| Result page URL | Must be absolute **https** (Herepay rejects http). Set the same URL in the API's `Herepay:RedirectUrl`. For local testing use `https://localhost:4321/payment/result` (local dev cert needed). |
| CORS | The site origin must be in the API's `Cors:AllowedOrigins`. |

## 7. Frontend verification checklist (UAT)
1. Channels render with logos; inactive banks absent.
2. Start with a valid bank: browser lands on the FPX simulator.
3. Pay successfully: result page shows `Paid` (callback must be reachable, see tunnel notes).
4. Fail/cancel in simulator: `Failed`, "Try again" works with a new `paymentCode`.
5. Close the tab before returning, reopen the result URL: stored code fallback works, or a clear "not found" message.
6. Callback blocked: result page shows pending, then resolves once the status call reconciles (after about 3 minutes).
7. Invalid email / unknown booking id / already-paid booking: each error renders correctly.
8. Check whether Herepay appends parameters to the redirect URL, and record the names in this document.
