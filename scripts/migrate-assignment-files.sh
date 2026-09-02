#!/usr/bin/env bash
# P04 Task 2 — Eski ödev teslim dosyalarını ortak IFileStorage düzenine taşır.
#
# ESKİ düzen (LocalAssignmentFileStorage, kaldırıldı):
#   <eski-kok>/<assignmentId:N>.<uzanti>        → dosya
#   <eski-kok>/<assignmentId:N>.name            → orijinal dosya adı (yan dosya)
#
# YENİ düzen (LocalFileStorage, Storage:LocalRootPath):
#   <yeni-kok>/assignments/<assignmentId>/<guid:N>.<uzanti>
#
# Script dosyaları kopyalar VE veritabanını güncelleyecek SQL'i üretir
# (assignments.assignments tablosundaki AttachmentStorageKey / AttachmentFileName /
# AttachmentContentType / AttachmentSizeBytes alanları). SQL'i gözden geçirip kendiniz
# uygularsınız — script veritabanına dokunmaz.
#
# Kullanim:
#   scripts/migrate-assignment-files.sh --from <eski-kok> --to <yeni-kok> [--sql-out <dosya>] [--dry-run]
#
# Ornek:
#   scripts/migrate-assignment-files.sh \
#     --from src/API.Host/bin/Debug/net9.0/uploads/assignments \
#     --to   src/API.Host/storage \
#     --sql-out /tmp/assignment-files.sql --dry-run
set -euo pipefail

FROM_ROOT=""
TO_ROOT=""
SQL_OUT=""
DRY_RUN=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --from)    FROM_ROOT="${2:-}"; shift 2 ;;
    --to)      TO_ROOT="${2:-}"; shift 2 ;;
    --sql-out) SQL_OUT="${2:-}"; shift 2 ;;
    --dry-run) DRY_RUN=1; shift ;;
    -h|--help) sed -n '2,25p' "$0"; exit 0 ;;
    *) echo "Bilinmeyen secenek: $1" >&2; exit 2 ;;
  esac
done

if [[ -z "$FROM_ROOT" || -z "$TO_ROOT" ]]; then
  echo "HATA: --from ve --to zorunlu. Yardim icin --help." >&2
  exit 2
fi

if [[ ! -d "$FROM_ROOT" ]]; then
  echo "Eski kok bulunamadi: $FROM_ROOT (tasinacak dosya yok, cikiliyor)."
  exit 0
fi

SQL_OUT="${SQL_OUT:-${TMPDIR:-/tmp}/assignment-files-$(date +%Y%m%d%H%M%S).sql}"

echo "Kaynak      : $FROM_ROOT"
echo "Hedef       : $TO_ROOT"
echo "SQL ciktisi : $SQL_OUT"
[[ $DRY_RUN -eq 1 ]] && echo "MOD         : DRY-RUN (hicbir dosya yazilmaz)"
echo

: > "$SQL_OUT"
{
  echo "-- P04: eski odev teslim dosyalari icin depolama anahtari guncellemesi"
  echo "-- Uygulamadan ONCE yedek alin. Sema: assignments"
  echo "BEGIN;"
} >> "$SQL_OUT"

MIGRATED=0
SKIPPED=0

# .name yan dosyalarini atla; yalniz veri dosyalarini dolas.
while IFS= read -r -d '' DATA_PATH; do
  BASE="$(basename "$DATA_PATH")"
  COMPACT_ID="${BASE%%.*}"
  EXT=".${BASE##*.}"

  if [[ ${#COMPACT_ID} -ne 32 ]]; then
    echo "ATLANDI (kimlik cozulemedi): $DATA_PATH"
    SKIPPED=$((SKIPPED + 1))
    continue
  fi

  # 32 karakterlik kompakt Guid -> tireli Guid
  ASSIGNMENT_ID="${COMPACT_ID:0:8}-${COMPACT_ID:8:4}-${COMPACT_ID:12:4}-${COMPACT_ID:16:4}-${COMPACT_ID:20:12}"

  META_PATH="${FROM_ROOT}/${COMPACT_ID}.name"
  if [[ -f "$META_PATH" ]]; then
    ORIGINAL_NAME="$(tr -d '\r\n' < "$META_PATH")"
  else
    ORIGINAL_NAME="$BASE"
  fi
  [[ -z "$ORIGINAL_NAME" ]] && ORIGINAL_NAME="$BASE"

  NEW_ID="$(uuidgen | tr '[:upper:]' '[:lower:]' | tr -d '-')"
  NEW_KEY="assignments/${ASSIGNMENT_ID}/${NEW_ID}${EXT}"
  TARGET_PATH="${TO_ROOT}/${NEW_KEY}"

  SIZE_BYTES="$(wc -c < "$DATA_PATH" | tr -d ' ')"
  CONTENT_TYPE="application/octet-stream"
  case "$(echo "$EXT" | tr '[:upper:]' '[:lower:]')" in
    .pdf)  CONTENT_TYPE="application/pdf" ;;
    .png)  CONTENT_TYPE="image/png" ;;
    .jpg|.jpeg) CONTENT_TYPE="image/jpeg" ;;
    .webp) CONTENT_TYPE="image/webp" ;;
    .txt)  CONTENT_TYPE="text/plain" ;;
    .zip)  CONTENT_TYPE="application/zip" ;;
  esac

  echo "TASI  $DATA_PATH"
  echo "  ->  $TARGET_PATH  (ad: $ORIGINAL_NAME, $SIZE_BYTES bayt)"

  if [[ $DRY_RUN -eq 0 ]]; then
    mkdir -p "$(dirname "$TARGET_PATH")"
    cp "$DATA_PATH" "$TARGET_PATH"
  fi

  # SQL tirnak kacisi
  ESCAPED_NAME="${ORIGINAL_NAME//\'/\'\'}"
  {
    echo "UPDATE assignments.assignments SET"
    echo "  \"AttachmentStorageKey\"  = '${NEW_KEY}',"
    echo "  \"AttachmentFileName\"    = '${ESCAPED_NAME}',"
    echo "  \"AttachmentContentType\" = '${CONTENT_TYPE}',"
    echo "  \"AttachmentSizeBytes\"   = ${SIZE_BYTES}"
    echo "WHERE \"Id\" = '${ASSIGNMENT_ID}';"
  } >> "$SQL_OUT"

  MIGRATED=$((MIGRATED + 1))
done < <(find "$FROM_ROOT" -maxdepth 1 -type f ! -name '*.name' -print0)

echo "COMMIT;" >> "$SQL_OUT"

echo
echo "Tasinan: $MIGRATED · Atlanan: $SKIPPED"
echo "SQL hazir: $SQL_OUT"
if [[ $DRY_RUN -eq 1 ]]; then
  echo "DRY-RUN idi — dosyalar kopyalanmadi. Gercek calistirma icin --dry-run'i kaldirin."
else
  echo "Sonraki adim: SQL'i gozden gecirip uygulayin, ardindan eski koku ($FROM_ROOT) silin."
fi
