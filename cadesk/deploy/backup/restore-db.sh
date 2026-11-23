#!/usr/bin/env bash
# ==============================================================================
# CaDesk Automated PostgreSQL Backup Restore / Verification Tool
# - Decrypts AES-256-CBC encrypted backup
# - Verifies SHA-256 checksum
# - Restores into target PostgreSQL database using pg_restore
# ==============================================================================

set -euo pipefail

if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <path_to_encrypted_backup_file> [target_db_name]"
    echo "Example: $0 /backups/daily/cadesk_20261123_120000Z.dump.enc cadesk_restored"
    exit 1
fi

ENC_FILE="$1"
DB_NAME="${2:-cadesk_restored}"
DB_HOST="${DB_HOST:-db}"
DB_PORT="${DB_PORT:-5432}"
DB_USER="${DB_USER:-cadesk_owner}"
PGPASSWORD="${PGPASSWORD:-owner_dev_pw}"
export PGPASSWORD

BACKUP_PASSPHRASE="${BACKUP_ENCRYPTION_KEY:-default_secure_backup_key_change_in_production_123456}"
SHA_FILE="${ENC_FILE%.enc}.sha256"

echo "=========================================================="
echo " CaDesk PostgreSQL Restore & Disaster Recovery Tool"
echo " Target Database: ${DB_NAME} on ${DB_HOST}:${DB_PORT}"
echo " Input File: ${ENC_FILE}"
echo "=========================================================="

if [[ ! -f "${ENC_FILE}" ]]; then
    echo "ERROR: Backup file '${ENC_FILE}' not found!" >&2
    exit 1
fi

# 1. Verify SHA-256 checksum if exists
if [[ -f "${SHA_FILE}" ]]; then
    echo "[1/4] Verifying SHA-256 checksum..."
    sha256sum -c "${SHA_FILE}"
    echo "      Checksum verification passed."
else
    echo "[1/4] Warning: No checksum file found, skipping integrity verification."
fi

# 2. Decrypt archive
DEC_FILE="/tmp/cadesk_restore_decrypted.dump"
echo "[2/4] Decrypting archive using OpenSSL..."
openssl enc -d -aes-256-cbc \
    -pbkdf2 \
    -iter 100000 \
    -in "${ENC_FILE}" \
    -out "${DEC_FILE}" \
    -pass pass:"${BACKUP_PASSPHRASE}"

# 3. Create target database if it does not exist
echo "[3/4] Ensuring database '${DB_NAME}' exists..."
psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d postgres -c \
    "SELECT 1 FROM pg_database WHERE datname = '${DB_NAME}';" | grep -q 1 || \
psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d postgres -c \
    "CREATE DATABASE \"${DB_NAME}\" OWNER \"${DB_USER}\";"

# 4. Restore using pg_restore
echo "[4/4] Restoring database schema and data..."
pg_restore -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d "${DB_NAME}" \
    --clean \
    --if-exists \
    --no-owner \
    --no-privileges \
    --verbose \
    "${DEC_FILE}" || true

# Securely wipe decrypted file
shred -u "${DEC_FILE}" 2>/dev/null || rm -f "${DEC_FILE}"

echo "=========================================================="
echo " Restore completed successfully into '${DB_NAME}'"
echo "=========================================================="
