#!/usr/bin/env bash
# E-05 — Postgres istemci araclari icin ortak baglanti yardimcilari.
# backup-db.sh ve restore-db.sh bu dosyayi "source" eder; tek basina calistirilmaz.
#
# Iki problemi cozer:
#
# 1) BAGLANTI BICIMI. Repo genelinde baglanti dizesi Npgsql anahtar-deger bicimindedir
#    ("Host=...;Port=...;Database=...;Username=...;Password=..."), pg_dump ise libpq
#    bicimini bekler ve Npgsql bicimini anlamaz. Burada cevrilir; zaten libpq conninfo
#    ya da postgres:// URI'si verilmisse oldugu gibi birakilir.
#    Parola conninfo'dan CIKARILIP PGPASSWORD'e alinir: komut satirinda kalsaydi
#    makinedeki her kullaniciya `ps` ciktisinda gorunurdu.
#
# 2) ISTEMCI SURUMU. pg_dump, kendisinden YENI surumlu bir sunucuyu dokumlemeyi reddeder
#    ("server version mismatch") ve yarim yedek birakmaz — hic yedek birakmaz. PATH'te
#    araci varsa o kullanilir; yoksa postgres:${PG_IMAGE_TAG} imajindan calistirilir.
#    Yerelde Postgres kurulu olmayan bir makinede (ornegin bu repo'nun gelistirildigi
#    MacBook) yedek almanin tek yolu budur.

PG_IMAGE_TAG="${PG_IMAGE_TAG:-17}"

# --- 1) Baglanti dizesini cozumle -------------------------------------------------
# Girdi: ham baglanti dizesi (Npgsql / libpq conninfo / postgres:// URI).
# Cikti (global): PG_CONNINFO (parolasiz), PGPASSWORD, PG_DB_NAME, PG_TARGET_LABEL.
pg_parse_connection() {
  local raw="${1:-}"
  if [ -z "$raw" ]; then
    echo "HATA: baglanti dizesi bos." >&2
    return 1
  fi

  PG_CONNINFO=""
  PG_DB_NAME=""
  PG_TARGET_LABEL=""

  case "$raw" in
    postgres://*|postgresql://*)
      # URI'yi libpq zaten anliyor; parolayi ayirmaya calismak yuzde-kodlamasini
      # cozmeyi gerektirirdi. Oldugu gibi gecirilir, yalniz ekrana maskeli yazilir.
      PG_CONNINFO="$raw"
      PG_DB_NAME="$(printf '%s' "$raw" | sed -E 's#^[^/]*//[^/]*/##; s#\?.*$##')"
      PG_TARGET_LABEL="$(printf '%s' "$raw" | sed -E 's#^(postgres(ql)?://)[^@/]*@#\1***:***@#')"
      return 0
      ;;
  esac

  local host="" port="" db="" user="" sslmode="" pass=""
  local pair key value
  local old_ifs="$IFS"
  IFS=';'
  for pair in $raw; do
    IFS="$old_ifs"
    case "$pair" in
      *=*) ;;
      *) continue ;;
    esac
    key="$(printf '%s' "${pair%%=*}" | tr -d ' ' | tr '[:upper:]' '[:lower:]')"
    value="${pair#*=}"
    case "$key" in
      host|server)          host="$value" ;;
      port)                 port="$value" ;;
      database|dbname)      db="$value" ;;
      username|user|userid) user="$value" ;;
      password|pwd)         pass="$value" ;;
      sslmode)              sslmode="$(printf '%s' "$value" | tr '[:upper:]' '[:lower:]')" ;;
    esac
    IFS=';'
  done
  IFS="$old_ifs"

  if [ -z "$host" ] || [ -z "$db" ]; then
    echo "HATA: baglanti dizesi cozumlenemedi (Host ve Database zorunlu): ${raw%%;*}..." >&2
    return 1
  fi

  PG_CONNINFO="host=$(pg_quote "$host") dbname=$(pg_quote "$db")"
  [ -n "$port" ] && PG_CONNINFO="$PG_CONNINFO port=$(pg_quote "$port")"
  [ -n "$user" ] && PG_CONNINFO="$PG_CONNINFO user=$(pg_quote "$user")"
  [ -n "$sslmode" ] && PG_CONNINFO="$PG_CONNINFO sslmode=$(pg_quote "$sslmode")"

  if [ -n "$pass" ]; then
    PGPASSWORD="$pass"
    export PGPASSWORD
  fi

  PG_DB_NAME="$db"
  PG_TARGET_LABEL="${user:-?}@${host}:${port:-5432}/${db}"
  return 0
}

# libpq conninfo degerini tek tirnaga alir (bosluk ve tirnak icerebilir).
pg_quote() {
  printf "'%s'" "$(printf '%s' "$1" | sed "s/\\\\/\\\\\\\\/g; s/'/\\\\'/g")"
}

# --- 2) Calisma bicimini sec ------------------------------------------------------
# Girdi: arac adi (pg_dump / pg_restore / psql).
# Cikti (global): PG_RUNTIME (native|docker), gerekiyorsa yeniden yazilmis PG_CONNINFO.
#
# Arsiv dosyasi konteynere BAGLANMAZ; stdin/stdout uzerinden akitilir. Bind mount
# denendiginde Docker Desktop paylasilmayan bir host dizinine sessizce bos bir
# baglama yapiyor: pg_dump konteynerin icinde basariyla yaziyor, host'ta dosya
# olusmuyor ve script "yedek alindi" diyordu. Akis bicimi hem bu tuzagi hem de
# dosyanin root'a ait olmasi sorununu ortadan kaldirir.
pg_use_runtime() {
  local tool="$1"

  if command -v "$tool" >/dev/null 2>&1; then
    PG_RUNTIME="native"
    return 0
  fi

  if ! command -v docker >/dev/null 2>&1; then
    echo "HATA: '$tool' PATH'te yok ve docker da bulunamadi." >&2
    echo "      Cozum: PostgreSQL istemcilerini kurun (brew install libpq) ya da Docker'i baslatin." >&2
    return 1
  fi
  if ! docker info >/dev/null 2>&1; then
    echo "HATA: '$tool' PATH'te yok ve Docker calismiyor." >&2
    return 1
  fi

  PG_RUNTIME="docker"
  # Konteynerin icinde 'localhost' konteynerin KENDISIDIR; host makineye yonlendir.
  PG_CONNINFO="$(printf '%s' "$PG_CONNINFO" \
    | sed -E "s/(host=')(localhost|127\.0\.0\.1)(')/\1host.docker.internal\3/g" \
    | sed -E "s#(postgres(ql)?://[^@/]*@)(localhost|127\.0\.0\.1)([:/])#\1host.docker.internal\4#g")"
  echo "==> '$tool' yerelde yok; postgres:${PG_IMAGE_TAG} imajindan calistirilacak." >&2
  return 0
}

# Secilen bicimde Postgres istemci aracini calistirir. Arsiv stdin/stdout ile akar.
pg_client() {
  local tool="$1"; shift

  if [ "${PG_RUNTIME:-native}" = "native" ]; then
    "$tool" "$@"
    return $?
  fi

  # -i: stdin konteynere baglanir. -t VERILMEZ; TTY stdout'a kontrol karakteri karistirip
  # arsivi bozardi.
  docker run --rm -i \
    --add-host=host.docker.internal:host-gateway \
    -e PGPASSWORD \
    --entrypoint "$tool" \
    "postgres:${PG_IMAGE_TAG}" "$@"
}
