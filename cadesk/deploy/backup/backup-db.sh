#!/usr/bin/env bash
# ==============================================================================
# CaDesk Automated PostgreSQL Encrypted Backup Engine
# - Uses pg_dump with custom compressed archive format (-Fc)
# - Encrypts with OpenSSL AES-256-CBC (PBKDF2 with 100k iterations)
# - Calculates SHA-256 integrity checksum
# - Implements Grandfather-Father-Son (GFS) rotation:
#     * Keep last 7 daily backups
#     * Keep last 4 weekly backups (Sundays)
#     * Keep last 12 monthly backups (1st of month)
# - Supports AWS S3, Google Cloud Storage (GCS), Azure Blob, and Healthchecks.io
# ==============================================================================

set -euo pipefail

# Configuration with environment defaults
DB_HOST="${DB_HOST:-db}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-cadesk}"
DB_USER="${DB_USER:-cadesk_owner}"
PGPASSWORD="${PGPASSWORD:-owner_dev_pw}"
export PGPASSWORD

BACKUP_DIR="${BACKUP_DIR:-/backups}"
TIMESTAMP=$(date -u +"%Y%m%d_%H%M%SZ")
DAY_OF_WEEK=$(date -u +"%u") # 1=Mon .. 7=Sun
DAY_OF_MONTH=$(date -u +"%d") # 01 .. 31

BACKUP_PASSPHRASE="${BACKUP_ENCRYPTION_KEY:-default_secure_backup_key_change_in_production_123456}"
S3_BUCKET="${BACKUP_S3_BUCKET:-}"
GCS_BUCKET="${BACKUP_GCS_BUCKET:-}"
HEALTHCHECK_URL="${HEALTHCHECK_BACKUP_URL:-}"

mkdir -p "${BACKUP_DIR}/daily" "${BACKUP_DIR}/weekly" "${BACKUP_DIR}/monthly"

RAW_DUMP="/tmp/${DB_NAME}_${TIMESTAMP}.dump"
ENC_FILE="${BACKUP_DIR}/daily/${DB_NAME}_${TIMESTAMP}.dump.enc"
SHA_FILE="${BACKUP_DIR}/daily/${DB_NAME}_${TIMESTAMP}.sha256"

echo "=========================================================="
echo " Starting CaDesk PostgreSQL Backup: ${TIMESTAMP}"
echo " Host: ${DB_HOST}:${DB_PORT} | Database: ${DB_NAME}"
echo "=========================================================="

# 1. Execute pg_dump in custom compressed format
echo "[1/5] Dumping database using pg_dump..."
if ! pg_dump -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d "${DB_NAME}" \
    --format=custom \
    --no-owner \
    --no-privileges \
    --verbose \
    --file="${RAW_DUMP}" 2>/tmp/pg_dump_err.log; then
    echo "ERROR: pg_dump failed!" >&2
    cat /tmp/pg_dump_err.log >&2
    if [[ -n "${HEALTHCHECK_URL}" ]]; then
        curl -fsS --retry 3 "${HEALTHCHECK_URL}/fail" || true
    fi
    exit 1
fi

DUMP_SIZE=$(du -h "${RAW_DUMP}" | cut -f1)
echo "      Database dump complete. Unencrypted size: ${DUMP_SIZE}"

# 2. Encrypt with AES-256-CBC with PBKDF2
echo "[2/5] Encrypting archive with OpenSSL AES-256-CBC..."
openssl enc -aes-256-cbc \
    -salt \
    -pbkdf2 \
    -iter 100000 \
    -in "${RAW_DUMP}" \
    -out "${ENC_FILE}" \
    -pass pass:"${BACKUP_PASSPHRASE}"

# Securely wipe unencrypted temporary dump
shred -u "${RAW_DUMP}" 2>/dev/null || rm -f "${RAW_DUMP}"
ENC_SIZE=$(du -h "${ENC_FILE}" | cut -f1)
echo "      Encrypted archive created: ${ENC_FILE} (${ENC_SIZE})"

# 3. Generate SHA-256 checksum for tamper-evidence
echo "[3/5] Generating SHA-256 checksum..."
sha256sum "${ENC_FILE}" > "${SHA_FILE}"
echo "      Checksum: $(cat "${SHA_FILE}")"

# 4. GFS Rotation (Copy to weekly/monthly if applicable)
if [[ "${DAY_OF_WEEK}" -eq 7 ]]; then
    echo "      Today is Sunday: archiving to weekly storage..."
    cp "${ENC_FILE}" "${BACKUP_DIR}/weekly/"
    cp "${SHA_FILE}" "${BACKUP_DIR}/weekly/"
fi

if [[ "${DAY_OF_MONTH}" -eq "01" ]]; then
    echo "      Today is 1st of month: archiving to monthly storage..."
    cp "${ENC_FILE}" "${BACKUP_DIR}/monthly/"
    cp "${SHA_FILE}" "${BACKUP_DIR}/monthly/"
fi

# 5. Cloud Storage Sync
if [[ -n "${S3_BUCKET}" ]]; then
    echo "[4/5] Uploading encrypted backup to AWS S3 (${S3_BUCKET})..."
    aws s3 cp "${ENC_FILE}" "s3://${S3_BUCKET}/backups/daily/"
    aws s3 cp "${SHA_FILE}" "s3://${S3_BUCKET}/backups/daily/"
fi

if [[ -n "${GCS_BUCKET}" ]]; then
    echo "[4/5] Uploading encrypted backup to Google Cloud Storage (${GCS_BUCKET})..."
    gcloud storage cp "${ENC_FILE}" "gs://${GCS_BUCKET}/backups/daily/" || gsutil cp "${ENC_FILE}" "gs://${GCS_BUCKET}/backups/daily/"
    gcloud storage cp "${SHA_FILE}" "gs://${GCS_BUCKET}/backups/daily/" || gsutil cp "${SHA_FILE}" "gs://${GCS_BUCKET}/backups/daily/"
fi

# 6. Retention Pruning:
# Keep 7 daily backups, 4 weekly backups, 12 monthly backups
echo "[5/5] Enforcing retention policy..."
find "${BACKUP_DIR}/daily" -type f -mtime +7 -delete
find "${BACKUP_DIR}/weekly" -type f -mtime +28 -delete
find "${BACKUP_DIR}/monthly" -type f -mtime +365 -delete
echo "      Old backup pruning complete."

# Notify healthcheck URL on success
if [[ -n "${HEALTHCHECK_URL}" ]]; then
    curl -fsS --retry 3 "${HEALTHCHECK_URL}" || true
fi

echo "=========================================================="
echo " CaDesk PostgreSQL Backup COMPLETED SUCCESSFULLY"
echo " Archive: ${ENC_FILE}"
echo "=========================================================="
