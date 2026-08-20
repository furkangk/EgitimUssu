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
| `authority` | evet | `code` \| `product` \| `derived` \| `archive` \| `reference` |
| `code_refs` | koşullu | `authority: code` ise ≥1 glob; diğerlerinde boş/atlanır |
| `source` | koşullu | `authority: reference` ise zorunlu: `raw/<dosya>`, repo dosyası veya URL |
| `subtype` | koşullu | `authority: reference` ise: `research` \| `design` \| `decision` |
| `updated` | evet | ISO tarih; kaynak: gövde Güncelleme:/Son güncelleme: → yoksa Tarih:/sürüm tarihi → yoksa migrasyon tarihi |

## authority değerleri

- **code** — Kod doğruluk kaynağı; `code_refs`'e karşı endpoint/enum/domain drift denetlenir.
- **product** — Ürün niyeti/plan; kod karşılığı yok, drift denetlenmez (PRD, yol haritası, planlanan modüller m16–m18).
- **derived** — Başka dokümandan türer; dolaylı tutarlılık (roller, index, denetim, rehber dokümanlar).
- **archive** — `doc/_arsiv/*`; drift/lint atlanır (yalnız kırık link + fence).
- **reference** — Dış/ham kaynaktan damıtılmış, kaynaklı makale (`doc/kaynaklar/`). `source` + `subtype` zorunlu; kod-drift'e tabi değil; kanonik gerçeği ezmez. Orijinal `doc/raw/`'da (verbatim, health-check muaf).

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
