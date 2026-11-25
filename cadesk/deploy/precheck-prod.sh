#!/usr/bin/env bash
# Pre-launch validator and .env generator for CaDesk Linux/VPS staging & production hosts.
# Usage:
#   ./deploy/precheck-prod.sh --init-env
#   ./deploy/precheck-prod.sh --env .env --site https://app.yourdomain.in
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${REPO_ROOT}/.env"
TEMPLATE_FILE="${REPO_ROOT}/.env.production.example"
INIT_ENV=false
FORCE=false
SITE_URL=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --init-env) INIT_ENV=true; shift ;;
    --force) FORCE=true; shift ;;
    --env) ENV_FILE="$2"; shift 2 ;;
    --site) SITE_URL="$2"; shift 2 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

if [[ "${INIT_ENV}" == "true" ]]; then
  if [[ -f "${ENV_FILE}" && "${FORCE}" != "true" ]]; then
    echo "ERROR: ${ENV_FILE} already exists. Pass --force to overwrite." >&2
    exit 1
  fi
  cp "${TEMPLATE_FILE}" "${ENV_FILE}"
  chmod 600 "${ENV_FILE}"
  rand_alnum() { openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c "$1"; }
  rand_b64() { openssl rand -base64 48 | tr -d '\n'; }

  sed -i.bak \
    -e "s|CHANGE_ME_SUPERUSER_STRONG_PASSWORD_32_CHARS|$(rand_alnum 32)|g" \
    -e "s|CHANGE_ME_APP_ROLE_PASSWORD_32_CHARS|$(rand_alnum 32)|g" \
    -e "s|CHANGE_ME_OWNER_ROLE_PASSWORD_32_CHARS|$(rand_alnum 32)|g" \
    -e "s|CHANGE_ME_GENERATE_WITH_OPENSSL_RAND_BASE64_64_CHARACTERS|$(rand_b64)|g" \
    -e "s|CHANGE_ME_GENERATE_STRONG_BACKUP_ENCRYPTION_KEY_32_CHARS|$(rand_alnum 32)|g" \
    -e "s|CHANGE_ME_GENERATE_RANDOM_WHATSAPP_VERIFY_TOKEN|$(rand_alnum 24)|g" \
    "${ENV_FILE}" && rm -f "${ENV_FILE}.bak"
  echo "Generated ${ENV_FILE} (mode 600) with random secrets."
fi

FAIL=0
echo "[1/3] Checking legal & help HTML bracket placeholders..."
for f in \
  "${REPO_ROOT}/web/public/legal/terms.html" \
  "${REPO_ROOT}/web/public/legal/privacy.html" \
  "${REPO_ROOT}/web/public/legal/dpa.html" \
  "${REPO_ROOT}/web/public/help/report-a-problem.html"; do
  if [[ ! -f "$f" ]]; then
    echo "  MISSING: $f"; FAIL=$((FAIL+1)); continue
  fi
  if grep -nE '\[[^]]+\]' "$f" >/dev/null; then
    echo "  PLACEHOLDERS LEFT IN $(basename "$f"):"
    grep -nE '\[[^]]+\]' "$f" | sed 's/^/    /'
    FAIL=$((FAIL+1))
  fi
done

echo "[2/3] Checking ${ENV_FILE}..."
if [[ ! -f "${ENV_FILE}" ]]; then
  echo "  MISSING ${ENV_FILE} (run with --init-env)"
  FAIL=$((FAIL+1))
else
  if grep -nE 'CHANGE_ME|YOUR_' "${ENV_FILE}" >/dev/null; then
    echo "  UNREPLACED PLACEHOLDERS IN ${ENV_FILE}:"
    grep -nE 'CHANGE_ME|YOUR_' "${ENV_FILE}" | sed 's/^/    /'
    FAIL=$((FAIL+1))
  fi
fi

echo "[3/3] Checking live site..."
if [[ -n "${SITE_URL}" ]]; then
  BASE="${SITE_URL%/}"
  for ep in /health/live /health/ready /legal/terms.html /legal/privacy.html /legal/dpa.html /help/report-a-problem.html; do
    code=$(curl -sS -o /dev/null -w "%{http_code}" --max-time 10 "${BASE}${ep}" || echo "000")
    if [[ "${code}" -lt 200 || "${code}" -ge 300 ]]; then
      echo "  FAIL ${BASE}${ep} -> HTTP ${code}"; FAIL=$((FAIL+1))
    else
      echo "  OK   ${ep} -> HTTP ${code}"
    fi
  done
else
  echo "  SKIP live check (pass --site https://app.yourdomain.in after starting stack)"
fi

if [[ "${FAIL}" -gt 0 ]]; then
  echo "FAILED with ${FAIL} pre-launch blocker(s)." >&2
  exit 1
fi
echo "ALL PRE-LAUNCH CHECKS PASSED."
