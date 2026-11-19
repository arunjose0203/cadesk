# Builds the React app and serves it with Caddy, which also proxies /api to the API.
# Build from the repository root:  docker build -f web/Dockerfile .
FROM node:22-alpine AS build
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM caddy:2
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /web/dist /srv
