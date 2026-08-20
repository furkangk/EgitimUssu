# Bilgi Tabanı Makinesi — Dilim A (Temel + Health-check) Uygulama Planı

> **Agentic worker için:** GEREKLİ ALT-SKILL: Bu planı görev görev uygulamak için superpowers:subagent-driven-development (önerilen) veya superpowers:executing-plans kullan. Adımlar takip için checkbox (`- [ ]`) kullanır.

**Goal:** `doc/`'a makine-okunur frontmatter konvansiyonu + koddan-sapmayı/biçim-bozukluğunu/kanonik-ihlali otomatik yakalayan `/kb-healthcheck` aracı ekle.

**Architecture:** Ajan = Claude Code. Makine = saf-bash deterministik script (`doc/_tools/kb_healthcheck.sh`, Faz 1) + Claude Code slash-komutu (`.claude/commands/kb-healthcheck.md`, orkestrasyon + `--deep` LLM drift fan-out) + tüm `doc/**/*.md`'de YAML frontmatter. Yeni dil/runtime bağımlılığı yok.

**Tech Stack:** Markdown, YAML frontmatter, bash 3.2 (grep/sed/awk/find), git, Claude Code slash-komutu.

## Global Constraints

- **Substrat:** Yalnızca skill/slash-komut + markdown konvansiyon + saf-bash. Yeni dil/runtime/paket bağımlılığı **eklenmez**.
- **bash 3.2 uyumu (macOS varsayılanı):** Associative array (`declare -A`), `mapfile`/`readarray`, `${var,,}` **kullanılmaz**. POSIX-uyumlu döngü/`while read` kullan.
- **Doğruluk hiyerarşisi (çelişkide):** 1) gerçek kod (`src/Modules/`, `mobile/lib/`) → 2) INDEX §0 kanonik gerçekler → 3) PRD v2.1.
- **Kanonik gerçekler:** görünen ad **EğitimÜssü**, kod adı **EgitimUssu** (`EgittimUssu` çift-t YANLIŞ), backend **.NET 9**, ana renk **`0xFF082B4F`**, DB PostgreSQL modül-başına-şema + Redis, PRD **v2.1**.
- **Migrasyon yalnız frontmatter ekler:** gövde içeriği/anlamı değişmez; mevcut `> **Güncelleme:**`/`> **Durum:**` blokları korunur.
- **`updated` alanı** düzenlenen dosyada **2026-08-20**; migrasyonda mevcut gövde `Güncelleme:` tarihini frontmatter'a taşı (gövdedekini değiştirme).
- **Fixtures ve `_tools/` gerçek taramadan dışlanır** (bilerek bozuk test verisi).
- Her görev kendi commit'i; commit mesajı sonunda `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- **Authority değerleri:** `code` (koda karşı drift) · `product` (plan, kod yok) · `derived` (başka dokümandan türer) · `archive` (`_arsiv/`, atlanır).

## Modül → klasör eşlemesi (code_refs için)

m01=Identity, m02=Teachers, m03=Students, m04=Scheduling, m05=LessonSessions, m06=Assignments, m07=Payments, m08=Study, m09=Parents, m10=ProgressTracking, m11=Notifications, m12=Matching, m13=Reviews, m14=Reporting, m15=Settings. **m16=Messaging, m17=Membership, m18=Feedback → backend klasörü YOK** (authority: product, code_refs boş).

---

## Task 1: Frontmatter konvansiyon dokümanı

**Files:**
- Create: `doc/00_kb_konvansiyon.md`

**Interfaces:**
- Produces: Frontmatter şeması (alanlar + authority kuralları + `code_refs` aile kalıpları). Sonraki tüm görevler (migrasyon + health-check) buna referans verir.

- [ ] **Step 1: Konvansiyon dokümanını yaz**

`doc/00_kb_konvansiyon.md` oluştur:

````markdown
---
title: "Bilgi Tabanı Frontmatter Konvansiyonu"
summary: "doc/ altındaki her markdown'ın makine-okunur frontmatter şeması, authority kuralları ve code_refs aile kalıpları"
tags: [kb, konvansiyon, meta]
authority: derived
updated: 2026-08-20
---

# 📐 Bilgi Tabanı Frontmatter Konvansiyonu

> Her `doc/**/*.md` dosyasının başında YAML frontmatter bulunur. Gövdedeki insan-okunur
> bloklar (`> **Güncelleme:**`, `> **Durum:**`) **korunur**; frontmatter makine-otoritesidir.
> `/kb-healthcheck` bu şemaya göre denetler.

## Şema

| Alan | Zorunlu | Rol |
|------|---------|-----|
| `title` | hayır | İnsan başlığı; yoksa H1 kullanılır |
| `summary` | evet | Tek satır özet (INDEX/Obsidian/ingest kullanır) |
| `tags` | evet (≥1) | kebab-case; Obsidian graph + filtreleme |
| `status` | koşullu | Modül/ekran/rol dokümanlarında 🟢/🟡/🔴 |
| `authority` | evet | `code` \| `product` \| `derived` \| `archive` |
| `code_refs` | koşullu | `authority: code` ise ≥1 glob; diğerlerinde boş/atlanır |
| `updated` | evet | ISO tarih (YYYY-MM-DD) |

## authority değerleri

- **code** — Kod doğruluk kaynağı; `code_refs`'e karşı endpoint/enum/domain drift denetlenir.
- **product** — Ürün niyeti/plan; kod karşılığı yok, drift denetlenmez (PRD, yol haritası, planlanan modüller m16–m18).
- **derived** — Başka dokümandan türer; dolaylı tutarlılık (roller, index, denetim, rehber dokümanlar).
- **archive** — `doc/_arsiv/*`; drift/lint atlanır (yalnız kırık link + fence).

## code_refs aile kalıpları

| Aile | Kalıp |
|------|-------|
| `modules/mNN_<ad>.md` | `src/Modules/<Ad>/**` |
| `modules/00_genel_bakis.md` | `src/Modules/*/API/*Module.cs` |
| `modules/veri_modeli.md` | `src/Modules/*/Domain/**` |
| `architecture/backend.md` | `src/**` |
| `architecture/mobile_flutter.md` | `mobile/lib/**` |
| `architecture/widgets.md` | `mobile/lib/shared/widgets/**` |
| `pages/*.md` | `mobile/lib/features/**/presentation/pages/*.dart` + `mobile/lib/core/routing/app_router.dart` |
| `roles/*.md`, index, rehber | (derived — code_refs boş) |
| PRD, yol_haritasi, m16–m18 | (product — code_refs boş) |
| `_arsiv/*` | (archive — code_refs boş) |

*Güncelleme: 2026-08-20*
````

- [ ] **Step 2: Commit**

```bash
git add doc/00_kb_konvansiyon.md
git commit -m "docs(kb): Dilim A — frontmatter konvansiyon dokümanı

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: `kb_healthcheck.sh` deterministik Faz 1 + test fixture'ları

**Files:**
- Create: `doc/_tools/kb_healthcheck.sh`
- Create: `doc/_tools/test_kb_healthcheck.sh`
- Create: `doc/_tools/fixtures/clean.md`, `broken_link.md`, `unclosed_fence.md`, `double_t.md`, `bad_frontmatter.md`, `date_conflict.md`
- Create: `doc/_health/.gitkeep`

**Interfaces:**
- Produces: `kb_healthcheck.sh [TARGET_DIR]` (varsayılan `doc`). Çıktı satır formatı: `SEVERITY<TAB>CHECK<TAB>file:line<TAB>message`. `SEVERITY` ∈ `RED|YELLOW|BLUE`. Exit kodu: RED bulgu varsa 1, yoksa 0. `_tools/` her zaman taramadan dışlanır. INDEX'e bağlı kontroller (orphan, status) yalnız `TARGET_DIR/INDEX.md` varsa çalışır.

- [ ] **Step 1: Test fixture'larını oluştur (kırmızı kanıtlar)**

`doc/_tools/fixtures/clean.md` (geçerli, sıfır bulgu vermeli):
```markdown
---
title: "Temiz Fixture"
summary: "Health-check testinde sıfır bulgu vermesi gereken geçerli örnek"
tags: [fixture, temiz]
authority: derived
updated: 2026-08-20
---

# Temiz Fixture

Gövde metni. Kırık link yok, fence dengeli.

*Güncelleme: 2026-08-20*
```

`doc/_tools/fixtures/broken_link.md`:
```markdown
---
title: "Kırık Link Fixture"
summary: "Var olmayan bir dosyaya link içeren örnek"
tags: [fixture]
authority: derived
updated: 2026-08-20
---

# Kırık Link

Bkz. [yok](yok_boyle_bir_dosya.md).
```

`doc/_tools/fixtures/unclosed_fence.md`:
````markdown
---
title: "Kapanmamış Fence Fixture"
summary: "Tek sayıda kod bloğu işareti içeren örnek"
tags: [fixture]
authority: derived
updated: 2026-08-20
---

# Kapanmamış Fence

```bash
echo "kapanmadı"
````

`doc/_tools/fixtures/double_t.md` (⚠️ gövde satırı `yanlış`/`YANLIŞ`/`çift-t` kelimelerini **içermemeli** — yoksa script beyaz-listeye alıp atlar):
```markdown
---
title: "Çift-t Fixture"
summary: "Çift-t yazım hatası içeren örnek"
tags: [fixture]
authority: derived
updated: 2026-08-20
---

# Çift-t

Proje adı EgittimUssu.
```

`doc/_tools/fixtures/bad_frontmatter.md` (zorunlu `summary` + `tags` eksik):
```markdown
---
title: "Eksik Frontmatter"
authority: derived
updated: 2026-08-20
---

# Eksik Frontmatter

summary ve tags alanları yok.
```

`doc/_tools/fixtures/date_conflict.md` (frontmatter `updated` ≠ gövde `Güncelleme:`):
```markdown
---
title: "Tarih Çelişkisi"
summary: "Frontmatter updated ile gövde Güncelleme tarihi çelişen örnek"
tags: [fixture]
authority: derived
updated: 2026-08-20
---

# Tarih Çelişkisi

*Güncelleme: 2026-01-01*
```

- [ ] **Step 2: Test runner'ı yaz**

`doc/_tools/test_kb_healthcheck.sh`:
```bash
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
assert_clean

exit $fail
```

- [ ] **Step 3: Testi çalıştır — başarısız olduğunu doğrula**

Run:
```bash
chmod +x doc/_tools/test_kb_healthcheck.sh
bash doc/_tools/test_kb_healthcheck.sh
```
Expected: FAIL satırları (script henüz yok → `kb_healthcheck.sh: No such file` veya boş çıktı). En az bir `FAIL:` görünür.

- [ ] **Step 4: `kb_healthcheck.sh`'i yaz (bash 3.2 uyumlu)**

`doc/_tools/kb_healthcheck.sh`:
```bash
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
md_files() {
  find "$TARGET" -name '*.md' | grep -v "/_tools/" | sort
}

# 1) Kırık göreli md link
check_links() {
  md_files | while IFS= read -r f; do
    grep -oE '\]\(([^)#]+\.md)' "$f" | sed -E 's/^\]\(//' | while IFS= read -r link; do
      [ -f "$(dirname "$f")/$link" ] || emit RED LINK "$f" "kırık link: $link"
    done
  done
}

# 2) Kapanmamış fence (tek sayıda ```)
check_fences() {
  md_files | while IFS= read -r f; do
    n=$(grep -c '^```' "$f")
    [ $((n % 2)) -ne 0 ] && emit RED FENCE "$f" "kapanmamış kod bloğu ($n fence)"
  done
  return 0
}

# 3) Kanonik süpürme (kural-tanımı/çözüldü-notu beyaz-listede)
check_canonical() {
  md_files | while IFS= read -r f; do
    # EgittimUssu çift-t — "YANLIŞ/yanlış/çift-t" içeren tanım satırları hariç
    grep -nE 'EgittimUssu' "$f" | grep -vE 'YANLIŞ|yanlış|çift-t' \
      | while IFS= read -r line; do emit RED CANONICAL "$f:${line%%:*}" "EgittimUssu çift-t"; done
    # Yanlış .NET sürümü — D4/çözüldü/Düzeltildi/hizalandı notları hariç
    grep -nE '\.NET [0-8]([^0-9]|$)' "$f" | grep -vE 'D4|çözüldü|Düzeltildi|hizalandı' \
      | while IFS= read -r line; do emit YELLOW CANONICAL "$f:${line%%:*}" "şüpheli .NET sürümü"; done
  done
  return 0
}

# 4) Frontmatter şema geçerliliği
check_frontmatter() {
  md_files | while IFS= read -r f; do
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
  done
  return 0
}

# 5) code_refs var olan dosyaya/glob'a işaret ediyor mu (repo kökünden)
check_code_refs() {
  ROOT="$(cd "$TARGET/.." && pwd)"
  md_files | while IFS= read -r f; do
    awk '/^code_refs:/{flag=1;next} /^[a-zA-Z_]+:/{flag=0} flag && /^[[:space:]]*-/{print}' "$f" \
      | sed -E 's/^[[:space:]]*-[[:space:]]*//' | while IFS= read -r ref; do
        base="${ref%%\**}"; base="${base%/}"
        [ -z "$base" ] && continue
        if [ ! -e "$ROOT/$base" ] && ! ls -d "$ROOT/$ref" >/dev/null 2>&1; then
          emit YELLOW CODEREF "$f:1" "code_refs çözülmüyor: $ref"
        fi
      done
  done
  return 0
}

# 6) Gövde "Güncelleme:" ↔ frontmatter updated çelişkisi
check_dates() {
  md_files | while IFS= read -r f; do
    fu=$(awk 'NR>1 && /^---$/{exit} /^updated:/{print}' "$f" | sed -E 's/^updated:[[:space:]]*//')
    bu=$(grep -oE 'Güncelleme:[[:space:]]*[0-9]{4}-[0-9]{2}-[0-9]{2}' "$f" | head -1 | grep -oE '[0-9]{4}-[0-9]{2}-[0-9]{2}')
    [ -n "$fu" ] && [ -n "$bu" ] && [ "$fu" != "$bu" ] \
      && emit YELLOW DATE "$f:1" "frontmatter updated=$fu ≠ gövde Güncelleme=$bu"
  done
  return 0
}

# 7) Öksüz doküman (INDEX.md'de linki yok) — yalnız INDEX varsa
check_orphans() {
  [ -f "$TARGET/INDEX.md" ] || return 0
  md_files | while IFS= read -r f; do
    rel="${f#$TARGET/}"
    case "$rel" in INDEX.md|_health/*|_arsiv/*) continue;; esac
    stem="$(basename "$rel" .md)"
    grep -q "$stem" "$TARGET/INDEX.md" || emit BLUE ORPHAN "$f" "INDEX.md'de referans yok"
  done
  return 0
}

# 8) modules/mNN status ↔ INDEX satırı çelişkisi — yalnız INDEX varsa
check_status_index() {
  [ -f "$TARGET/INDEX.md" ] || return 0
  find "$TARGET/modules" -name 'm[0-9][0-9]_*.md' 2>/dev/null | sort | while IFS= read -r f; do
    st=$(awk 'NR>1 && /^---$/{exit} /^status:/{print}' "$f" | grep -oE '🟢|🟡|🔴' | head -1)
    [ -z "$st" ] && continue
    row=$(grep -F "$(basename "$f" .md)" "$TARGET/INDEX.md" | head -1)
    [ -z "$row" ] && continue
    if ! echo "$row" | grep -q "$st"; then
      emit YELLOW STATUS "$f:1" "frontmatter status=$st INDEX satırıyla çelişiyor"
    fi
  done
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
```

- [ ] **Step 5: Testi çalıştır — geçtiğini doğrula**

Run:
```bash
chmod +x doc/_tools/kb_healthcheck.sh
bash doc/_tools/test_kb_healthcheck.sh
```
Expected: Tüm satırlar `PASS:`; exit 0.

- [ ] **Step 6: Gerçek `doc/`'ta çalıştır — biçim/kanonik yeşil, frontmatter eksik (beklenen)**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | awk -F'\t' '{print $1, $2}' | sort | uniq -c
```
Expected: `LINK`/`FENCE`/`CANONICAL` bulgusu **yok** (Faz 1 temizliği yeşil); çok sayıda `YELLOW FRONTMATTER "frontmatter yok"` (henüz migrasyon yapılmadı — sonraki görevler düzeltir). Bunu not al.

- [ ] **Step 7: `_health/` çıktı klasörü + commit**

```bash
mkdir -p doc/_health && touch doc/_health/.gitkeep
git add doc/_tools doc/_health/.gitkeep
git commit -m "feat(kb): Dilim A — deterministik health-check script + fixture testleri

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Frontmatter migrasyonu — modules/ (21 dosya)

**Files:**
- Modify: `doc/modules/m01_identity.md` … `m18_feedback.md` (18), `doc/modules/00_genel_bakis.md`, `doc/modules/mimari_inceleme.md`, `doc/modules/veri_modeli.md`

**Interfaces:**
- Consumes: Task 1 şeması.
- Produces: Tüm modül dokümanlarında geçerli frontmatter (Task 2 script'i `modules/` için yeşil frontmatter kontrolü verir).

**Değerler (deterministik — her dosya için birebir):**

| Dosya | authority | code_refs | status |
|-------|-----------|-----------|--------|
| m01_identity | code | `src/Modules/Identity/**` | 🟢 |
| m02_teachers | code | `src/Modules/Teachers/**` | 🟢 |
| m03_students | code | `src/Modules/Students/**` | 🟢/🟡 |
| m04_scheduling | code | `src/Modules/Scheduling/**` | 🟢 |
| m05_lesson_sessions | code | `src/Modules/LessonSessions/**` | 🟢 |
| m06_assignments | code | `src/Modules/Assignments/**` | 🟢 |
| m07_payments | code | `src/Modules/Payments/**` | 🟢 |
| m08_study | code | `src/Modules/Study/**` | 🟢 |
| m09_parents | code | `src/Modules/Parents/**` | 🟢 |
| m10_progress_tracking | code | `src/Modules/ProgressTracking/**` | 🟡 |
| m11_notifications | code | `src/Modules/Notifications/**` | 🟡 |
| m12_matching | code | `src/Modules/Matching/**` | 🔴 |
| m13_reviews | code | `src/Modules/Reviews/**` | 🔴 |
| m14_reporting | code | `src/Modules/Reporting/**` | 🔴 |
| m15_settings | code | `src/Modules/Settings/**` | 🟡 |
| m16_messaging | product | _(boş)_ | 🔴 |
| m17_membership | product | _(boş)_ | 🔴 |
| m18_feedback | product | _(boş)_ | 🔴 |
| 00_genel_bakis | code | `src/Modules/*/API/*Module.cs` | _(yok)_ |
| mimari_inceleme | derived | _(boş)_ | _(yok)_ |
| veri_modeli | code | `src/Modules/*/Domain/**` | _(yok)_ |

**`summary` + `tags` kuralı (dosya-başı, kaynaktan üret):**
- `summary` = dosyanın H1 + ilk "Amaç/özet" cümlesinden damıtılmış **tek satır** (≤160 karakter), modülün ne olduğunu + mevcut durumu söyler.
- `tags` = `[modul, <klasör-adı-küçük>, <2-3 konu>]` + varsa `faz-N`. Örn m15 → `[modul, settings, gizlilik, bildirim, faz-0]`.
- `updated` = dosyanın gövdesindeki mevcut son `Güncelleme:` tarihi (Faz 1'den sonra çoğu **2026-08-19**); gövdeyi değiştirme.
- `title` = kısa insan başlığı (örn `"M15 — Ayarlar & Güvenlik"`).

- [ ] **Step 1: İki tam örnek (kalıbı göster)**

`doc/modules/m15_settings.md` en başına ekle (H1'den önce):
```yaml
---
title: "M15 — Ayarlar & Güvenlik"
summary: "Kullanıcı bildirim/gizlilik/güvenlik ayarları; study-sharing endpoint + CQRS + sahiplik authorizer mevcut, tam CRUD eksik"
tags: [modul, settings, gizlilik, bildirim, faz-0]
status: "🟡"
authority: code
code_refs:
  - src/Modules/Settings/**
updated: 2026-08-19
---
```

`doc/modules/m16_messaging.md` en başına ekle:
```yaml
---
title: "M16 — Mesajlaşma"
summary: "Planlanan mesajlaşma modülü; backend klasörü henüz yok (Faz 2-3, tüm domain önerilen)"
tags: [modul, messaging, planlanan, faz-2, faz-3]
status: "🔴"
authority: product
updated: 2026-08-19
---
```

- [ ] **Step 2: Kalan 19 modül dosyasına frontmatter ekle**

Yukarıdaki tabloya göre her dosyanın H1'inden önce frontmatter bloğu ekle. `summary`/`tags`/`title`'ı yukarıdaki kurala göre dosyayı okuyarak üret; `authority`/`code_refs`/`status`/`updated`'i tablodan/gövdeden al. Gövde içeriğine dokunma.

- [ ] **Step 3: Doğrula — modules/ frontmatter yeşil**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | grep "/modules/" | grep -E "FRONTMATTER|CODEREF"
```
Expected: **Boş** (modül dosyalarında eksik frontmatter / kırık code_refs yok).

- [ ] **Step 4: Commit**

```bash
git add doc/modules
git commit -m "docs(kb): Dilim A — modules/ frontmatter migrasyonu (21 dosya)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Frontmatter migrasyonu — architecture/ (11 dosya)

**Files:**
- Modify: `doc/architecture/{00_genel_bakis,accessibility,animations,anti_patterns,backend,design_system,figma_references,mobile_flutter,ux_rules,web_angular,widgets}.md`

**Interfaces:**
- Consumes: Task 1 şeması.
- Produces: architecture/ frontmatter yeşil.

**Değerler:**

| Dosya | authority | code_refs |
|-------|-----------|-----------|
| 00_genel_bakis | derived | _(boş)_ |
| backend | code | `src/**` |
| mobile_flutter | code | `mobile/lib/**` |
| widgets | code | `mobile/lib/shared/widgets/**` |
| web_angular | product | _(boş)_ |
| design_system | derived | _(boş)_ |
| ux_rules | derived | _(boş)_ |
| animations | derived | _(boş)_ |
| accessibility | derived | _(boş)_ |
| anti_patterns | derived | _(boş)_ |
| figma_references | derived | _(boş)_ |

`summary`/`tags`/`title`/`updated` kuralı Task 3 ile aynı (gövdeden üret; `updated` = gövde `Güncelleme:`). `tags` ilk etiketi `mimari`.

- [ ] **Step 1: Bir tam örnek**

`doc/architecture/backend.md` en başına:
```yaml
---
title: "Backend Mimari (.NET 9 modüler monolit)"
summary: "Çözüm yapısı, modül anatomisi, Shared/Kernel, CQRS, Outbox, persistence, JWT — 15 gerçek modül"
tags: [mimari, backend, dotnet, cqrs, outbox]
authority: code
code_refs:
  - src/**
updated: 2026-06-24
---
```

- [ ] **Step 2: Kalan 10 architecture dosyasına ekle**

Tabloya göre frontmatter ekle; `summary`/`tags`/`title`/`updated` her dosyadan üretilir. Gövdeye dokunma.

- [ ] **Step 3: Doğrula**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | grep "/architecture/" | grep -E "FRONTMATTER|CODEREF"
```
Expected: **Boş**.

- [ ] **Step 4: Commit**

```bash
git add doc/architecture
git commit -m "docs(kb): Dilim A — architecture/ frontmatter migrasyonu (11 dosya)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Frontmatter migrasyonu — pages/ (21 dosya)

**Files:**
- Modify: `doc/pages/00_pages_index.md` + 20 sayfa md'si (`account_info`, `assignment_follow_up`, `auth_login`, `auth_register`, `auth_role_selection`, `auth_welcome`, `dashboard`, `dashboard_preview`, `lesson_detail`, `lesson_note_form`, `lesson_note_view`, `lesson_sessions_list`, `more`, `payment_form`, `payments_list`, `scheduling`, `students_detail`, `students_list`, `study_student`, `teacher_profile`)

**Interfaces:**
- Consumes: Task 1 şeması.
- Produces: pages/ frontmatter yeşil.

**Değerler:**
- `00_pages_index.md` → authority: `derived`, code_refs boş, tags ilk etiket `sayfa-index`.
- Diğer 20 sayfa md'si → authority: `code`, `code_refs: [mobile/lib/features/**/presentation/pages/*.dart, mobile/lib/core/routing/app_router.dart]`, `status` = ilgili index satırındaki veri rozeti (🟢/🟡/🔴 → 🟢/🟡/🔴), `tags` ilk etiket `sayfa`.
- `updated` = gövde `Güncelleme:` (varsa) yoksa `2026-08-19`.

- [ ] **Step 1: Bir tam örnek**

`doc/pages/payment_form.md` en başına:
```yaml
---
title: "Ödeme Ekle Ekranı"
summary: "Öğretmenin ödeme kaydı oluşturduğu form ekranı; gerçek /api/payments backend'ine bağlı"
tags: [sayfa, payments, form, ogretmen]
status: "🟢"
authority: code
code_refs:
  - mobile/lib/features/payments/presentation/pages/payment_form_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-08-19
---
```

- [ ] **Step 2: Kalan 20 pages dosyasına ekle**

Her sayfa md'sinin `code_refs`'inde ilgili spesifik `*_page.dart` yolu + `app_router.dart`. `00_pages_index.md` derived. `summary`/`tags`/`status` dosyadan/indexten üretilir.

- [ ] **Step 3: Doğrula**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | grep "/pages/" | grep -E "FRONTMATTER|CODEREF"
```
Expected: **Boş**.

- [ ] **Step 4: Commit**

```bash
git add doc/pages
git commit -m "docs(kb): Dilim A — pages/ frontmatter migrasyonu (21 dosya)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: Frontmatter migrasyonu — roles/ + diagrams/ + kök/ürün/arşiv (20 dosya)

**Files:**
- Modify: `doc/roles/{00_roller_genel_bakis,admin,ogrenci,ogrenci_ux,ogretmen,veli}.md` (6)
- Modify: `doc/diagrams/is_akislari/README.md`, `doc/diagrams/rol_sayfa_mimarisi/{README,ogrenci,ogretmen,veli}.md` (5)
- Modify: `doc/INDEX.md`, `doc/ozel_ders_platformu_PRD_v2.md`, `doc/yol_haritasi.md`, `doc/tab_widget.md`, `doc/denetim/2026-06-30_kapsamli_kod_denetimi.md` (5)
- Modify: `doc/_arsiv/{is_akislari,ogrenci_rolu_fonksiyonel_dokuman_v1,ogretmen_rolu_fonksiyonel_dokuman_v1,veli_rolu_fonksiyonel_dokuman_v1}.md` (4)

**Interfaces:**
- Consumes: Task 1 şeması.
- Produces: Kalan tüm dokümanlarda frontmatter → tüm `doc/` frontmatter yeşil.

**Değerler:**

| Dosya grubu | authority | code_refs | tags[0] |
|-------------|-----------|-----------|---------|
| roles/* | derived | _(boş)_ | `rol` |
| diagrams/**/* | derived | _(boş)_ | `diyagram` |
| INDEX.md | derived | _(boş)_ | `index` |
| ozel_ders_platformu_PRD_v2 | product | _(boş)_ | `prd` |
| yol_haritasi | product | _(boş)_ | `yol-haritasi` |
| tab_widget | derived | _(boş)_ | `widget` |
| denetim/2026-06-30_* | derived | _(boş)_ | `denetim` |
| _arsiv/* | archive | _(boş)_ | `arsiv` |

`roles/*` için `status` = rolün genel durumu (örn ogretmen 🟢, ogrenci 🟡). `summary`/`tags`/`title`/`updated` Task 3 kuralıyla üretilir. `_arsiv/*` için `summary` = "ARŞİV (tarihî): güncel otorite roles/+modules/".

- [ ] **Step 1: İki tam örnek**

`doc/roles/admin.md` en başına:
```yaml
---
title: "Admin Rolü"
summary: "Platform yöneticisi; doğrulama/moderasyon/destek — adanmış panel yok, yetenekler planlanan"
tags: [rol, admin, moderasyon, planlanan]
status: "🔴"
authority: derived
updated: 2026-08-19
---
```

`doc/_arsiv/is_akislari.md` en başına:
```yaml
---
title: "İş Akışları (ARŞİV)"
summary: "ARŞİV (tarihî): PRD v2.0 türevi iş akışları; güncel otorite roles/ + modules/"
tags: [arsiv, is-akislari]
authority: archive
updated: 2026-08-19
---
```

- [ ] **Step 2: Kalan 18 dosyaya ekle**

Tabloya göre frontmatter ekle. INDEX.md'ye eklerken frontmatter H1'den önce gelir; INDEX gövdesine dokunma.

- [ ] **Step 3: Doğrula — tüm doc/ frontmatter + code_refs yeşil**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | grep -E "FRONTMATTER|CODEREF"
```
Expected: **Boş** (tüm 73 dosya frontmatter'lı; tüm code_refs çözülüyor).

- [ ] **Step 4: Commit**

```bash
git add doc
git commit -m "docs(kb): Dilim A — roles/diagrams/ürün/arşiv frontmatter migrasyonu (20 dosya)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: `/kb-healthcheck` slash-komutu (orkestrasyon + `--deep` fan-out)

**Files:**
- Create: `.claude/commands/kb-healthcheck.md`

**Interfaces:**
- Consumes: `doc/_tools/kb_healthcheck.sh` (Task 2), frontmatter (Task 3-6).
- Produces: `/kb-healthcheck` (Faz 1) ve `/kb-healthcheck --deep` (Faz 1+2) slash-komutu; `doc/_health/YYYY-MM-DD-healthcheck.md` raporu.

- [ ] **Step 1: Slash-komutunu yaz**

`.claude/commands/kb-healthcheck.md`:
````markdown
---
description: doc/ bilgi tabanı health-check — biçim/kanonik/frontmatter (Faz 1) + opsiyonel kod-drift (--deep)
---

`doc/` bilgi tabanının sağlığını denetle. Argüman: `$ARGUMENTS` (`--deep` verilirse Faz 2 de çalışır).

## Faz 1 — deterministik (her zaman)

1. Çalıştır: `bash doc/_tools/kb_healthcheck.sh doc`
2. Çıktıyı severity'ye göre topla (RED/YELLOW/BLUE). Satır formatı: `SEVERITY<TAB>CHECK<TAB>file:line<TAB>message`.

## Faz 2 — kod-drift (yalnız `$ARGUMENTS` içinde `--deep` varsa)

3. `authority: code` olan her `doc/**/*.md`'yi bul:
   `grep -rl '^authority: code' doc --include='*.md'`
4. Bu dokümanlar için **paralel alt-ajan** (Task/Agent) dispatch et — her biri için ayrı bir `general-purpose` ajan, tek mesajda toplu gönder. Her ajana talimat:
   > Şu dokümanı oku: `<yol>`. Frontmatter'daki `code_refs` glob'larındaki gerçek kodu oku. Kodun gerçek endpoint (`Map(Get|Post|Put|Delete|Patch)`), enum değerleri ve domain alanlarını çıkar. Dokümanın iddia ettikleriyle diff'le. SADECE yapısal drift bulgularını döndür; her biri: `severity(RED/YELLOW) | doküman-diyor | kod-diyor | dosya:satır`. Drift yoksa "TEMİZ" döndür.
5. `authority: derived` roller için: iddia edilen modül durumlarını ilgili `modules/mNN` frontmatter `status`'larıyla karşılaştır (tek ajan yeterli).
6. Tüm ajan bulgularını topla, dedup'la.

## Rapor

7. `doc/_health/<bugün YYYY-MM-DD>-healthcheck.md` yaz. Frontmatter:
   ```yaml
   ---
   title: "Health-check <tarih>"
   summary: "doc/ health-check raporu — <RED> kırmızı / <YELLOW> sarı / <BLUE> mavi bulgu"
   tags: [kb, health, rapor]
   authority: derived
   updated: <tarih>
   ---
   ```
   Gövde: en üstte pass/fail + sayaçlar + mod (Faz 1 / --deep); ardından severity sıralı bulgu listesi (🔴 RED, 🟡 YELLOW, 🔵 BLUE), her biri `dosya:satır + ne + beklenen`.
8. Terminal'e kısa özet bas (sayaçlar + ilk birkaç RED bulgu). RED bulgu varsa kullanıcıyı uyar.

## Kurallar
- Beyaz-liste: `EgittimUssu` kural-tanımı satırları ve `.NET 8/10` "çözüldü" notları bulgu değildir (script zaten hariç tutar).
- Bulguları **otomatik düzeltme**; yalnız raporla. Düzeltme kullanıcı onayıyla ayrı adımdır.
````

- [ ] **Step 2: Faz 1'i komut üzerinden doğrula**

Run (komutun sardığı script'i elle çalıştırıp doğrula):
```bash
bash doc/_tools/kb_healthcheck.sh doc | awk -F'\t' '{print $1}' | sort | uniq -c
```
Expected: `RED` satırı **0** (tüm doc/ temiz + migrasyon tam); yalnız olası `BLUE ORPHAN` önerileri olabilir.

- [ ] **Step 3: Commit**

```bash
git add .claude/commands/kb-healthcheck.md
git commit -m "feat(kb): Dilim A — /kb-healthcheck slash-komutu (Faz 1 + --deep fan-out)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 8: Doğrulama — yeşil taban + `--deep` gerçek drift + rapor

**Files:**
- Create: `doc/_health/<bugün>-healthcheck.md` (komut çıktısı)

**Interfaces:**
- Consumes: Task 7 komutu.

- [ ] **Step 1: Yeşil taban (Faz 1)**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc; echo "exit: $?"
```
Expected: RED bulgu yok, `exit: 0`. (BLUE ORPHAN önerileri kabul; varsa not al — gerçek öksüz mü yoksa INDEX'e eklenmeli mi karar ver.)

- [ ] **Step 2: `--deep` çalıştır (Faz 2 kod-drift)**

`/kb-healthcheck --deep` slash-komutunu çalıştır (bu oturumda). Fan-out alt-ajanları `authority: code` dokümanları koda karşı denetler.
Expected: Ya "kod-drift TEMİZ" ya da **gerçek** drift bulguları. Rapor `doc/_health/<bugün>-healthcheck.md`'ye yazılır.

- [ ] **Step 3: Çıkan gerçek drift'i düzelt (varsa)**

Faz 2 gerçek bir uyumsuzluk bulduysa (endpoint/enum/domain), ilgili modül/sayfa dokümanını koda göre düzelt (doğruluk hiyerarşisi) ve frontmatter `updated`'i bugüne çek. Yeniden `/kb-healthcheck --deep` → drift 0 olana kadar.

- [ ] **Step 4: Raporu commit'le**

```bash
git add doc/_health doc
git commit -m "docs(kb): Dilim A — ilk health-check raporu + tespit edilen drift düzeltmeleri

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Tamamlanma kanıtı (tüm plan)

- `bash doc/_tools/kb_healthcheck.sh doc` → RED bulgu **0**, exit 0.
- `bash doc/_tools/test_kb_healthcheck.sh` → tüm `PASS`, exit 0.
- 73 `doc/**/*.md` dosyasının hepsinde geçerli frontmatter (`summary`/`tags`/`authority`/`updated`); `authority: code` olanların hepsinde çözülen `code_refs`.
- `/kb-healthcheck` ve `/kb-healthcheck --deep` çalışır; `doc/_health/` altında en az bir rapor.
- `--deep` kod-drift'i **0** (bulunan gerçek drift düzeltildi).
- Migrasyon yalnız frontmatter ekledi; gövde içeriği/anlamı değişmedi (blockquote korundu).
- Kapsam dışı (Obsidian/ingest/Q&A/arama) bu planda **yok** — ayrı dilimler.
```
