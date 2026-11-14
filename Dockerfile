# ─── Build stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore katmanı önbellekten yararlanmak için önce meta dosyaları kopyala
COPY global.json Directory.Build.props ./
COPY src/ src/

RUN dotnet restore src/API.Host/EgitimUssu.API.Host.csproj

RUN dotnet publish src/API.Host/EgitimUssu.API.Host.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ─── Runtime stage ──────────────────────────────────────────────────────────
# K-19: Yerel takvim "Europe/Istanbul"u /usr/share/zoneinfo'dan çözer. Debian tabanlı aspnet:9.0
# imajında tzdata kurulu (P16 T1'de doğrulandı). alpine / chiseled'e geçilirse tzdata eklenmeli —
# aksi halde LocalCalendarGuard açılışı durdurur.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EgitimUssu.API.Host.dll"]
