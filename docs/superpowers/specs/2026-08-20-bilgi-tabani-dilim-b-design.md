---
title: "Bilgi Tabanı Dilim B — Obsidian Görünümü (Tasarım)"
summary: "doc/'u Obsidian vault olarak açan config + authority-renkli graph + _assets görsel konvansiyonu + Dataview panoları + kurulum dokümanı"
tags: [kb, dilim-b, obsidian, tasarim, spec]
authority: derived
updated: 2026-08-20
---

# Bilgi Tabanı Makinesi — Dilim B: Obsidian Görünümü (Tasarım)

> **Tarih:** 2026-08-20 · **Durum:** Onaylandı (tasarım) · **Önceki dilim:** [Dilim A — Temel + Health-check](2026-08-20-bilgi-tabani-dilim-a-design.md) (tamamlandı, merge)
>
> **Kaynak fikir:** LLM tabanlı bilgi tabanı yönteminin "IDE / görüntüleyici" katmanı. Dilim A'nın kurduğu frontmatter (`summary`/`tags`/`status`/`authority`/`code_refs`) üzerine, `doc/`'u gezilebilir bir Obsidian vault'una dönüştürür.

## 1. Amaç ve kapsam

Dilim A `doc/`'a makine-okunur frontmatter + otomatik health-check ekledi. Dilim B, aynı `doc/`'u **Obsidian görüntüleyicisi** olarak açılabilir hale getirir: graph view (backlink ağı, authority'e göre renkli), yerel arama, görsel/ek konvansiyonu ve frontmatter'dan **canlı oto-üretilen panolar** (Dataview). Wiki'yi Claude yazar/bakım yapar; kullanıcı Obsidian'da **görüntüler**.

**Substrat (tüm makine için geçerli):** Ajan = Claude Code. Makine = markdown konvansiyon + committed config; yeni **repo bağımlılığı yok**. Obsidian ve Dataview kullanıcının uygulamasında çalışır (repo'nun build/paket bağımlılığı değil); Dataview kurulu değilse dokümanlar **zarifçe düşer** (sorgu blokları inert görünür).

**Bu dilimin kapsamı:**
- `doc/.obsidian/` committed çekirdek config (graph renkleri, core plugin'ler, ek klasörü, community-plugins listesi).
- `.gitignore`'a kişisel/oturum Obsidian dosyaları (workspace layout, plugin data).
- `doc/_assets/` görsel/ek konvansiyonu (yeni görseller buraya; mevcut `diagrams/` SVG'leri yerinde kalır).
- `doc/_dashboards/` — 2 Dataview panosu (modül durumu + kod-doküman envanteri), frontmatter'dan canlı.
- `doc/_obsidian_kurulum.md` — kurulum + kullanım dokümanı.
- Health-check + INDEX uyumu (yeni `.md`'ler Dilim A frontmatter'ı alır; `.obsidian/` yok sayılır).

**Kapsam dışı (bilinçli, sonraki dilimler):**
- **Marp / slayt kurulumu → Dilim D.** Marp'ın değeri slayt *çıktısını* görüntülemektir; onu Dilim D üretir. Şimdi kurmak erken (YAGNI). Kurulum dokümanı yalnızca "D'de gelecek" notu düşer.
- `raw/` ingest + `/kb-ingest` → **Dilim C**.
- `/kb-ask` Q&A + çıktı render → **Dilim D**.
- Obsidian Publish / web yayını, mobil senkron, tema geliştirme — kapsam dışı.
- Obsidian'ın kendisini kurmak (kullanıcı zaten kurar) — yalnızca dokümante edilir.

## 2. Doğruluk hiyerarşisi ve kısıtlar

- Dilim A ile aynı: kod > INDEX §0 kanonik > PRD. Bu dilim yalnızca **görüntüleme** ekler; doküman içeriğinin doğruluğunu değiştirmez.
- Committed config **kişiden bağımsız** olmalı: yalnızca paylaşılabilir ayarlar (graph renk grupları, core plugin toggle, ek klasörü) commit'lenir; kişisel oturum durumu (`workspace.json`, açık sekmeler, pencere boyutu, plugin veri klasörleri) **gitignore**'lanır.
- Yeni `.md` dosyaları Dilim A frontmatter şemasına uyar (`_dashboards/*`, `_obsidian_kurulum.md`) → health-check yeşil kalır.
- Görsel konvansiyonu Windows + MacBook'ta çalışır (göreli yollar; platforma özgü mutlak yol yok).

## 3. Vault yapısı ve `.obsidian` config

Vault kökü = `doc/`. Kullanıcı Obsidian'da "Open folder as vault" → `doc/` seçer.

### 3.1 Committed `doc/.obsidian/` dosyaları

- **`app.json`** — `attachmentFolderPath: "_assets"`, `newLinkFormat: "relative"`, `useMarkdownLinks: true` (Obsidian'ın `[[wikilink]]` yerine standart `[](path.md)` linkleri kullanması — mevcut göreli md linkleriyle uyumlu), `alwaysUpdateLinks: true`.
- **`core-plugins.json`** — açık: `graph`, `backlink`, `outgoing-link`, `tag-pane`, `properties`, `outline`, `global-search`, `page-preview`, `file-explorer`, `command-palette`. (Yerleşik; bağımlılık değil.)
- **`graph.json`** — `colorGroups`: `authority` alanına göre renk:
  - `["authority: code"]` → mavi · `["authority: product"]` → gri · `["authority: derived"]` → yeşil · `["authority: archive"]` → soluk/kırık beyaz.
  - (Obsidian graph renk grupları frontmatter/etiket araması ile eşleşir; bu gruplar Dilim A `authority` alanından anında anlam kazanır.)
- **`community-plugins.json`** — `["dataview"]` (kullanıcı Community Plugins'ten kurar; commit yalnızca "önerilir" listesidir, plugin kodu commit'lenmez).
- **`appearance.json`** — minimum/varsayılan (tema dayatmayız; kullanıcı tercihine bırakılır).

### 3.2 `.gitignore` eklemeleri (repo kökü)

```
doc/.obsidian/workspace.json
doc/.obsidian/workspace-mobile.json
doc/.obsidian/plugins/
doc/.obsidian/themes/
doc/.obsidian/snippets/
```

> Yalnızca **paylaşılabilir** config (`app.json`, `core-plugins.json`, `graph.json`, `community-plugins.json`, `appearance.json`) commit edilir; kişisel oturum/plugin verisi izlenmez. Bu, CLAUDE.md "platforma özgü dosyaları main'e commit'leme" kuralıyla hizalıdır.

## 4. Görsel / ek konvansiyonu (`doc/_assets/`)

- Yeni yapıştırılan/indirilen görseller `doc/_assets/` altına gider (Obsidian `attachmentFolderPath` buraya ayarlı → yapıştırınca otomatik oraya kaydeder).
- Mevcut 44 SVG (`doc/diagrams/is_akislari/*.svg`) **yerinde kalır** — taşınmaz (mevcut göreli referanslar bozulmasın).
- Mermaid diyagramları inline kalır (Obsidian natif render eder; ek gerekmez).
- `_assets/` bir `.gitkeep` ile başlar; konvansiyon kurulum dokümanında belgelenir.

## 5. Dataview panoları (`doc/_dashboards/`)

Frontmatter'dan **canlı oto-üretilen** görünümler. Her pano kendi `.md` dosyası, Dilim A frontmatter'ı + bir `dataview` kod bloğu içerir. "⚠️ Dataview gerekir" notu üstte.

### 5.1 `_dashboards/modul_durum_panosu.md`
`authority: code` (veya `status` alanı olan) dokümanları `status`'a göre gruplayan tablo — INDEX §3 modül tablosunun oto-üretilen hali. Örnek sorgu:
```dataview
TABLE status, updated FROM "modules" WHERE status SORT status DESC
```

### 5.2 `_dashboards/kod_dokuman_envanteri.md`
`authority: code` tüm dokümanlar + `code_refs` + `updated` → bayat/riskli doküman avı. Örnek:
```dataview
TABLE code_refs, updated, status WHERE authority = "code" SORT updated ASC
```

> Panolar **ayrı** `_dashboards/` altındadır; çekirdek dokümanlara Dataview sorgusu **gömülmez** (çekirdek dokümanlar plugin-bağımsız/portatif kalır). Dataview kurulu değilse pano dosyaları sorguyu inert kod bloğu olarak gösterir — bozulma yok.

## 6. Kurulum dokümanı (`doc/_obsidian_kurulum.md`)

Dilim A frontmatter'lı (`authority: derived`) kısa rehber:
- Obsidian'ı `doc/` üzerine "Open folder as vault" ile açma.
- Community Plugins → Dataview kurma/etkinleştirme (panolar için).
- Graph renk anlamları (authority: code/product/derived/archive).
- Görsel konvansiyonu (`_assets/`), mermaid'in natif çalıştığı.
- "Marp (slaytlar) Dilim D'de gelecek" notu.
- Committed vs gitignore edilen config ayrımı (neden kişisel dosyalar izlenmez).

## 7. Health-check ve INDEX uyumu

- `.obsidian/` JSON'dur → `kb_healthcheck.sh` yalnızca `*.md` tarar, dokunmaz.
- Yeni `.md`'ler (`_dashboards/modul_durum_panosu.md`, `_dashboards/kod_dokuman_envanteri.md`, `_obsidian_kurulum.md`) Dilim A frontmatter şemasına uyar (`summary`/`tags`/`authority: derived`/`updated`), `Güncelleme:` gövde satırı frontmatter'la eşit.
- Dataview kod blokları ` ```dataview ` dengeli fence → fence kontrolü temiz.
- Bu 3 yeni doküman + `_assets/` INDEX §6.1 "Bilgi Tabanı Makinesi" bölümüne satır olarak eklenir (orphan olmasınlar; CLAUDE.md doküman kuralı).
- Kabul: `bash doc/_tools/kb_healthcheck.sh doc` → **0 RED**, exit 0; yeni dosyalarda 0 FRONTMATTER/DATE bulgusu.

## 8. Kabul kriterleri

- `doc/` Obsidian'da vault olarak açıldığında graph, backlink, etiket paneli, arama çalışır; graph düğümleri `authority`'e göre renklidir.
- `doc/.obsidian/`'da yalnızca paylaşılabilir config commit'li; `workspace.json`/plugin-data gitignore'lı (git status temiz).
- Görsel yapıştırıldığında `_assets/`'e düşer; mevcut SVG/mermaid render'ı bozulmaz.
- Dataview kuruluyken 2 pano frontmatter'dan doğru tablo üretir; kurulu değilken dosyalar bozulmadan (inert) görünür.
- `_obsidian_kurulum.md` yeni bir kullanıcıyı sıfırdan çalışır vault'a götürür.
- Health-check yeşil (0 RED); yeni `.md`'ler INDEX §6.1'de kayıtlı (orphan değil).

## 9. Riskler ve kararlar

- **Dataview community bağımlılığı:** Repo bağımlılığı değil (kullanıcının Obsidian'ında); graceful degrade. Panolar ayrı klasörde → çekirdek portatif kalır. Kabul edildi.
- **`.obsidian` kişisel gürültüsü:** Sıkı gitignore ile yalnızca paylaşılabilir config izlenir.
- **Marp'ı ertelemek:** Dilim D slayt üretene kadar Marp kurulumunun görüntüleyeceği bir şey yok → YAGNI, D'ye bırakıldı.
- **Vault = doc/ (repo kökü değil):** Graph temiz kalır; `docs/superpowers/` spec/plan'ları vault dışında (kabul edildi — onlar meta).

## 10. Sonraki dilimler

- **Dilim C — Ingest:** `raw/` + `/kb-ingest` (dış kaynak → wiki makalesi + backlink + index).
- **Dilim D — Q&A + render:** `/kb-ask` + Marp/matplotlib çıktı (Marp kurulumu burada) + geri dosyalama.
