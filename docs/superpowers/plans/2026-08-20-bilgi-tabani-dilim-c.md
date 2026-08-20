# Bilgi Tabanı Makinesi — Dilim C (Ingest + Derleme) Uygulama Planı

> **Agentic worker için:** GEREKLİ ALT-SKILL: superpowers:subagent-driven-development (önerilen) veya superpowers:executing-plans. Adımlar checkbox (`- [ ]`) kullanır.

**Goal:** `doc/raw/` (ham kaynak) + `doc/kaynaklar/` (damıtılmış `reference` makaleleri) + `/kb-ingest` slash-komutu ekle; `kb_healthcheck.sh`'ı `reference` authority + `raw/` muafiyeti için genişlet — health-check yeşil kalarak.

**Architecture:** Yeni `authority: reference` (+ `source`/`subtype`) Dilim A konvansiyonunu genişletir. `raw/` verbatim'dir, health-check'ten muaf. `/kb-ingest` Claude-güdümlü (kaynağı al → damıt → backlink → index). Dogfood: `promp.txt`.

**Tech Stack:** Markdown + YAML frontmatter, bash 3.2 (`kb_healthcheck.sh` uzantısı), Claude Code slash-komutu, git.

## Global Constraints

- **Substrat:** markdown konvansiyon + slash-komut + saf bash; yeni **repo bağımlılığı yok**.
- **bash 3.2 / BSD grep uyumu:** `declare -A`/`mapfile`/`${var,,}`/`grep -P` **yok**. Mevcut `kb_healthcheck.sh` desenini koru (process-substitution `< <(...)`).
- **Yeni authority `reference`:** dış/ham kaynaktan damıtılmış; `code_refs` yok, kod-drift'e tabi değil. Zorunlu ek alanlar: `source` (raw/ yolu, repo dosyası veya URL) + `subtype` (`research`|`design`|`decision`).
- **`reference` kanonik gerçeği EZMEZ** (doğruluk hiyerarşisi: kod > INDEX §0 > PRD; reference dış bilgidir).
- **`raw/` health-check MUAF** (verbatim; frontmatter/format kuralı uygulanmaz).
- **Backlink tek yönlü:** makale → ilgili doküman (Obsidian tersini gösterir; çekirdek dokümanlar düzenlenmez).
- **Health-check yeşil:** `bash doc/_tools/kb_healthcheck.sh doc` → 0 RED, exit 0; fixture testleri (`test_kb_healthcheck.sh`) yeşil.
- Her görev kendi commit'i; mesaj sonunda `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

---

## Task 1: Konvansiyon + health-check uzantısı (`reference` + `raw/` muafiyeti) + fixtures

**Files:**
- Modify: `doc/00_kb_konvansiyon.md` (authority += reference; source/subtype)
- Modify: `doc/_tools/kb_healthcheck.sh` (md_files raw/ hariç; frontmatter authority-set + reference→source; yeni check_source)
- Modify: `doc/_tools/test_kb_healthcheck.sh` (yeni assert'ler)
- Create: `doc/_tools/fixtures/good_reference.md`, `doc/_tools/fixtures/bad_reference_nosource.md`, `doc/_tools/fixtures/raw/verbatim.md`
- Modify: `doc/.obsidian/graph.json` (reference rengi)

**Interfaces:**
- Produces: `authority: reference` şemasını doğrulayan + `raw/` dizinini atlayan health-check. `SOURCE` yeni CHECK adı (`SEVERITY<TAB>SOURCE<TAB>file:line<TAB>msg`).

- [ ] **Step 1: Yeni fixture'ları oluştur (TDD kırmızı)**

`doc/_tools/fixtures/good_reference.md`:
```markdown
---
title: "Geçerli Reference Fixture"
summary: "authority reference + URL source ile geçerli örnek — sıfır bulgu vermeli"
tags: [fixture, kaynak]
authority: reference
subtype: research
source: https://example.com/kaynak
updated: 2026-08-20
---

# Geçerli Reference

Damıtılmış içerik. Source URL olduğu için varlık kontrolü atlanır.
```

`doc/_tools/fixtures/bad_reference_nosource.md`:
```markdown
---
title: "Source'suz Reference Fixture"
summary: "authority reference ama source alanı yok — bulgu vermeli"
tags: [fixture, kaynak]
authority: reference
subtype: research
updated: 2026-08-20
---

# Source Yok

Bu dosyanın source alanı eksik.
```

`doc/_tools/fixtures/raw/verbatim.md` (frontmatter YOK — raw/ muaf olmalı):
```markdown
# Ham Kaynak (verbatim)

Bu dosya raw/ altında; frontmatter yok ama health-check bulgu ÜRETMEMELİ (muaf).
```

- [ ] **Step 2: Test runner'a yeni assert'leri ekle**

`doc/_tools/test_kb_healthcheck.sh` içinde `assert_clean` çağrısından ÖNCE şu satırları ekle:
```bash
assert_finds "FRONTMATTER" "bad_reference_nosource.md" "reference source eksikliği yakalandı"

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
```

- [ ] **Step 3: Testi çalıştır — yeni assert'ler başarısız (script henüz genişlemedi)**

Run: `bash doc/_tools/test_kb_healthcheck.sh`
Expected: Eski 6 assert PASS; yeni `bad_reference_nosource` FAIL (script henüz source kontrolü yapmıyor) ve/veya `raw/ muaf değil` FAIL (raw henüz dışlanmıyor). En az bir yeni `FAIL:`.

- [ ] **Step 4: `md_files()`'a raw/ dışlaması ekle**

`doc/_tools/kb_healthcheck.sh` içinde `md_files()`'taki `case` bloğunu değiştir:
```bash
    case "$rel" in
      _tools/*|*/_tools/*) continue ;;
```
→
```bash
    case "$rel" in
      _tools/*|*/_tools/*|raw/*|*/raw/*) continue ;;
```

- [ ] **Step 5: `check_frontmatter()`'a authority-set + reference→source doğrulaması ekle**

`check_frontmatter()` içindeki şu blok:
```bash
    auth=$(echo "$fm" | grep -E '^authority:' | sed -E 's/^authority:[[:space:]]*//')
    if [ "$auth" = "code" ]; then
      echo "$fm" | grep -qE '^code_refs:' || emit YELLOW FRONTMATTER "$f:1" "authority: code ama code_refs yok"
    fi
```
→
```bash
    auth=$(echo "$fm" | grep -E '^authority:' | sed -E 's/^authority:[[:space:]]*//')
    case "$auth" in
      code|product|derived|archive|reference|"") : ;;
      *) emit YELLOW FRONTMATTER "$f:1" "geçersiz authority: $auth" ;;
    esac
    if [ "$auth" = "code" ]; then
      echo "$fm" | grep -qE '^code_refs:' || emit YELLOW FRONTMATTER "$f:1" "authority: code ama code_refs yok"
    fi
    if [ "$auth" = "reference" ]; then
      echo "$fm" | grep -qE '^source:' || emit YELLOW FRONTMATTER "$f:1" "authority: reference ama source yok"
    fi
```

- [ ] **Step 6: `check_source()` fonksiyonunu ekle ve çağır**

`check_code_refs()` fonksiyonunun HEMEN ARDINDAN ekle:
```bash
# 5b) reference source (raw/ yolu veya repo dosyası) var mı — URL ise atla
check_source() {
  while IFS= read -r f; do
    src=$(awk 'NR>1 && /^---$/{exit} /^source:/{print}' "$f" | sed -E 's/^source:[[:space:]]*//' | head -1)
    [ -z "$src" ] && continue
    case "$src" in http://*|https://*) continue ;; esac
    [ -f "$TARGET/$src" ] || emit YELLOW SOURCE "$f:1" "source çözülmüyor: $src"
  done < <(md_files)
  return 0
}
```
Ve dosyanın sonundaki çağrı listesine `check_code_refs`'ten sonra `check_source` ekle:
```bash
check_code_refs
check_source
check_dates
```

- [ ] **Step 7: Testi çalıştır — hepsi geçsin**

Run: `bash doc/_tools/test_kb_healthcheck.sh`
Expected: Tüm satırlar `PASS:` (eski 6 + yeni 3); exit 0.

- [ ] **Step 8: Gerçek doc/'ta regresyon yok doğrula**

Run: `bash doc/_tools/kb_healthcheck.sh doc | grep -E "RED|	YELLOW	" || echo "0 RED/0 YELLOW"; bash doc/_tools/kb_healthcheck.sh doc >/dev/null; echo "exit=$?"`
Expected: `0 RED/0 YELLOW`, `exit=0` (henüz reference dokümanı yok; raw/ yok; mevcut dokümanlar etkilenmedi).

- [ ] **Step 9: Konvansiyon dokümanını güncelle**

`doc/00_kb_konvansiyon.md`:
- Şema tablosuna iki satır ekle (`code_refs` satırından sonra):
```markdown
| `source` | koşullu | `authority: reference` ise zorunlu: `raw/<dosya>`, repo dosyası veya URL |
| `subtype` | koşullu | `authority: reference` ise: `research` \| `design` \| `decision` |
```
- authority değerleri listesine ekle:
```markdown
- **reference** — Dış/ham kaynaktan damıtılmış, kaynaklı makale (`doc/kaynaklar/`). `source` + `subtype` zorunlu; kod-drift'e tabi değil; kanonik gerçeği ezmez. Orijinal `doc/raw/`'da (verbatim, health-check muaf).
```
- `updated: 2026-08-20` ve gövde `*Güncelleme:*` satırını 2026-08-20 yap (zaten öyle olabilir — eşit kalsın).

- [ ] **Step 10: Obsidian graph'a reference rengi ekle**

`doc/.obsidian/graph.json` `colorGroups` dizisine ekle (archive satırından sonra, virgülle):
```json
    { "query": "[authority:reference]", "color": { "a": 1, "rgb": 11032055 } }
```

- [ ] **Step 11: Commit**

Run: `python3 -m json.tool doc/.obsidian/graph.json >/dev/null && echo "JSON OK"`
Expected: `JSON OK`.
```bash
git add doc/_tools doc/00_kb_konvansiyon.md doc/.obsidian/graph.json
git commit -m "feat(kb): Dilim C — health-check reference authority + raw/ muafiyeti + fixtures

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: `raw/` + `kaynaklar/` iskeleti + `/kb-ingest` slash-komutu

**Files:**
- Create: `doc/raw/.gitkeep`
- Create: `doc/kaynaklar/00_kaynaklar_index.md`
- Create: `.claude/commands/kb-ingest.md`
- Modify: `doc/INDEX.md` (§6.1 satırları + tarih)

**Interfaces:**
- Consumes: Task 1 (reference şeması + health-check).
- Produces: Ingest hedef dizinleri + `/kb-ingest` komutu + index kaydı.

- [ ] **Step 1: `doc/raw/.gitkeep` oluştur**

```bash
mkdir -p doc/raw && touch doc/raw/.gitkeep
```

- [ ] **Step 2: `doc/kaynaklar/00_kaynaklar_index.md` oluştur**

```markdown
---
title: "Kaynaklar İndeksi"
summary: "doc/kaynaklar/ altındaki damıtılmış reference makalelerinin subtype'a göre indeksi (research/design/decision); orijinaller doc/raw/'da"
tags: [kb, kaynaklar, indeks, reference]
authority: derived
updated: 2026-08-20
---

# 📚 Kaynaklar İndeksi

> Dış/ham kaynaklardan `/kb-ingest` ile damıtılmış `reference` makaleleri. Orijinaller `../raw/`'da (verbatim).
> Her makale ilgili modül/rollere backlink verir; Obsidian ters backlink'i otomatik gösterir.

## Araştırma (research)
| Makale | Özet | Kaynak |
|--------|------|--------|
| _(henüz yok)_ | | |

## Tasarım (design)
| Makale | Özet | Kaynak |
|--------|------|--------|
| _(henüz yok)_ | | |

## Karar (decision)
| Makale | Özet | Kaynak |
|--------|------|--------|
| _(henüz yok)_ | | |

*Güncelleme: 2026-08-20*
```

- [ ] **Step 3: `/kb-ingest` slash-komutunu yaz**

`.claude/commands/kb-ingest.md`:
````markdown
---
description: Dış/ham kaynağı (raw/ dosyası, URL veya yapıştırma) damıtıp doc/kaynaklar/ altında reference makalesi + backlink + index üretir
---

Bir kaynağı bilgi tabanına **ingest** et. Argüman: `$ARGUMENTS` (raw/ dosya yolu, URL veya "paste:<metin>"; opsiyonel `subtype=research|design|decision` ve konu ipucu).

## 1. Kaynağı edin (doc/raw/)
- **URL** ise: WebFetch ile getir → temizlenmiş metni `doc/raw/<slug>.md`'ye yaz; başına `> Kaynak: <URL> · Alındı: <bugün>` satırı.
- **raw/ dosyası** zaten varsa: onu kullan.
- **paste:** ise: metni `doc/raw/<slug>.md`'ye yaz.
- **Görsel (design)** ise: `doc/_assets/<slug>.<ext>`'e koy.
- `<slug>` = kısa kebab-case ad (konudan).

## 2. Damıt
Kaynağı oku → başlık, tek-satır özet (≤160), kilit noktalar (madde), kavram/varlıklar. `subtype`'ı belirle/doğrula (research/design/decision).

## 3. İlişkilendir (backlink)
`doc/INDEX.md` + `grep` ile bu kaynağın hangi `modules/`/`roles/`/`architecture/` dokümanlarını beslediğini bul. Makalede "## İlgili" altında bunlara **tek yönlü** göreli link ver (çekirdek dokümanları DÜZENLEME).

## 4. Makaleyi yaz/güncelle (doc/kaynaklar/<slug>.md)
Aynı `source` için makale varsa **güncelle** (yeniden oluşturma). Frontmatter:
```yaml
---
title: "<başlık>"
summary: "<tek satır>"
tags: [kaynak, <subtype>, <2-3 konu>]
authority: reference
subtype: <research|design|decision>
source: raw/<slug>.md    # veya URL veya repo dosyası
updated: <bugün YYYY-MM-DD>
---
```
Gövde: özet → "## Kilit Noktalar" → (decision ise "## Karar" + "## Gerekçe") → "## İlgili" (backlink'ler) → "## Kaynak" (`[orijinal](../raw/<slug>.md)` veya URL). Gövde sonunda `*Güncelleme: <bugün>*` (frontmatter `updated` ile EŞİT).

## 5. Kaydet (index)
`doc/kaynaklar/00_kaynaklar_index.md`'de ilgili subtype tablosuna satır ekle (`_(henüz yok)_` satırını değiştir/altına ekle).

## 6. Doğrula
`bash doc/_tools/kb_healthcheck.sh doc` çalıştır → yeni makalede FRONTMATTER/DATE/SOURCE bulgusu **olmamalı**, 0 RED. Sorun varsa düzelt.

## Kurallar
- **Otomatik düzeltme yok** — yalnız ekle/güncelle; kanonik gerçeği (INDEX §0) EZME. Reference dış bilgidir.
- Belirsizlikte (subtype, hangi backlink) makul karar ver ve makalede belirt.
- `source` `raw/` yolu değilse (URL/repo dosyası) da olur; health-check URL'i atlar, repo yolunu doğrular.
````

- [ ] **Step 4: INDEX §6.1'e raw/ + kaynaklar/ ekle**

`doc/INDEX.md` §6.1 tablosuna (`.obsidian/, _assets/` satırından sonra) ekle:
```markdown
| [`kaynaklar/`](kaynaklar/00_kaynaklar_index.md) | **Ingest** (Dilim C): `/kb-ingest` ile dış/ham kaynaklardan damıtılmış `reference` makaleleri (research/design/decision) + backlink. İndeks: `00_kaynaklar_index.md` |
| `raw/` | Ham kaynaklar (verbatim: kırpılmış makale/PDF/görsel/döküm). Health-check **muaf**; orijinal `source` referansı buraya bakar |
```
Ve §6.1 başlığını `(Dilim A + B + C)` yap. `updated`/`Son güncelleme` = 2026-08-20 (Dilim C notu ekle; DATE çelişkisi olmasın).

- [ ] **Step 5: Health-check yeşil + commit**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | grep -E "RED|	YELLOW	" || echo "0 RED/0 YELLOW"
bash doc/_tools/kb_healthcheck.sh doc >/dev/null; echo "exit=$?"
```
Expected: `0 RED/0 YELLOW`, `exit=0`. (`00_kaynaklar_index` INDEX'te; `raw/.gitkeep` md değil; boş kaynaklar/ makale yok.)
```bash
git add doc/raw doc/kaynaklar .claude/commands/kb-ingest.md doc/INDEX.md
git commit -m "feat(kb): Dilim C — raw/ + kaynaklar/ iskeleti + /kb-ingest slash-komutu

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Dogfood — `promp.txt` ingest + kabul

**Files:**
- Create: `doc/kaynaklar/proje-vizyonu-promp.md` (promp.txt'den damıtılmış)
- Modify: `doc/kaynaklar/00_kaynaklar_index.md` (satır ekle)

**Interfaces:**
- Consumes: Task 1 + 2 (şema + komut + iskelet).
- Produces: Makinenin ilk gerçek ingest çıktısı; `/kb-ingest` akışının kanıtı.

- [ ] **Step 1: `promp.txt`'yi `/kb-ingest` akışıyla damıt**

`doc/promp.txt` mevcut ham vizyon kaynağıdır. `/kb-ingest` Adım 2-4'ü uygula (kaynak zaten repoda → `source: promp.txt`). `doc/kaynaklar/proje-vizyonu-promp.md` oluştur:
- Frontmatter: `authority: reference`, `subtype: decision`, `source: promp.txt`, `summary`/`tags: [kaynak, decision, vizyon]`/`updated: 2026-08-20`.
- Gövde: promp.txt'nin damıtılmış vizyonu → "## Kilit Noktalar" (öğretmen/öğrenci/veli, eşleştirme, premium, yıldız/yorum) → "## İlgili" backlink'ler: [`../ozel_ders_platformu_PRD_v2.md`](PRD), [`../modules/00_genel_bakis.md`], [`../roles/00_roller_genel_bakis.md`] → "## Kaynak": [`../promp.txt`](../promp.txt). Sonda `*Güncelleme: 2026-08-20*`.

> Not: promp.txt PRD'nin türediği kaynaktır; bu makale onu **cite eder**, kanonik gerçeği ezmez (PRD v2.1 esas kalır).

- [ ] **Step 2: `00_kaynaklar_index.md` "Karar (decision)" tablosuna satır ekle**

`_(henüz yok)_` satırını şununla değiştir:
```markdown
| [Proje Vizyonu (promp)](proje-vizyonu-promp.md) | Kullanıcının özgün vizyon metninin damıtımı; PRD'nin kaynağı | [promp.txt](../promp.txt) |
```

- [ ] **Step 3: Health-check + reference doğrulaması**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | grep "kaynaklar" || echo "(kaynaklar temiz)"
bash doc/_tools/kb_healthcheck.sh doc | grep -E "RED|	YELLOW	" || echo "0 RED/0 YELLOW"
bash doc/_tools/kb_healthcheck.sh doc >/dev/null; echo "exit=$?"
```
Expected: kaynaklar makalesinde FRONTMATTER/DATE/SOURCE bulgusu **yok** (`source: promp.txt` → `doc/promp.txt` var → temiz); `0 RED/0 YELLOW`; `exit=0`. Makale `00_kaynaklar_index`'te listeli; check-7 yalnız root INDEX'e baktığı için **BLUE ORPHAN** verebilir (bilinen sınır, non-blocking — section-index'li diğer dosyalar gibi). BLUE artışı sorun değildir; RED/YELLOW olmamalı.

- [ ] **Step 4: Commit**

```bash
git add doc/kaynaklar
git commit -m "docs(kb): Dilim C — dogfood: promp.txt → proje vizyonu reference makalesi

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Tamamlanma kanıtı (tüm plan)

- `bash doc/_tools/test_kb_healthcheck.sh` → tüm PASS (eski 6 + yeni 3: reference-source, raw/ muafiyeti, good_reference temiz), exit 0.
- `bash doc/_tools/kb_healthcheck.sh doc` → **0 RED / 0 YELLOW, exit 0**.
- `authority: reference` + `source` + `subtype` konvansiyonda belgeli; health-check reference'ı doğruluyor (eksik source → bulgu; raw/ source yoksa → bulgu; URL → atla; geçerli → temiz).
- `doc/raw/` health-check muaf; `doc/kaynaklar/00_kaynaklar_index.md` + `/kb-ingest` komutu mevcut; INDEX §6.1'de kayıtlı.
- **Dogfood:** `promp.txt` → `doc/kaynaklar/proje-vizyonu-promp.md` (reference/decision, backlink'li, kaynak linkli), `00_kaynaklar_index`'te listeli, 0 RED/0 YELLOW (makale section-index'li → BLUE orphan olabilir, non-blocking).
- `doc/.obsidian/graph.json` reference için renk grubu içeriyor (geçerli JSON).
- Kapsam dışı (Q&A/Marp = Dilim D, arama motoru = YAGNI) bu planda **yok**.
```
