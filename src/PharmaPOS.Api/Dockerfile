# syntax=docker/dockerfile:1

# --- build -------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (layer-cached) using only the files that affect package resolution.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/PharmaPOS.Domain/PharmaPOS.Domain.csproj src/PharmaPOS.Domain/
COPY src/PharmaPOS.Application/PharmaPOS.Application.csproj src/PharmaPOS.Application/
COPY src/PharmaPOS.Infrastructure/PharmaPOS.Infrastructure.csproj src/PharmaPOS.Infrastructure/
COPY src/PharmaPOS.Api/PharmaPOS.Api.csproj src/PharmaPOS.Api/
RUN dotnet restore src/PharmaPOS.Api/PharmaPOS.Api.csproj

COPY src/ src/
RUN dotnet publish src/PharmaPOS.Api/PharmaPOS.Api.csproj \
    --configuration Release \
    --output /app

# --- runtime -----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql probes for GSSAPI/Kerberos at startup; the runtime image ships without it.
# curl serves the container HEALTHCHECK against /health (no shell-out to dotnet).
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app ./

USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "PharmaPOS.Api.dll"]
