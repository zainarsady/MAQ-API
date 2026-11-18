# S1-concurrent-booking-creation

Run: 2026-11-18 12:03:33 · MySQL 8.0.36 (Testcontainers) · 20 iterations · limit 20/day

**Setup:** 19 paid bookings exist. Two `POST /api/TempahanRamadan` sent at the same instant.

**Result:** 19/20 iterations over capacity.

| # | User A | User B | Booking rows for date |
|---|---|---|---|
| 1 | 409 | 201 | 20 |
| 2 | 201 | 201 | 21 |
| 3 | 201 | 201 | 21 |
| 4 | 201 | 201 | 21 |
| 5 | 201 | 201 | 21 |
| 6 | 201 | 201 | 21 |
| 7 | 201 | 201 | 21 |
| 8 | 201 | 201 | 21 |
| 9 | 201 | 201 | 21 |
| 10 | 201 | 201 | 21 |
| 11 | 201 | 201 | 21 |
| 12 | 201 | 201 | 21 |
| 13 | 201 | 201 | 21 |
| 14 | 201 | 201 | 21 |
| 15 | 201 | 201 | 21 |
| 16 | 201 | 201 | 21 |
| 17 | 201 | 201 | 21 |
| 18 | 201 | 201 | 21 |
| 19 | 201 | 201 | 21 |
| 20 | 201 | 201 | 21 |
