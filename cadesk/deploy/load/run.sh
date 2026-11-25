#!/usr/bin/env bash
# CaDesk load test (Sprint 72): 50 firms × 500 clients, then a filing-day workload.
# Needs: k6 (https://k6.io, or Docker), psql, and an API started against a database you can throw away.
#
#   BASE_URL=https://staging.example.in OWNER_CS="postgresql://cadesk_owner:...@host/cadesk_load" ./deploy/load/run.sh
#
# Start the API with RateLimits__AuthPerMinute=100000: all virtual users sign in from one IP address.
set -euo pipefail
cd "$(dirname "$0")"
: "${BASE_URL:?Set BASE_URL to the API, e.g. http://localhost:5080}"
: "${OWNER_CS:?Set OWNER_CS to the owner connection string of the load-test database}"
export BASE_URL FIRMS="${FIRMS:-50}" CLIENTS="${CLIENTS:-500}"

if command -v k6 >/dev/null; then K6=(k6); else K6=(docker run --rm -i --network host -e BASE_URL -e FIRMS -e CLIENTS -e USERS_PER_FIRM -e HOLD -e RAMP -v "$PWD:/load" -w /load grafana/k6); fi

echo "== 1/4 sign up $FIRMS firms"
"${K6[@]}" run 01-signup.js
echo "== 2/4 open every desk and lift the client limit for the load firms"
psql "$OWNER_CS" -v ON_ERROR_STOP=1 -f seed-plans.sql
echo "== 3/4 import $CLIENTS clients into each firm"
"${K6[@]}" run 02-import.js
echo "== 4/4 filing-day workload"
"${K6[@]}" run --summary-export=summary.json 03-workload.js
echo "Done. Results: deploy/load/summary.json"
