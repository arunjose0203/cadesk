#!/bin/sh
# Run automatically on first Postgres container start via /docker-entrypoint-initdb.d.
# Reads OWNER_DB_PASSWORD and APP_DB_PASSWORD from environment so production deploys never boot with dev passwords.
set -eu

OWNER_PW="${OWNER_DB_PASSWORD:-owner_dev_pw}"
APP_PW="${APP_DB_PASSWORD:-app_dev_pw}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v owner_pw="$OWNER_PW" -v app_pw="$APP_PW" <<'EOSQL'
SELECT format('CREATE ROLE cadesk_owner LOGIN PASSWORD %L', :'owner_pw')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'cadesk_owner')\gexec

SELECT format('ALTER ROLE cadesk_owner WITH PASSWORD %L', :'owner_pw')\gexec

SELECT format('CREATE ROLE cadesk_app LOGIN PASSWORD %L', :'app_pw')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'cadesk_app')\gexec

SELECT format('ALTER ROLE cadesk_app WITH PASSWORD %L', :'app_pw')\gexec

SELECT 'CREATE DATABASE cadesk OWNER cadesk_owner'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'cadesk')\gexec
EOSQL
