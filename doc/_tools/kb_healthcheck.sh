#!/usr/bin/env bash
# EğitimÜssü doc/ deterministik health-check (Faz 1). bash 3.2 uyumlu, saf grep/sed/awk/find.
# Kullanım: kb_healthcheck.sh [TARGET_DIR]   (varsayılan: script'in üstündeki doc/)
# Çıktı: SEVERITY<TAB>CHECK<TAB>file:line<TAB>message   (SEVERITY: RED|YELLOW|BLUE)
# Exit: RED bulgu varsa 1, yoksa 0.
set -u
LC_ALL=${LC_ALL:-en_US.UTF-8}; export LC_ALL

HERE="$(cd "$(dirname "$0")" && pwd)"
TARGET="${1:-$(cd "$HERE/.." && pwd)}"
red=0

emit() { # <sev> <check> <file:line> <msg>
  printf '%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$4"
  [ "$1" = "RED" ] && red=1
  return 0
}

# Taranacak md dosyaları (_tools hariç)
# NOT: dışlama TARGET'e GÖRELİ yoldan yapılır (mutlak yoldan değil) — yoksa
# TARGET bizzat doc/_tools/... altında bir yol olduğunda (ör. test runner
# fixtures'ı hedeflerken) her şey kendi kendini dışlardı.
md_files() {
  find "$TARGET" -name '*.md' | while IFS= read -r f; do
    rel="${f#$TARGET/}"
    case "$rel" in
      _tools/*|*/_tools/*) continue ;;
    esac
    printf '%s\n' "$f"
  done | sort
}

# NOT: her `cmd | while read ...; do emit ...; done` normalde while'ı bir
# alt-kabukta çalıştırır (pipe'ın sağ tarafı) — emit() içindeki `red=1` ataması
# üst-seviye $red'e asla yansımaz ve exit kodu her zaman 0 kalırdı. Bu yüzden
# tüm döngüler `while ...; done < <(cmd)` process-substitution biçimine
# çevrildi (bash 3.2'de de çalışır) — döngü mevcut kabukta kalır, $red doğru
# yansır. Brief'in orijinal kodundan tek kasıtlı sapma budur.

# 1) Kırık göreli md link
check_links() {
  while IFS= read -r f; do
    while IFS= read -r link; do
      [ -f "$(dirname "$f")/$link" ] || emit RED LINK "$f" "kırık link: $link"
    done < <(grep -oE '\]\(([^)#]+\.md)' "$f" | sed -E 's/^\]\(//')
  done < <(md_files)
}

# 2) Kapanmamış fence (tek sayıda ```)
check_fences() {
  while IFS= read -r f; do
    n=$(grep -c '^```' "$f")
    [ $((n % 2)) -ne 0 ] && emit RED FENCE "$f" "kapanmamış kod bloğu ($n fence)"
  done < <(md_files)
  return 0
}

# 3) Kanonik süpürme (kural-tanımı/çözüldü-notu beyaz-listede)
check_canonical() {
  while IFS= read -r f; do
    # EgittimUssu çift-t — "YANLIŞ/yanlış/çift-t" içeren tanım satırları hariç
    while IFS= read -r line; do emit RED CANONICAL "$f:${line%%:*}" "EgittimUssu çift-t"; done \
      < <(grep -nE 'EgittimUssu' "$f" | grep -vE 'YANLIŞ|yanlış|çift-t')
    # Yanlış .NET sürümü — D4/çözüldü/Düzeltildi/hizalandı notları hariç
    while IFS= read -r line; do emit YELLOW CANONICAL "$f:${line%%:*}" "şüpheli .NET sürümü"; done \
      < <(grep -nE '\.NET [0-8]([^0-9]|$)' "$f" | grep -vE 'D4|çözüldü|Düzeltildi|hizalandı')
  done < <(md_files)
  return 0
}

# 4) Frontmatter şema geçerliliği
check_frontmatter() {
  while IFS= read -r f; do
    if ! head -1 "$f" | grep -q '^---$'; then
      emit YELLOW FRONTMATTER "$f:1" "frontmatter yok"; continue
    fi
    fm=$(awk 'NR>1 && /^---$/{exit} NR>1{print}' "$f")
    for field in summary tags authority updated; do
      echo "$fm" | grep -qE "^$field:" || emit YELLOW FRONTMATTER "$f:1" "eksik alan: $field"
    done
    auth=$(echo "$fm" | grep -E '^authority:' | sed -E 's/^authority:[[:space:]]*//')
    if [ "$auth" = "code" ]; then
      echo "$fm" | grep -qE '^code_refs:' || emit YELLOW FRONTMATTER "$f:1" "authority: code ama code_refs yok"
    fi
  done < <(md_files)
  return 0
}

# 5) code_refs var olan dosyaya/glob'a işaret ediyor mu (repo kökünden)
check_code_refs() {
  ROOT="$(cd "$TARGET/.." && pwd)"
  while IFS= read -r f; do
    while IFS= read -r ref; do
      base="${ref%%\**}"; base="${base%/}"
      [ -z "$base" ] && continue
      if [ ! -e "$ROOT/$base" ] && ! ls -d "$ROOT/$ref" >/dev/null 2>&1; then
        emit YELLOW CODEREF "$f:1" "code_refs çözülmüyor: $ref"
      fi
    done < <(awk '/^code_refs:/{flag=1;next} /^[a-zA-Z_]+:/{flag=0} flag && /^[[:space:]]*-/{print}' "$f" \
      | sed -E 's/^[[:space:]]*-[[:space:]]*//')
  done < <(md_files)
  return 0
}

# 6) Gövde "Güncelleme:" ↔ frontmatter updated çelişkisi
check_dates() {
  while IFS= read -r f; do
    fu=$(awk 'NR>1 && /^---$/{exit} /^updated:/{print}' "$f" | sed -E 's/^updated:[[:space:]]*//')
    bu=$(grep -oE 'Güncelleme:[[:space:]]*[0-9]{4}-[0-9]{2}-[0-9]{2}' "$f" | head -1 | grep -oE '[0-9]{4}-[0-9]{2}-[0-9]{2}')
    [ -n "$fu" ] && [ -n "$bu" ] && [ "$fu" != "$bu" ] \
      && emit YELLOW DATE "$f:1" "frontmatter updated=$fu ≠ gövde Güncelleme=$bu"
  done < <(md_files)
  return 0
}

# 7) Öksüz doküman (INDEX.md'de linki yok) — yalnız INDEX varsa
check_orphans() {
  [ -f "$TARGET/INDEX.md" ] || return 0
  while IFS= read -r f; do
    rel="${f#$TARGET/}"
    case "$rel" in INDEX.md|_health/*|_arsiv/*) continue;; esac
    stem="$(basename "$rel" .md)"
    grep -q "$stem" "$TARGET/INDEX.md" || emit BLUE ORPHAN "$f" "INDEX.md'de referans yok"
  done < <(md_files)
  return 0
}

# 8) modules/mNN status ↔ INDEX satırı çelişkisi — yalnız INDEX varsa
check_status_index() {
  [ -f "$TARGET/INDEX.md" ] || return 0
  while IFS= read -r f; do
    st=$(awk 'NR>1 && /^---$/{exit} /^status:/{print}' "$f" | grep -oE '🟢|🟡|🔴' | head -1)
    [ -z "$st" ] && continue
    row=$(grep -F "$(basename "$f" .md)" "$TARGET/INDEX.md" | head -1)
    [ -z "$row" ] && continue
    if ! echo "$row" | grep -q "$st"; then
      emit YELLOW STATUS "$f:1" "frontmatter status=$st INDEX satırıyla çelişiyor"
    fi
  done < <(find "$TARGET/modules" -name 'm[0-9][0-9]_*.md' 2>/dev/null | sort)
  return 0
}

check_links
check_fences
check_canonical
check_frontmatter
check_code_refs
check_dates
check_orphans
check_status_index

exit $red
