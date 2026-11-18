# S3-end-to-end

Run: 2026-11-18 12:03:13 · MySQL 8.0.36 (Testcontainers) · 20 iterations · limit 20/day

**Setup:** 19 paid bookings exist. Two customers each run book → pay → success callback, in parallel.

**Result:** 20/20 iterations over capacity.

| # | User A | User B | Paid bookings | Availability.Remaining |
|---|---|---|---|---|
| 1 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 2 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 3 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 4 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 5 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 6 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 7 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 8 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 9 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 10 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 11 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 12 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 13 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 14 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 15 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 16 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 17 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 18 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 19 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
| 20 | booking 201, initiate 200, callback 200, payment Paid | booking 201, initiate 200, callback 200, payment Paid | 21 | 0 |
