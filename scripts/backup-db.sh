#!/usr/bin/env bash
# E-05 — Veritabani yedegi alir (pg_dump, custom format) ve istege bagli olarak S3/R2'ye yukler.
#
# Kullanim:
#   scripts/backup-db.sh                                  # $ConnectionStrings__Postgres kullanilir
#   scripts/backup-db.sh "Host=localhost;Port=5432;Database=egitimussu;Username=..;Password=.."
#   scripts/backup-db.sh "postgresql://user:pass@host:5432/db"
#
# Ortam degiskenleri:
#   BACKUP_DIR             Yedeklerin yazilacagi dizin        (varsayilan: ./backups)
#   BACKUP_RETENTION_DAYS  Yerelde saklama suresi, gun        (varsayilan: 30)
#   BACKUP_S3_BUCKET       Doluysa yedek buraya da yuklenir   (ornek: s3://egitimussu-backups)
#   BACKUP_S3_ENDPOINT     S3-uyumlu ozel uc (Cloudflare R2)  (bos = AWS S3)
#   PG_IMAGE_TAG           Docker'a dusulurse istemci surumu  (varsayilan: 17)
#
# Cikti: <BACKUP_DIR>/egitimussu-<db>-<YYYYMMDD-HHMMSS>Z.dump  (pg_restore ile geri yuklenir)
#
# Prosedur, sıklık ve RTO/RPO: doc/architecture/backup_restore.md
set -euo pipefail

cd "$(dirname "$0")/.."
# shellcheck source=lib/pg-connection.sh
. "$(dirname "$0")/lib/pg-connection.sh"

RAW_CONNECTION="${1:-${ConnectionStrings__Postgres:-}}"
if [ -z "$RAW_CONNECTION" ]; then
  echo "HATA: baglanti dizesi verilmedi." >&2
  echo "      Kullanim: scripts/backup-db.sh \"Host=..;Database=..;Username=..;Password=..\"" >&2
  echo "      ya da: export ConnectionStrings__Postgres=..." >&2
  exit 1
fi
case "$RAW_CONNECTION" in
  InMemory:*)
    echo "HATA: '$RAW_CONNECTION' bellek-ici bir saglayici; yedeklenecek bir veritabani yok." >&2
    exit 1
    ;;
esac

BACKUP_DIR="${BACKUP_DIR:-$(pwd)/backups}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-30}"

pg_parse_connection "$RAW_CONNECTION"
mkdir -p "$BACKUP_DIR"
pg_use_runtime pg_dump

# Zaman damgasi UTC: yedek dosyalari farkli saat dilimlerindeki makinelerde de siralanabilir olmali.
STAMP="$(date -u +%Y%m%d-%H%M%S)Z"
FILE_NAME="egitimussu-${PG_DB_NAME}-${STAMP}.dump"
HOST_PATH="${BACKUP_DIR}/${FILE_NAME}"

echo "==> Yedek aliniyor: ${PG_TARGET_LABEL}"
echo "    Hedef dosya   : ${HOST_PATH}"

# -Fc (custom): pg_restore ile secmeli geri yukleme yapilabilir ve sikistirilmistir.
# --no-owner/--no-privileges GERI YUKLEMEDE verilir; yedegin kendisi tam bilgiyi tasisin.
# Arsiv stdout'tan akar (bkz. lib/pg-connection.sh -> pg_use_runtime).
if ! pg_client pg_dump -d "$PG_CONNINFO" --format=custom >"$HOST_PATH"; then
  # Yarim dosya birakmak, "yedek var" yanilgisi uretir; en tehlikeli basarisizlik budur.
  rm -f "$HOST_PATH"
  echo "HATA: pg_dump basarisiz. Yarim dosya silindi." >&2
  exit 1
fi

# Dogrulama: dosya HOST'ta olustu mu ve okunabilir bir arsiv mi? 0 baytlik ya da kesik
# bir dump sessizce gecmemeli — "yedegim var" yanilgisi, yedeksizlikten daha tehlikelidir.
if [ ! -s "$HOST_PATH" ]; then
  echo "HATA: yedek dosyasi olusmadi ya da bos: ${HOST_PATH}" >&2
  exit 1
fi

TOC="$(pg_client pg_restore --list <"$HOST_PATH")" || {
  echo "HATA: yedek dosyasi okunamiyor (pg_restore --list basarisiz): ${HOST_PATH}" >&2
  exit 1
}

SIZE="$(wc -c <"$HOST_PATH" | tr -d ' ')"
TABLE_COUNT="$(printf '%s\n' "$TOC" | grep -c ' TABLE DATA ' || true)"
echo "==> Yedek tamam: ${SIZE} bayt, ${TABLE_COUNT} tablo verisi."

# --- S3 / R2'ye yukleme (istege bagli) --------------------------------------------
if [ -n "${BACKUP_S3_BUCKET:-}" ]; then
  if ! command -v aws >/dev/null 2>&1; then
    echo "HATA: BACKUP_S3_BUCKET dolu ama 'aws' CLI bulunamadi." >&2
    exit 1
  fi
  S3_ARGS=""
  [ -n "${BACKUP_S3_ENDPOINT:-}" ] && S3_ARGS="--endpoint-url ${BACKUP_S3_ENDPOINT}"
  echo "==> Yukleniyor: ${BACKUP_S3_BUCKET}/${FILE_NAME}"
  # shellcheck disable=SC2086
  aws $S3_ARGS s3 cp "$HOST_PATH" "${BACKUP_S3_BUCKET}/${FILE_NAME}"
  echo "==> Yukleme tamam."
else
  echo "==> BACKUP_S3_BUCKET bos; yedek yalniz yerelde. Tek kopya yedek, yedek sayilmaz."
fi

# --- Yerel saklama suresi ----------------------------------------------------------
if [ "$RETENTION_DAYS" -gt 0 ] 2>/dev/null; then
  DELETED="$(find "$BACKUP_DIR" -maxdepth 1 -name 'egitimussu-*.dump' -type f -mtime "+${RETENTION_DAYS}" -print -delete | wc -l | tr -d ' ')"
  [ "$DELETED" -gt 0 ] && echo "==> ${RETENTION_DAYS} gunden eski ${DELETED} yerel yedek silindi."
fi

echo "$HOST_PATH"
