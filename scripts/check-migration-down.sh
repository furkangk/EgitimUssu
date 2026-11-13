#!/usr/bin/env bash
# E-05 — Her migration geri alinabilir mi?
#
# Kural: Up() bir sey YAPIYORSA Down() de bir sey YAPMALI.
#   EF Core, geri alinamayan bir degisiklikte Down()'i "throw" ile uretir ya da bos birakir;
#   bos Down() ile uretime cikan bir migration, sorun cikardiginda geri alinamaz — tek cikis
#   yolu yedekten geri yuklemektir (doc/architecture/backup_restore.md).
#
# Up() BOS olan migration'lar mesru olarak muaftir: EF, yalniz model metadata'sini degistiren
# (ornegin ValueGeneratedNever) bir degisiklik icin sema uretmeden migration olusturur; boyle
# bir migration'in geri alacagi da yoktur.
#
# Kullanim:
#   scripts/check-migration-down.sh            # tum repo
#   scripts/check-migration-down.sh <dosya>... # yalniz verilen dosyalar
#
# Cikis: 0 = hepsi geri alinabilir · 1 = en az bir migration geri alinamaz
set -euo pipefail

cd "$(dirname "$0")/.."

if [ "$#" -gt 0 ]; then
  FILES="$*"
else
  FILES="$(find src -path '*/Migrations/*.cs' \
    ! -name '*.Designer.cs' ! -name '*ModelSnapshot.cs' | sort)"
fi

if [ -z "$FILES" ]; then
  echo "UYARI: hic migration dosyasi bulunamadi." >&2
  exit 0
fi

# Up()/Down() govdelerindeki ifade sayisini kuseli parantez derinligiyle sayar.
# Cikti: "<Up ifade> <Down ifade> <Down'da throw OLMAYAN ifade>"
# Ucuncu sayi neden ayri: EF, geri alinamayan bir islem icin Down()'i "throw new
# NotSupportedException(...)" olarak uretir. Govde bos degildir ama migration yine de geri
# alinamaz — ayni kusurun kilik degistirmis hali.
count_statements() {
  awk '
    function strip(l) {
      sub(/\/\/.*/, "", l)            # satir sonu yorumu
      gsub(/[{}]/, "", l)             # kuseli parantezler ifade degildir
      gsub(/^[ \t\r]+|[ \t\r]+$/, "", l)
      return l
    }
    /protected override void Up\(/   { method = "Up";   inside = 1; depth = 0; opened = 0; next }
    /protected override void Down\(/ { method = "Down"; inside = 1; depth = 0; opened = 0; next }
    inside {
      opens  = gsub(/\{/, "{")
      closes = gsub(/\}/, "}")
      if (opens > 0) opened = 1
      body = strip($0)
      if (opened && depth + opens > closes && body != "") {
        count[method]++
        if (method == "Down" && body !~ /^throw[ (]/) nonthrow++
      }
      depth += opens - closes
      if (opened && depth <= 0) { inside = 0 }
    }
    END { printf "%d %d %d\n", count["Up"] + 0, count["Down"] + 0, nonthrow + 0 }
  ' "$1"
}

FAILED=0
CHECKED=0
NOOP=0

for file in $FILES; do
  [ -f "$file" ] || continue
  CHECKED=$((CHECKED + 1))
  read -r up down nonthrow <<EOF
$(count_statements "$file")
EOF

  if [ "$up" -eq 0 ]; then
    NOOP=$((NOOP + 1))
    continue
  fi
  if [ "$down" -eq 0 ]; then
    echo "::error file=${file}::Down() bos — bu migration geri alinamaz. Up() ${up} ifade iceriyor."
    echo "HATA: ${file} — Up() dolu (${up} ifade) ama Down() bos." >&2
    FAILED=$((FAILED + 1))
  elif [ "$nonthrow" -eq 0 ]; then
    echo "::error file=${file}::Down() yalniz 'throw' iciyor — bu migration geri alinamaz."
    echo "HATA: ${file} — Down() yalniz 'throw' iceriyor; geri alinamaz." >&2
    FAILED=$((FAILED + 1))
  fi
done

if [ "$FAILED" -gt 0 ]; then
  echo >&2
  echo "${FAILED} migration geri alinamaz durumda. Down() metodunu doldurun:" >&2
  echo "  - Tabloyu/kolonu geri dusuren islemi yazin, ya da" >&2
  echo "  - Gercekten geri alinamiyorsa Up()'i bolun; veri kaybi olan adimi ayri bir" >&2
  echo "    migration'a alip prosedurunu doc/architecture/backup_restore.md'ye yazin." >&2
  exit 1
fi

echo "Migration geri alma kontrolu: ${CHECKED} migration incelendi, hepsi geri alinabilir (${NOOP} tanesi sema degistirmiyor)."
