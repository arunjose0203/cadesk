# ==============================================================================
# Multi-Stage Production Dockerfile for CaDesk Web (React + Vite + Caddy)
# Build context: repository root (e.g., docker build -f web/Dockerfile .)
# ==============================================================================

# ------------------------------------------------------------------------------
# Stage 1: Build Frontend Assets
# ------------------------------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /web

# Cache node dependencies layer
COPY web/package.json web/package-lock.json ./
RUN npm ci --prefer-offline --no-audit

# Copy application source code and compile production bundle
COPY web/ ./
RUN npm run build

# ------------------------------------------------------------------------------
# Stage 2: Serve with Hardened Caddy 2 Reverse Proxy & Web Server
# ------------------------------------------------------------------------------
FROM caddy:2-alpine
WORKDIR /srv

# Copy compiled SPA static assets
COPY --from=build /web/dist /srv

# Copy production Caddyfile configuration
COPY deploy/Caddyfile /etc/caddy/Caddyfile

# Ports for HTTP and HTTPS
EXPOSE 80 443

# Healthcheck testing HTTP response
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q -O - http://localhost/ || exit 1
