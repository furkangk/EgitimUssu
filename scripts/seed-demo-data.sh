#!/usr/bin/env bash
# E-06: Demo veriyi yerel bir API.Host kosumunda tohumlar.
#
# Tohumlama uygulamanin ACILIS adiminda calisir (Program.cs -> SeedDemoDataAsync). Bu script
# uygulamayi dogru ortam degiskenleriyle baslatir, /health/ready yesile donunce tohumlamanin
# bittigini dogrular ve durur.
#
# Staging'de bu script'e gerek YOKTUR: render.yaml'daki Seeding__Enabled=true her deploy'da
# ayni tohumlamayi kendisi calistirir. Script yereldeki denemeler icindir.
#
# Kullanim:
#   scripts/seed-demo-data.sh                      # InMemory veritabani (hizli deneme)
#   scripts/seed-demo-data.sh "Host=localhost;..." # gercek Postgres
#
# UYARI: Production ortaminda calismaz — DemoDataSeedGuard bayrak acik olsa bile engeller.
set -euo pipefail

cd "$(dirname "$0")/.."

CONNECTION_STRING="${1:-InMemory:demo-seed}"
PORT="${SEED_PORT:-5399}"
BASE_URL="http://127.0.0.1:${PORT}"

if [ "${ASPNETCORE_ENVIRONMENT:-}" = "Production" ]; then
  echo "HATA: ASPNETCORE_ENVIRONMENT=Production. Demo veri uretimde tohumlanmaz." >&2
  exit 1
fi

echo "==> API baslatiliyor (${BASE_URL}, veritabani: ${CONNECTION_STRING})"

ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Staging}" \
ASPNETCORE_URLS="${BASE_URL}" \
ConnectionStrings__Postgres="${CONNECTION_STRING}" \
Database__ApplyMigrationsOnStartup="true" \
Seeding__Enabled="true" \
Email__Provider="Logging" \
Email__FromEmail="no-reply@egitimussu.local" \
Push__Provider="Logging" \
Jwt__SigningKey="${Jwt__SigningKey:-local-seed-signing-key-do-not-use-in-production-0123456789}" \
  dotnet run --project src/API.Host/EgitimUssu.API.Host.csproj --no-launch-profile &

API_PID=$!
# shellcheck disable=SC2064
trap "kill ${API_PID} 2>/dev/null || true" EXIT

echo "==> Hazir olmasi bekleniyor"
for _ in $(seq 1 60); do
  if curl -fsS "${BASE_URL}/health/ready" >/dev/null 2>&1; then
    echo "==> Tohumlama tamamlandi (acilista kosuldu)."
    echo
    echo "Demo hesaplar (parola: Demo1234!):"
    echo "  ogretmen : demo.ogretmen1@egitimussu.local / demo.ogretmen2@egitimussu.local"
    echo "  ogrenci  : demo.ogrenci1@egitimussu.local  / demo.ogrenci2@egitimussu.local"
    echo "  veli     : demo.veli1@egitimussu.local     / demo.veli2@egitimussu.local"
    echo
    echo "Ornek: curl -s ${BASE_URL}/api/identity/login -H 'Content-Type: application/json' \\"
    echo "         -d '{\"email\":\"demo.ogretmen1@egitimussu.local\",\"password\":\"Demo1234!\"}'"
    exit 0
  fi
  sleep 1
done

echo "HATA: API 60 saniyede hazir olmadi. Yukaridaki log'lara bakin." >&2
exit 1
