#!/usr/bin/env bash
# Gercek Postgres + Redis ile tam test paketini kosar (Testcontainers testleri dahil).
#
# Not: Testcontainers testleri kendi container'larini ayaga kaldirir; yalnizca calisan bir
# Docker daemon'a ihtiyac duyarlar. infra/docker-compose.yml yigini (yerel gelistirme icin)
# Compose kuruluysa ek olarak ayaga kaldirilir, degilse atlanir.
set -euo pipefail

cd "$(dirname "$0")/.."

if ! docker info >/dev/null 2>&1; then
  echo "HATA: Docker calismiyor. Docker Desktop veya colima baslatin (ornek: colima start)." >&2
  exit 1
fi

COMPOSE=""
if docker compose version >/dev/null 2>&1; then
  COMPOSE="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
  COMPOSE="docker-compose"
fi

if [ -n "$COMPOSE" ]; then
  if [ ! -f infra/.env ]; then
    echo "HATA: infra/.env yok. Once kopyalayin: cp infra/.env.example infra/.env" >&2
    exit 1
  fi
  echo "==> Altyapi ayaga kaldiriliyor"
  $COMPOSE -f infra/docker-compose.yml --env-file infra/.env up -d --wait
else
  echo "==> Compose bulunamadi, yerel altyapi yigini atlaniyor (Testcontainers kendi container'larini yonetir)."
  echo "    Yigini da istiyorsaniz: brew install docker-compose"
fi

echo "==> Testler"
set +e
ConnectionStrings__Postgres="InMemory:local" dotnet test EgitimUssu.slnx
TEST_EXIT=$?
set -e

if [ -n "$COMPOSE" ]; then
  echo "==> Altyapi kapatiliyor"
  $COMPOSE -f infra/docker-compose.yml --env-file infra/.env down
fi

exit $TEST_EXIT
