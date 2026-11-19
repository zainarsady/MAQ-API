# S2-two-bookings-pay-concurrently

Run: 2026-11-18 14:34:06 · MySQL 8.0.36 (Testcontainers) · 20 iterations · limit 20/day

**Setup:** 19 paid + 2 unpaid bookings exist. Both customers `POST /api/payments` at once, then both Herepay success callbacks arrive at once.

**Result:** 20/20 iterations over capacity.

| # | A initiate / callback | B initiate / callback | Paid bookings | Availability.Booked | Availability.Remaining |
|---|---|---|---|---|---|
| 1 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 2 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 3 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 4 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 5 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 6 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 7 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 8 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 9 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 10 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 11 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 12 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 13 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 14 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 15 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 16 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 17 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 18 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 19 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
| 20 | 200 / 200 | 200 / 200 | 21 | 21 | 0 |
