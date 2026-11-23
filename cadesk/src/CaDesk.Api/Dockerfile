# ==============================================================================
# Multi-Stage Production Dockerfile for CaDesk.Api (.NET 10 Web API)
# Build context: repository root (e.g., docker build -f src/CaDesk.Api/Dockerfile .)
# ==============================================================================

# ------------------------------------------------------------------------------
# Stage 1: Build & Publish
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

# Copy project manifests for optimized layer caching
COPY Directory.Build.props Directory.Packages.props CaDesk.slnx ./
COPY src/CaDesk.Domain/CaDesk.Domain.csproj src/CaDesk.Domain/
COPY src/CaDesk.Application/CaDesk.Application.csproj src/CaDesk.Application/
COPY src/CaDesk.Infrastructure/CaDesk.Infrastructure.csproj src/CaDesk.Infrastructure/
COPY src/CaDesk.Api/CaDesk.Api.csproj src/CaDesk.Api/

# Restore dependencies
RUN dotnet restore src/CaDesk.Api/CaDesk.Api.csproj

# Copy remaining source code and publish
COPY src/ src/
RUN dotnet publish src/CaDesk.Api/CaDesk.Api.csproj \
    -c Release \
    -o /app \
    --no-restore \
    /p:UseAppHost=false

# ------------------------------------------------------------------------------
# Stage 2: Hardened Production Runtime
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

# Install curl for container health checks
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Copy published application from build stage
COPY --from=build /app .

# Create volume mount points for Data Protection keys and document storage
RUN mkdir -p /keys /documents && \
    chown -R $APP_UID:$APP_UID /app /keys /documents && \
    chmod 700 /keys /documents

# Switch to non-root application user
USER $APP_UID

# Environment defaults
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Production Healthcheck using built-in liveness endpoint
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "CaDesk.Api.dll"]
