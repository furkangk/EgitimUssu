#!/usr/bin/env bash
# A-09: Tüm modül DbContext'leri için aynı adlı migration'ı üretir.
# Kullanım: scripts/add-migration-all.sh <MigrationAdi>
# Tasarım zamanı bağlantı dizesi yalnız model karşılaştırması için gerekir; veritabanına bağlanılmaz.
set -uo pipefail

name="${1:?migration adi gerekli}"
export ConnectionStrings__Postgres="${ConnectionStrings__Postgres:-Host=localhost;Database=egitimussu;Username=postgres;Password=design-time-only}"

ok=0
fail=0

for dir in $(grep -rl "DesignTimeDbContextFactoryBase" src --include="*.cs" \
  | grep -v "Shared/Infrastructure/Design" \
  | xargs -n1 dirname | sort -u); do
  proj=$(ls "$dir"/*.csproj)
  if out=$(dotnet ef migrations add "$name" \
      --project "$proj" \
      --startup-project src/API.Host/EgitimUssu.API.Host.csproj \
      --no-build 2>&1); then
    ok=$((ok + 1))
    echo "OK   $dir"
  else
    fail=$((fail + 1))
    echo "FAIL $dir"
    echo "$out" | tail -5
  fi
done

echo "--- uretilen=$ok basarisiz=$fail"
[ "$fail" -eq 0 ]
