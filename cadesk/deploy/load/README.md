# Load test (Sprint 72)

Checks that one CaDesk server holds **50 firms × 500 clients** with 3 staff per firm working at once
(150 concurrent users) on a filing day.

## What it does

| Step | Script | What happens |
| --- | --- | --- |
| 1 | `01-signup.js` | Signs up `load-firm-000` … `load-firm-049` through the real sign-up |
| 2 | `seed-plans.sql` | Puts those firms on the all-desks plan (Starter stops at 100 clients) and accepts the legal documents |
| 3 | `02-import.js` | Imports 500 clients per firm through the real CSV import (25,000 clients, with PAN/GSTIN checks and deadline generation) |
| 4 | `03-workload.js` | Ramps to 150 users over 2 minutes, holds 10 minutes: Today screen, deadlines, tasks, client search, a client's record and documents, billing summary, with think time between screens |

## Pass marks

| Measure | Target |
| --- | --- |
| Failed requests | under 1% |
| Screen reads, 95th percentile | under 800 ms |
| Screen reads, 99th percentile | under 2 s |
| Deadlines list, 95th percentile | under 1 s |
| Import of 500 clients, 95th percentile | under 60 s |

k6 exits with an error if a target is missed.

## Running it

Use a database you can throw away; the test adds 50 firms and 25,000 clients.

```bash
# API, against the load-test database, with sign-in throttling off for the test
ConnectionStrings__App=... ConnectionStrings__Owner=... RateLimits__AuthPerMinute=100000 \
  dotnet run -c Release --project src/CaDesk.Api

BASE_URL=http://localhost:5080 OWNER_CS="postgresql://cadesk_owner:owner_dev_pw@localhost/cadesk_load" \
  ./deploy/load/run.sh
```

Smaller first run: `FIRMS=5 CLIENTS=100 HOLD=2m ./deploy/load/run.sh`.
Options: `USERS_PER_FIRM` (default 3), `RAMP` (2m), `HOLD` (10m), `LOAD_PASSWORD`.

Run it against the same size of server you will use for the pilot (the production compose file gives the
database 2 CPUs and 2 GB). While it runs, watch `/metrics` and the database's CPU; after it, look at
`summary.json` and the slowest request names.
