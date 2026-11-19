# Build from the repository root:  docker build -f src/CaDesk.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props CaDesk.slnx ./
COPY src/ src/
RUN dotnet publish src/CaDesk.Api/CaDesk.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# folders for Data Protection keys and document files (mounted as volumes); writable by the non-root app user
RUN mkdir -p /keys /documents && chown $APP_UID /keys /documents
USER $APP_UID
ENTRYPOINT ["dotnet", "CaDesk.Api.dll"]
