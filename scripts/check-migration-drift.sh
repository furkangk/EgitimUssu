#!/usr/bin/env bash
# CI'daki "Migration drift kontrolü" adımının yerel karşılığı.
set -uo pipefail

export ConnectionStrings__Postgres="${ConnectionStrings__Postgres:-Host=localhost;Database=egitimussu;Username=postgres;Password=design-time-only}"

drift=0

for dir in $(grep -rl "DesignTimeDbContextFactoryBase" src --include="*.cs" \
  | grep -v "Shared/Infrastructure/Design" \
  | xargs -n1 dirname | sort -u); do
  proj=$(ls "$dir"/*.csproj)
  out=$(dotnet ef migrations has-pending-model-changes \
    --project "$proj" \
    --startup-project src/API.Host/EgitimUssu.API.Host.csproj \
    --no-build 2>&1)
  if echo "$out" | grep -qi "No changes\|Değişiklik yok"; then
    echo "OK    $dir"
  else
    drift=$((drift + 1))
    echo "DRIFT $dir"
    echo "$out" | tail -3
  fi
done

echo "--- drift=$drift"
[ "$drift" -eq 0 ]
