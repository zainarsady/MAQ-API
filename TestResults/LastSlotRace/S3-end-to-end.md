# S3-end-to-end

Run: 2026-11-18 14:34:05 · MySQL 8.0.36 (Testcontainers) · 20 iterations · limit 20/day

**Setup:** 19 paid bookings exist. Two customers each run book → pay → success callback, in parallel.

**Result:** 0/20 iterations over capacity.

| # | User A | User B | Paid bookings | Availability.Remaining |
|---|---|---|---|---|
| 1 | booking 201, initiate 200, callback 200, payment Paid | booking 409 | 20 | 0 |
| 2 | booking 201, initiate 200, callback 200, payment Paid | booking 409 | 20 | 0 |
| 3 | booking 201, initiate 200, callback 200, payment Paid | booking 409 | 20 | 0 |
| 4 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 5 | booking 201, initiate 200, callback 200, payment Paid | booking 409 | 20 | 0 |
| 6 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 7 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 8 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 9 | booking 201, initiate 200, callback 200, payment Paid | booking 409 | 20 | 0 |
| 10 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 11 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 12 | booking 201, initiate 200, callback 200, payment Paid | booking 409 | 20 | 0 |
| 13 | booking 201, initiate 200, callback 200, payment Paid | booking 409 | 20 | 0 |
| 14 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 15 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 16 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 17 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 18 | booking 201, initiate 200, callback 200, payment Paid | booking 409 | 20 | 0 |
| 19 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
| 20 | booking 409 | booking 201, initiate 200, callback 200, payment Paid | 20 | 0 |
