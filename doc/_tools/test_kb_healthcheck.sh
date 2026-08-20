#!/usr/bin/env bash
# kb_healthcheck.sh için fixture-tabanlı test. Her beklenen bulguyu doğrular.
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
SCRIPT="$HERE/kb_healthcheck.sh"
FIX="$HERE/fixtures"
fail=0

assert_finds() { # <check> <file-substr> <human>
  # Aynı emit satırında hem CHECK hem dosya bulunur; tab'e bağlı kalmadan iki grep'le doğrula.
  if bash "$SCRIPT" "$FIX" | grep "$2" | grep -q "$1"; then
    echo "PASS: $3"
  else
    echo "FAIL: $3 (beklenen $1 bulgusu $2 için yok)"; fail=1
  fi
}
assert_clean() { # clean.md hiçbir bulguda görünmemeli
  if bash "$SCRIPT" "$FIX" | grep -q "clean.md"; then
    echo "FAIL: clean.md bulgu üretti"; fail=1
  else
    echo "PASS: clean.md temiz"
  fi
}

assert_finds "LINK"        "broken_link.md"   "kırık link yakalandı"
assert_finds "FENCE"       "unclosed_fence.md" "kapanmamış fence yakalandı"
assert_finds "CANONICAL"   "double_t.md"      "EgittimUssu çift-t yakalandı"
assert_finds "FRONTMATTER" "bad_frontmatter.md" "eksik frontmatter yakalandı"
assert_finds "DATE"        "date_conflict.md" "tarih çelişkisi yakalandı"

assert_finds "FRONTMATTER" "bad_reference_nosource.md" "reference source eksikliği yakalandı"
assert_finds "SOURCE" "bad_source_missing.md" "çözülmeyen source yakalandı"

# raw/ muafiyeti: raw altındaki dosya HİÇBİR bulguda görünmemeli
if bash "$SCRIPT" "$FIX" | grep -q "raw/verbatim"; then
  echo "FAIL: raw/ muaf değil (verbatim.md bulgu üretti)"; fail=1
else
  echo "PASS: raw/ muafiyeti çalışıyor"
fi

# good_reference temiz olmalı
if bash "$SCRIPT" "$FIX" | grep -q "good_reference"; then
  echo "FAIL: good_reference bulgu üretti"; fail=1
else
  echo "PASS: good_reference temiz"
fi

assert_clean

exit $fail
