# S4-same-booking-paid-twice

Run: 2026-11-18 12:03:15 · MySQL 8.0.36 (Testcontainers) · 20 iterations · limit 20/day

**Setup:** 19 paid + 1 unpaid booking. The unpaid booking's owner submits payment twice at once; both success callbacks arrive at once.

**Result:** 20/20 iterations double-charged.

| # | Attempt 1 initiate / callback | Attempt 2 initiate / callback | Paid payments for booking | Paid bookings for date |
|---|---|---|---|---|
| 1 | 200 / 200 | 200 / 200 | 2 | 20 |
| 2 | 200 / 200 | 200 / 200 | 2 | 20 |
| 3 | 200 / 200 | 200 / 200 | 2 | 20 |
| 4 | 200 / 200 | 200 / 200 | 2 | 20 |
| 5 | 200 / 200 | 200 / 200 | 2 | 20 |
| 6 | 200 / 200 | 200 / 200 | 2 | 20 |
| 7 | 200 / 200 | 200 / 200 | 2 | 20 |
| 8 | 200 / 200 | 200 / 200 | 2 | 20 |
| 9 | 200 / 200 | 200 / 200 | 2 | 20 |
| 10 | 200 / 200 | 200 / 200 | 2 | 20 |
| 11 | 200 / 200 | 200 / 200 | 2 | 20 |
| 12 | 200 / 200 | 200 / 200 | 2 | 20 |
| 13 | 200 / 200 | 200 / 200 | 2 | 20 |
| 14 | 200 / 200 | 200 / 200 | 2 | 20 |
| 15 | 200 / 200 | 200 / 200 | 2 | 20 |
| 16 | 200 / 200 | 200 / 200 | 2 | 20 |
| 17 | 200 / 200 | 200 / 200 | 2 | 20 |
| 18 | 200 / 200 | 200 / 200 | 2 | 20 |
| 19 | 200 / 200 | 200 / 200 | 2 | 20 |
| 20 | 200 / 200 | 200 / 200 | 2 | 20 |
