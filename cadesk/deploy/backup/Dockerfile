# ==============================================================================
# Automated PostgreSQL Backup Runner Container
# Runs pg_dump + AES-256-CBC encryption on a daily cron schedule
# ==============================================================================

FROM alpine:3.21

# Install postgresql17-client (matching postgres:17-alpine), openssl, bash, curl, coreutils, ca-certificates, and aws-cli
RUN apk add --no-cache \
    postgresql17-client \
    openssl \
    bash \
    curl \
    coreutils \
    ca-certificates \
    aws-cli \
    tzdata

# Create backup working directory and scripts folder
WORKDIR /scripts

COPY deploy/backup/backup-db.sh /scripts/backup-db.sh
COPY deploy/backup/restore-db.sh /scripts/restore-db.sh
COPY deploy/backup/crontab /etc/crontabs/root

RUN chmod +x /scripts/backup-db.sh /scripts/restore-db.sh

# Mount point for persistent encrypted backup storage
VOLUME ["/backups"]

# Run crond in foreground with log level 2
CMD ["crond", "-f", "-l", "2"]
