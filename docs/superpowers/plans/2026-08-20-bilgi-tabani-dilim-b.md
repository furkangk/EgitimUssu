# Bilgi Tabanı Makinesi — Dilim B (Obsidian Görünümü) Uygulama Planı

> **Agentic worker için:** GEREKLİ ALT-SKILL: Bu planı görev görev uygulamak için superpowers:subagent-driven-development (önerilen) veya superpowers:executing-plans kullan. Adımlar takip için checkbox (`- [ ]`) kullanır.

**Goal:** `doc/`'u Obsidian vault olarak açılabilir hale getir: committed `.obsidian` config (authority-renkli graph), `_assets` görsel konvansiyonu, 2 Dataview panosu, kurulum dokümanı — health-check yeşil kalarak.

**Architecture:** Vault = `doc/`. Yalnızca paylaşılabilir `.obsidian` config commit'lenir; kişisel/oturum dosyaları gitignore'lanır. Dataview kullanıcının Obsidian'ında çalışır (repo bağımlılığı değil, graceful degrade). Yeni `.md`'ler Dilim A frontmatter'ına uyar, INDEX §6.1'e kaydedilir.

**Tech Stack:** Obsidian config (JSON), Markdown + YAML frontmatter, Dataview sorgu sözdizimi, git, `doc/_tools/kb_healthcheck.sh` (Dilim A).

## Global Constraints

- **Substrat:** markdown konvansiyon + committed config; yeni **repo bağımlılığı yok** (Dataview kullanıcının Obsidian'ında).
- **Vault = `doc/`** (repo kökü değil). `.obsidian` → `doc/.obsidian/`.
- **Yalnızca paylaşılabilir config commit'lenir:** `app.json`, `core-plugins.json`, `graph.json`, `community-plugins.json`, `appearance.json`. Kişisel/oturum dosyaları (`workspace.json`, `workspace-mobile.json`, `plugins/`, `themes/`, `snippets/`) **gitignore**. (CLAUDE.md "platforma özgü dosyaları commit'leme" kuralıyla hizalı.)
- **Görseller** `doc/_assets/`'e; mevcut `doc/diagrams/*.svg` (44 dosya) ve inline mermaid **yerinde kalır/taşınmaz**.
- **Yeni `.md`'ler Dilim A frontmatter şeması:** `summary`/`tags`/`authority`/`updated` zorunlu; gövde `*Güncelleme: 2026-08-20*` frontmatter `updated` ile **eşit** (health-check DATE kontrolü). `authority: derived`.
- **Dataview panoları ayrı `_dashboards/` altında** — çekirdek dokümanlara sorgu gömülmez (portatiflik).
- **Marp / slaytlar bu dilimde YOK** → Dilim D (yalnızca kurulum dokümanında "D'de gelecek" notu).
- **Health-check yeşil kalmalı:** `bash doc/_tools/kb_healthcheck.sh doc` → 0 RED, exit 0. Yeni `.md`'ler INDEX §6.1'de → orphan değil.
- Her görev kendi commit'i; mesaj sonunda `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- **Obsidian JSON'u ilk açılışta kendi formatına normalize eder** — committed dosyalar "tohum"dur; değerler (ek klasörü, graph renkleri) onurlandırılır. Kesin şema tam olmasa da Obsidian toleranslıdır.

## Doğrulama notu (bu dilime özgü)

Config'in görsel etkisi (graph renkleri, panoların render'ı) yalnızca **Obsidian açılarak** doğrulanır (headless çalışmaz). Bu yüzden otomatik kontroller şunları kapsar: JSON geçerliliği, gitignore davranışı (`git check-ignore`), health-check yeşilliği, frontmatter/tarih, INDEX link çözünürlüğü. **Graph rengi + Dataview render'ı = manuel kabul adımı** (Task 3, Step son).

---

## Task 1: `.obsidian` config + `.gitignore` + `_assets/`

**Files:**
- Create: `doc/.obsidian/app.json`, `doc/.obsidian/core-plugins.json`, `doc/.obsidian/graph.json`, `doc/.obsidian/community-plugins.json`, `doc/.obsidian/appearance.json`
- Create: `doc/_assets/.gitkeep`
- Modify: `.gitignore` (repo kökü)

**Interfaces:**
- Produces: `doc/` üzerinde açılabilir Obsidian vault iskeleti; ek klasörü `_assets`; graph `authority`'e göre renkli. Sonraki görevler bu vault'a doküman/pano ekler.

- [ ] **Step 1: `doc/.obsidian/app.json` yaz**

```json
{
  "attachmentFolderPath": "_assets",
  "newLinkFormat": "relative",
  "useMarkdownLinks": true,
  "alwaysUpdateLinks": true,
  "showFrontmatter": true
}
```

- [ ] **Step 2: `doc/.obsidian/core-plugins.json` yaz**

```json
{
  "file-explorer": true,
  "global-search": true,
  "switcher": true,
  "graph": true,
  "backlink": true,
  "outgoing-link": true,
  "tag-pane": true,
  "properties": true,
  "page-preview": true,
  "outline": true,
  "command-palette": true
}
```

- [ ] **Step 3: `doc/.obsidian/graph.json` yaz (authority renk grupları)**

```json
{
  "colorGroups": [
    { "query": "[authority:code]", "color": { "a": 1, "rgb": 3900150 } },
    { "query": "[authority:product]", "color": { "a": 1, "rgb": 10265519 } },
    { "query": "[authority:derived]", "color": { "a": 1, "rgb": 2278750 } },
    { "query": "[authority:archive]", "color": { "a": 1, "rgb": 13158084 } }
  ],
  "showTags": true,
  "showAttachments": false,
  "showOrphans": true
}
```

> Renkler (mavi/gri/yeşil/soluk) kozmetiktir; Obsidian'da elle ayarlanabilir. `[authority:code]` = Obsidian property araması.

- [ ] **Step 4: `doc/.obsidian/community-plugins.json` + `appearance.json` yaz**

`community-plugins.json`:
```json
["dataview"]
```
`appearance.json`:
```json
{}
```

> `community-plugins.json` yalnızca "önerilen" listesidir; plugin **kodu** commit'lenmez (kullanıcı Obsidian'dan kurar). `appearance.json` boş = kullanıcının teması dayatılmaz.

- [ ] **Step 5: `doc/_assets/.gitkeep` oluştur**

```bash
mkdir -p doc/_assets && touch doc/_assets/.gitkeep
```

- [ ] **Step 6: `.gitignore`'a kişisel Obsidian dosyalarını ekle**

Repo kökü `.gitignore` sonuna ekle:
```
# Obsidian — kişisel/oturum durumu (paylaşılabilir config commit'lenir)
doc/.obsidian/workspace.json
doc/.obsidian/workspace-mobile.json
doc/.obsidian/plugins/
doc/.obsidian/themes/
doc/.obsidian/snippets/
```

- [ ] **Step 7: JSON geçerliliği + gitignore davranışını doğrula**

Run:
```bash
for f in app core-plugins graph community-plugins appearance; do python3 -m json.tool "doc/.obsidian/$f.json" >/dev/null && echo "OK $f" || echo "BOZUK $f"; done
echo "-- ignore edilmeli (yol yazmalı) --"; git check-ignore doc/.obsidian/workspace.json doc/.obsidian/plugins/x
echo "-- izlenmeli (çıktı BOŞ olmalı) --"; git check-ignore doc/.obsidian/app.json doc/.obsidian/graph.json || echo "(izleniyor: doğru)"
```
Expected: 5 `OK`; `workspace.json` + `plugins/x` ignore listesinde (yol basılır); `app.json`/`graph.json` için `git check-ignore` **boş** (exit 1 → "izleniyor: doğru").

- [ ] **Step 8: Health-check etkilenmedi + commit**

Run: `bash doc/_tools/kb_healthcheck.sh doc >/dev/null; echo "exit=$?"`
Expected: `exit=0` (`.obsidian`/`_assets` markdown değil, taranmaz).
```bash
git add doc/.obsidian doc/_assets/.gitkeep .gitignore
git commit -m "feat(kb): Dilim B — doc/ Obsidian vault config + _assets + gitignore

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Dataview panoları (`doc/_dashboards/`)

**Files:**
- Create: `doc/_dashboards/modul_durum_panosu.md`, `doc/_dashboards/kod_dokuman_envanteri.md`

**Interfaces:**
- Consumes: Dilim A frontmatter (`status`/`authority`/`code_refs`/`updated`).
- Produces: Frontmatter'dan canlı oto-üretilen 2 pano (Dataview kuruluyken).

- [ ] **Step 1: `doc/_dashboards/modul_durum_panosu.md` yaz**

```markdown
---
title: "Modül Durum Panosu"
summary: "modules/ dokümanlarının status'una göre canlı Dataview tablosu — INDEX §3 modül tablosunun oto-üretilen hali (Dataview gerekir)"
tags: [kb, pano, dataview, modul]
authority: derived
updated: 2026-08-20
---

# 📊 Modül Durum Panosu

> ⚠️ **Dataview gerekir.** Bu pano frontmatter'dan canlı üretilir; Dataview plugin'i
> kurulu değilse aşağıdaki sorgu inert kod bloğu olarak görünür (bozulma yok).

```dataview
TABLE WITHOUT ID file.link AS "Modül", status AS "Durum", updated AS "Güncelleme"
FROM "modules"
WHERE status
SORT status DESC, file.name ASC
```

*Güncelleme: 2026-08-20*
```

- [ ] **Step 2: `doc/_dashboards/kod_dokuman_envanteri.md` yaz**

```markdown
---
title: "Kod-Doküman Envanteri"
summary: "authority: code dokümanları + code_refs + updated — en eski güncellenen (drift riski yüksek) üstte; Dataview gerekir"
tags: [kb, pano, dataview, drift]
authority: derived
updated: 2026-08-20
---

# 🔎 Kod-Doküman Envanteri (drift riski)

> ⚠️ **Dataview gerekir.** `authority: code` dokümanları en eski `updated` üstte listeler —
> koddan sapma riski en yüksek olanları önce gösterir. `/kb-healthcheck --deep` ile birlikte kullanılır.

```dataview
TABLE WITHOUT ID file.link AS "Doküman", code_refs AS "Kaynak", status AS "Durum", updated AS "Güncelleme"
WHERE authority = "code"
SORT updated ASC
```

*Güncelleme: 2026-08-20*
```

- [ ] **Step 3: Health-check panolarda temiz (fence + frontmatter) doğrula**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | grep "_dashboards" | grep -E "FRONTMATTER|DATE|FENCE"
```
Expected: **BOŞ** (frontmatter tam, gövde `Güncelleme` = frontmatter `updated` = 2026-08-20, ` ```dataview ` fence'leri dengeli). Not: bu iki dosya bu adımda `BLUE ORPHAN` verebilir (henüz INDEX'te yok) — Task 3'te INDEX'e eklenince düşer; RED/YELLOW olmamalı.

- [ ] **Step 4: 0 RED doğrula + commit**

Run: `bash doc/_tools/kb_healthcheck.sh doc | grep RED || echo "(0 RED)"`
Expected: `(0 RED)`.
```bash
git add doc/_dashboards
git commit -m "feat(kb): Dilim B — Dataview panoları (modül durumu + kod-doküman envanteri)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Kurulum dokümanı + INDEX §6.1 kaydı + kabul

**Files:**
- Create: `doc/_obsidian_kurulum.md`
- Modify: `doc/INDEX.md` (§6.1 satırları + `updated`/`Son güncelleme` tarihi)

**Interfaces:**
- Consumes: Task 1 (vault config) + Task 2 (panolar).
- Produces: Yeni bir kullanıcıyı sıfırdan çalışır vault'a götüren rehber; yeni `.md`'ler INDEX'te kayıtlı (orphan değil).

- [ ] **Step 1: `doc/_obsidian_kurulum.md` yaz**

```markdown
---
title: "Obsidian Kurulum & Kullanım"
summary: "doc/'u Obsidian vault olarak açma, Dataview kurulumu, graph renk anlamları, görsel konvansiyonu — bilgi tabanı görüntüleyicisi"
tags: [kb, obsidian, kurulum, rehber]
authority: derived
updated: 2026-08-20
---

# 🧭 Obsidian Kurulum & Kullanım

> `doc/` bilgi tabanını Obsidian ile **görüntülemek** için rehber. Wiki'yi Claude yazar/bakım yapar;
> sen Obsidian'da gezinirsin. (Dilim B — bilgi tabanı makinesi.)

## 1. Vault'u aç
Obsidian → **Open folder as vault** → repodaki `doc/` klasörünü seç. `.obsidian` config'i hazır gelir
(graph renkleri, ek klasörü, core plugin'ler).

## 2. Dataview'i kur (panolar için)
**Settings → Community plugins → Browse → "Dataview"** kur ve etkinleştir. Sonra `_dashboards/`
altındaki panolar (modül durumu, kod-doküman envanteri) frontmatter'dan canlı tablo üretir.
Dataview kurulmazsa panolar bozulmaz; sorgu inert kod bloğu görünür.

## 3. Graph renkleri (authority)
Graph düğüm renkleri `authority` alanına göre: 🔵 `code` (koddan doğrulanan) · ⚪ `product` (plan/PRD) ·
🟢 `derived` (türev/rehber) · soluk `archive`. Renk grupları `.obsidian/graph.json`'da; elle ayarlanabilir.

## 4. Görseller (`_assets/`)
Yapıştırdığın/indirdiğin görseller `doc/_assets/`'e kaydedilir (ek klasörü oraya ayarlı). Mevcut
diyagram SVG'leri `doc/diagrams/`'da, mermaid blokları inline (Obsidian natif render eder) — dokunma.

## 5. Neler commit'lenir
Yalnızca paylaşılabilir config (`app/core-plugins/graph/community-plugins/appearance.json`) izlenir;
kişisel oturum durumu (`workspace.json`, plugin verisi, temalar) `.gitignore`'dadır — her cihazda kendi düzenin.

## 6. Sırada ne var
**Marp (slayt önizleme/export)** ve **`/kb-ask` Q&A + çıktı render** **Dilim D**'de gelecek.
Dış kaynak ingest'i (`raw/` + `/kb-ingest`) **Dilim C**'de.

*Güncelleme: 2026-08-20*
```

- [ ] **Step 2: INDEX §6.1'e yeni dosyaları ekle**

`doc/INDEX.md` içinde §6.1 "Bilgi Tabanı Makinesi (Dilim A)" başlığını "Bilgi Tabanı Makinesi (Dilim A + B)" yap ve tablosuna şu satırları ekle (mevcut `00_kb_konvansiyon`/`_tools`/`_health` satırlarının altına):

```markdown
| [`_obsidian_kurulum.md`](_obsidian_kurulum.md) | **Obsidian görünümü** (Dilim B): vault'u açma, Dataview kurulumu, graph renk anlamları, `_assets` görsel konvansiyonu |
| [`_dashboards/`](_dashboards/) | Dataview panoları (frontmatter'dan canlı): `modul_durum_panosu.md` (INDEX §3'ün oto-üretilen hali) + `kod_dokuman_envanteri.md` (drift riski) |
| `.obsidian/`, `_assets/` | Obsidian vault config (authority-renkli graph, ek klasörü) + görsel/ek klasörü. Kişisel dosyalar `.gitignore`'da |
```

- [ ] **Step 3: INDEX tarihini güncelle**

`doc/INDEX.md`'de frontmatter `updated:` ve gövde `> **Son güncelleme:**` satırındaki tarihi **2026-08-20** yap ve parantez notuna `Dilim B — Obsidian görünümü` ekle (ikisi de 2026-08-20 kalmalı — DATE çelişkisi olmasın).

- [ ] **Step 4: Health-check yeşil + yeni dosyalar orphan değil doğrula**

Run:
```bash
bash doc/_tools/kb_healthcheck.sh doc | awk -F'\t' '{print $1,$2}' | sort | uniq -c
bash doc/_tools/kb_healthcheck.sh doc >/dev/null; echo "exit=$?"
echo "-- yeni dosyalar orphan mı? (boş olmalı) --"
bash doc/_tools/kb_healthcheck.sh doc | grep -E "_obsidian_kurulum|_dashboards" || echo "(orphan değil — temiz)"
```
Expected: `exit=0`, **0 RED / 0 YELLOW**; `_obsidian_kurulum`/`_dashboards` için bulgu **yok** (INDEX'e eklendi). BLUE orphan sayısı Task 1 öncesine göre **artmamalı** (yeni 3 md INDEX'te).

- [ ] **Step 5: Commit**

```bash
git add doc/_obsidian_kurulum.md doc/INDEX.md
git commit -m "docs(kb): Dilim B — Obsidian kurulum dokümanı + INDEX §6.1 kaydı

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

- [ ] **Step 6: Manuel kabul (Obsidian) — kullanıcı adımı**

> ⚠️ **Bu adım otomatik değildir** (Obsidian headless açılmaz). Kullanıcıya bildir: Obsidian'ı `doc/` üzerine
> aç → (a) graph düğümleri authority'e göre renkli mi, (b) Dataview kurulunca 2 pano tablo üretiyor mu,
> (c) yapıştırılan görsel `_assets/`'e düşüyor mu, (d) `git status` temiz mi (workspace.json izlenmiyor).
> Sorun varsa ilgili config/JSON düzeltilir.

---

## Tamamlanma kanıtı (tüm plan)

- `doc/.obsidian/` 5 paylaşılabilir config dosyası **geçerli JSON**; `git check-ignore` kişisel dosyaları (workspace/plugins/themes/snippets) ignore ediyor, config dosyalarını izliyor.
- `doc/_assets/.gitkeep` var; ek klasörü `app.json`'da `_assets`.
- 2 Dataview panosu + `_obsidian_kurulum.md` Dilim A frontmatter'lı, health-check'te 0 RED/YELLOW, INDEX §6.1'de kayıtlı (orphan değil).
- `bash doc/_tools/kb_healthcheck.sh doc` → **exit 0**, 0 RED.
- INDEX `updated`/`Son güncelleme` = 2026-08-20; DATE çelişkisi yok.
- **Manuel kabul** (Obsidian görsel doğrulaması) kullanıcıya devredildi — plan bunu otomatik iddia etmez.
- Kapsam dışı (Marp/slayt = Dilim D, ingest = Dilim C) bu planda **yok**.
```
