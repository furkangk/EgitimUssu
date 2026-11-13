#!/usr/bin/env bash
# E-05 — Bir yedegi hedef veritabanina geri yukler. VAR OLAN VERININ UZERINE YAZAR.
#
# Kullanim:
#   scripts/restore-db.sh <yedek-dosyasi> [baglanti-dizesi] --yes
#
# Ornek:
#   scripts/restore-db.sh backups/egitimussu-egitimussu-20260905-101500Z.dump \
#       "Host=localhost;Port=5432;Database=egitimussu;Username=..;Password=.." --yes
#
# --yes VERILMEDEN CALISMAZ. Hedef ekrana yazilir ve script durur; geri yukleme
# yanlis veritabanina yapildiginda geri alinamaz, onay bilincli olmalidir.
#
# Ortam degiskenleri:
#   PG_IMAGE_TAG   pg_restore yerelde yoksa kullanilacak imaj surumu (varsayilan: 17)
#
# Prosedur ve tatbikat: doc/architecture/backup_restore.md
set -euo pipefail

cd "$(dirname "$0")/.."
# shellcheck source=lib/pg-connection.sh
. "$(dirname "$0")/lib/pg-connection.sh"

CONFIRMED="false"
DUMP_FILE=""
RAW_CONNECTION=""

for arg in "$@"; do
  case "$arg" in
    --yes) CONFIRMED="true" ;;
    -*)
      echo "HATA: bilinmeyen secenek: $arg" >&2
      exit 1
      ;;
    *)
      if [ -z "$DUMP_FILE" ]; then DUMP_FILE="$arg"
      elif [ -z "$RAW_CONNECTION" ]; then RAW_CONNECTION="$arg"
      else echo "HATA: fazladan arguman: $arg" >&2; exit 1
      fi
      ;;
  esac
done

if [ -z "$DUMP_FILE" ]; then
  echo "HATA: yedek dosyasi verilmedi." >&2
  echo "      Kullanim: scripts/restore-db.sh <yedek-dosyasi> [baglanti] --yes" >&2
  exit 1
fi
if [ ! -f "$DUMP_FILE" ]; then
  echo "HATA: yedek dosyasi bulunamadi: $DUMP_FILE" >&2
  exit 1
fi

RAW_CONNECTION="${RAW_CONNECTION:-${ConnectionStrings__Postgres:-}}"
if [ -z "$RAW_CONNECTION" ]; then
  echo "HATA: baglanti dizesi verilmedi (arguman ya da ConnectionStrings__Postgres)." >&2
  exit 1
fi

pg_parse_connection "$RAW_CONNECTION"

echo
echo "  ############################################################"
echo "  #  GERI YUKLEME — hedefteki veriler SILINIP degistirilecek #"
echo "  ############################################################"
echo
echo "  Yedek : ${DUMP_FILE}"
echo "  Hedef : ${PG_TARGET_LABEL}"
echo

if [ "$CONFIRMED" != "true" ]; then
  echo "  Onay verilmedi. Yukaridaki hedefin dogru oldugunu dogrulayin ve komutu" >&2
  echo "  '--yes' ekleyerek tekrar calistirin." >&2
  echo >&2
  exit 1
fi

pg_use_runtime pg_restore

# Yedegin okunabilirligi, hedefe dokunmadan ONCE dogrulanir: bozuk bir dosya yuzunden
# once hedefi temizleyip sonra "arsiv okunamiyor" demek, veriyi silip yerine bir sey
# koyamamak demektir.
if ! pg_client pg_restore --list <"$DUMP_FILE" >/dev/null; then
  echo "HATA: yedek dosyasi okunamiyor (bozuk ya da custom format degil): ${DUMP_FILE}" >&2
  exit 1
fi

echo "==> Geri yukleniyor..."
# --single-transaction: ya hepsi ya hicbiri. Yarim geri yuklenmis bir veritabani,
#   hic geri yuklenmemis olandan daha tehlikelidir (kismi veri gercek sanilir).
# --clean --if-exists: var olan nesneler once dusurulur; yoklar icin hata uretmez.
# --no-owner --no-privileges: yedek baska bir rol adiyla alinmis olabilir (Render'da
#   uretim ve staging kullanicilari farklidir); sahiplik hedefteki role birakilir.
if ! pg_client pg_restore \
      -d "$PG_CONNINFO" \
      --clean --if-exists \
      --no-owner --no-privileges \
      --single-transaction \
      <"$DUMP_FILE"; then
  echo "HATA: pg_restore basarisiz. --single-transaction sayesinde hedef DEGISMEDI." >&2
  exit 1
fi

echo "==> Geri yukleme tamam: ${PG_TARGET_LABEL}"
echo "    Sonraki adim: uygulamayi baslatip /health/ready yesil mi dogrulayin."
