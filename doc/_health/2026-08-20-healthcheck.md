---
title: "Health-check 2026-08-20"
summary: "İlk kb-healthcheck raporu — Faz 1 deterministik temiz (0 RED); --deep hedefli kod-drift 6 dokümanı taradı, 5 gerçek drift bulundu ve düzeltildi"
tags: [kb, health, rapor]
authority: derived
updated: 2026-08-20
---

# 🩺 Health-check Raporu — 2026-08-20

> Bilgi Tabanı Makinesi Dilim A'nın ilk sağlık taraması. `bash doc/_tools/kb_healthcheck.sh doc` (Faz 1) +
> `/kb-healthcheck --deep` (Faz 2, kod-drift fan-out — hedefli).

## Özet

| Faz | Sonuç |
|-----|-------|
| **Faz 1 (deterministik)** | ✅ **0 RED**, 0 YELLOW, 25 BLUE (orphan), `exit=0` |
| **Faz 2 (--deep kod-drift)** | 6 kod-dokümanı tarandı → **5 gerçek drift** bulundu → **hepsi düzeltildi** (commit `f20a69c`) |

Faz 1 temizliği (2 gün önce, elle) sonrası deterministik taban temiz. `--deep` fan-out, elle kaçırılan
**5 kod↔doküman sapmasını** yakaladı — makinenin ilk somut getirisi.

## Faz 1 — deterministik (tüm doc/)

- Kırık link: **0** · Kapanmamış fence: **0** · Kanonik ihlal (ad-yazımı çift-t / .NET sürüm / ana renk): **0**
- Frontmatter şema (74 dosya): **0 eksik** · code_refs çözünürlük: **0 kırık** · Gövde↔frontmatter tarih: **0 çelişki**
- 🔵 **25 BLUE ORPHAN** — root `INDEX.md`'de doğrudan linki olmayan dosyalar. **Yanlış-pozitif:** bunların
  tamamı bir **section-index**'te linklidir (`pages/00_pages_index.md` tüm sayfa md'lerini, `architecture/00_genel_bakis.md`
  rehber dokümanları listeler). Kök INDEX bilinçli olarak leaf'lere değil section-index'lere işaret eder.
  → **Öneri (küçük follow-up):** `kb_healthcheck.sh` check-7'yi, dosyayı herhangi bir index (`INDEX.md` **veya**
  `**/00_*.md` / `**/README.md`) linkliyorsa öksüz saymayacak şekilde genişlet; o zaman baz çizgisi 0 bulguya iner.

## Faz 2 — kod-drift (--deep, hedefli)

**Kapsam (şeffaflık — tam-filo değil):** 40 `authority: code` dokümanın **6'sı** tarandı. Faz 1'in modül
(Geçiş 2) + mimari/sayfa (Geçiş 3) derin senkronu 2 gün önce yapıldığı için tam 40-doküman sweep düşük marjinal
değerdeydi; bilinen-risk + temsili örnek hedeflendi. Kalan 34 kod-dokümanı için `/kb-healthcheck --deep` istenildiğinde çalıştırılabilir.

Taranan: `modules/m08_study` (ajan), `architecture/widgets` (ajan), `architecture/mobile_flutter` (ajan),
`modules/veri_modeli` (deterministik), `pages/payments_list` + `pages/students_list` (deterministik).

### Bulgular (hepsi düzeltildi — commit `f20a69c`)

| # | Doküman | Severity | Sapma | Düzeltme |
|---|---------|----------|-------|----------|
| 1 | `modules/veri_modeli.md` | 🔴 | Parents enum'u `NotificationChannel` yazıyordu; kod tipi **`ParentNotificationChannel`**. Ayrıca "aynı adlı enum'dan ayrıdır" notu yanlış (adları farklı) | Enum adı düzeltildi; not "farklı adlı tipler" olarak netleştirildi |
| 2 | `pages/payments_list.md` (+ index) | 🟡 | Durum 🔴 (tamamen demo) iddiası; kod `/api/payments/records` **gerçek API + demo fallback** | Durum 🔴→🟡 (frontmatter + index rozeti) |
| 3 | `pages/students_list.md` (+ index) | 🟡 | Durum 🔴; kod `/api/students/profiles` **gerçek API + demo fallback** | Durum 🔴→🟡 (frontmatter + index rozeti) |
| 4 | `architecture/widgets.md` | 🔴 | `StudentBottomNav` girişi bayat: olmayan `StudentPlaceholderPage` + eski sekme adları (Ana Sayfa/Ders Programı/İstatistik/Diğer) | Gerçek 4 sekmeye (Çalışma/Derslerim/Performans/Profil, gerçek rotalar) güncellendi, durum 🟢; AppTextField'a `textCapitalization` eklendi |
| 5 | `architecture/mobile_flutter.md` | 🔴 | §13 `study`(M08)/`progress`(M10) feature'larını "Planlanan" diyor; kodda **tam uygulanmış** (doküman kendi §8/giriş ile de çelişiyor). Feature listesi `notifications`/`progress`/`study`/`parent` eksik | study/progress "mevcut" yapıldı; feature listesi tamamlandı |

`modules/m08_study.md` → **TEMİZ** (38 endpoint + domain + enum'lar kodla birebir).

## Kalan / öneriler

- **check-7 orphan iyileştirmesi** (yukarıda) → baz çizgisini 0 bulguya indirir.
- **Tam-filo --deep**: kalan 34 `authority: code` dokümanı henüz taranmadı; ihtiyaç halinde çalıştırılabilir.
- Bu 5 drift, Faz 1'in **elle** derin-senkronunun bile kaçırdıklarıydı → tekrarlı `/kb-healthcheck --deep` kod
  değiştikçe doküman çürümesini erken yakalar (makinenin amacı).

*Güncelleme: 2026-08-20*
