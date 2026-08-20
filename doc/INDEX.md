---
title: "Doküman Haritası (INDEX)"
summary: "doc/ altındaki tüm dokümanların tek listesi ve kanonik gerçekler; hangi konuda hangi dokümana bakılacağını gösterir"
tags: [index, harita, kanonik]
authority: derived
updated: 2026-08-20
---

# 📚 EğitimÜssü — Doküman Haritası (INDEX)

> **Bu dosyanın amacı:** Projedeki tüm dokümanların **tek listesi**. Yapay zekâya tüm md'leri tek tek vermek yerine
> yalnızca bu dosyayı verin; hangi dokümanın ne işe yaradığını ve ne zaman okunması gerektiğini buradan görür.
>
> **Bu dosya her zaman güncel tutulmalıdır** (yeni doküman eklenince/silinince/amacı değişince) — bkz. kökteki `CLAUDE.md` → "Doküman bakımı".
>
> **Son güncelleme:** 2026-08-20 (Bilgi Tabanı Dilim A — frontmatter + health-check; Dilim B — Obsidian görünümü; Faz 1 tamam — m01–m18 + mimari/sayfa/rol/INDEX koddan doğrulandı)

---

## 0. Kanonik Gerçekler (Tek Doğruluk Kaynağı)

> Çelişki halinde bunlar esastır. Dokümanlarda farklı bir değer görürseniz yanlıştır, düzeltin.

| Konu | Değer |
|------|-------|
| Görünen ad | **EğitimÜssü** |
| ASCII / kod / dosya adı | **EgitimUssu** (`EgittimUssu` çift-t YANLIŞ) |
| Backend | **.NET 9** modüler monolit (`global.json` → SDK 9.0.311) |
| Mobil | **Flutter** (`flutter_bloc`/Cubit, `go_router`, `dio`, `get_it`) |
| Web | Angular (planlandı — Faz 4-5) |
| Ana renk (primary) | **`0xFF082B4F`** |
| Veritabanı | PostgreSQL (modül başına ayrı şema + DbContext), Redis cache |
| PRD sürümü | **v2.1** (promp.txt vizyonu işlendi) |

---

## 1. Üst Düzey Ürün ve Mimari

| Doküman | Ne işe yarar | Otorite |
|---------|--------------|---------|
| [`promp.txt`](promp.txt) | Kullanıcının kendi sözleriyle proje vizyonu (kaynak girdi) | Vizyon kaynağı |
| [`ozel_ders_platformu_PRD_v2.md`](ozel_ders_platformu_PRD_v2.md) | **PRD v2.1**: vizyon, kullanıcılar, M01–M18 modül listesi, 6 fazlı yol haritası, iş modeli, free/premium, reklam/kampanya | Ürün için **esas** |
| [`yol_haritasi.md`](yol_haritasi.md) | **Geliştirme yol haritası**: Faz 0–5, epic→faz eşlemesi, bağımlılıklar, milestone'lar | Planlama |
| [`architecture/00_genel_bakis.md`](architecture/00_genel_bakis.md) | **Mimari genel bakış**: platformlar, aktörler, katmanlar, veri akışı, event/Outbox, ölçeklenebilirlik, fazlar | Mimari için **buradan başla** |

## 1.1 Mimari (`doc/architecture/`) — Platforma Göre, Koddan Doğrulanmış

> Eski tek-parça `ai_ready_architecture.md` + `design.md` + `tutormatch_flutter_ui_design.md` bu klasöre bölündü/birleştirildi.

| Doküman | Kapsam |
|---------|--------|
| [`architecture/00_genel_bakis.md`](architecture/00_genel_bakis.md) | Sistem geneli + bu klasörün haritası |
| [`architecture/backend.md`](architecture/backend.md) | .NET 9 modüler monolit: çözüm yapısı, modül anatomisi, Shared/Kernel, CQRS, Outbox, persistence, JWT |
| [`architecture/mobile_flutter.md`](architecture/mobile_flutter.md) | Flutter mimari (bloc/get_it/go_router/dio) + tasarım uygulaması + 20 ekran görsel rehberi |
| [`architecture/web_angular.md`](architecture/web_angular.md) | Angular web — 🔴 planlanan (Faz 4-5) |
| [`architecture/design_system.md`](architecture/design_system.md) | Platformlar-arası ortak görsel token (renk/tipografi/spacing) + Atomic/CBD — **token tek doğruluk kaynağı** |
| [`architecture/widgets.md`](architecture/widgets.md) | Ortak widget kataloğu: paylaşılan bileşenlerin API + kural + durumu (🟢/🟡/🔴) |

## 2. Roller (`doc/roles/`) — Rol Perspektifi

> Her rolün yetenekleri, kullanıcı yolculuğu, ekranları ve rol-özel kuralları. **Teknik detay modüllerdedir.**

| Doküman | Kapsam |
|---------|--------|
| [`roles/00_roller_genel_bakis.md`](roles/00_roller_genel_bakis.md) | Rol×yetenek matrisi + roller-arası kurallar (mesajlaşma çiftleri, üyelik, veli=gerçek kişi, bireysel-önce strateji) |
| [`roles/ogretmen.md`](roles/ogretmen.md) | 👨‍🏫 Öğretmen — takvim-merkezli yönetim (Faz 1, 🟢) |
| [`roles/ogrenci.md`](roles/ogrenci.md) | 🎓 Öğrenci — bireysel çalışma + gelişim (Faz 2, 🟡) |
| [`roles/ogrenci_ux.md`](roles/ogrenci_ux.md) | 🎓 Öğrenci Deneyimi (Student UX) — günlük kullanım/motivasyon odaklı UX vizyonu + ekran hedefleri (🟡 vizyon) |
| [`roles/veli.md`](roles/veli.md) | 👪 Veli — gelişim/ödeme takibi (Faz 2-3, 🟡 Faz 2 uygulandı) |
| [`roles/admin.md`](roles/admin.md) | 🛡️ Admin — doğrulama, moderasyon, destek |

## 3. Modüller (`doc/modules/`) — Saf Teknik (Koddan Doğrulanmış)

> Her backend modülünün domain modeli, API, iş kuralları, durum ve eksikleri. **Buradan başla:** [`modules/00_genel_bakis.md`](modules/00_genel_bakis.md).

| M | Dosya | Durum | M | Dosya | Durum |
|---|-------|-------|---|-------|-------|
| M01 | [`m01_identity`](modules/m01_identity.md) | 🟢 | M10 | [`m10_progress_tracking`](modules/m10_progress_tracking.md) | 🟡 |
| M02 | [`m02_teachers`](modules/m02_teachers.md) | 🟢 | M11 | [`m11_notifications`](modules/m11_notifications.md) | 🟡 |
| M03 | [`m03_students`](modules/m03_students.md) | 🟢/🟡 | M12 | [`m12_matching`](modules/m12_matching.md) | 🔴 |
| M04 | [`m04_scheduling`](modules/m04_scheduling.md) | 🟢 | M13 | [`m13_reviews`](modules/m13_reviews.md) | 🔴 |
| M05 | [`m05_lesson_sessions`](modules/m05_lesson_sessions.md) | 🟢 | M14 | [`m14_reporting`](modules/m14_reporting.md) | 🔴 |
| M06 | [`m06_assignments`](modules/m06_assignments.md) | 🟢 | M15 | [`m15_settings`](modules/m15_settings.md) | 🟡 |
| M07 | [`m07_payments`](modules/m07_payments.md) | 🟢 | M16 | [`m16_messaging`](modules/m16_messaging.md) | 🔴 yeni |
| M08 | [`m08_study`](modules/m08_study.md) | 🟢 | M17 | [`m17_membership`](modules/m17_membership.md) | 🔴 yeni |
| M09 | [`m09_parents`](modules/m09_parents.md) | 🟢 | M18 | [`m18_feedback`](modules/m18_feedback.md) | 🔴 yeni |

**Çapraz-kesit:** [`modules/00_genel_bakis.md`](modules/00_genel_bakis.md) (indeks + tech stack + endpoint envanteri) · [`modules/mimari_inceleme.md`](modules/mimari_inceleme.md) (hata/güvenlik/öncelik) · [`modules/veri_modeli.md`](modules/veri_modeli.md) (ER şeması).

## 4. Mobil UI / Tasarım Sistemi

| Doküman | Ne işe yarar |
|---------|--------------|
| [`architecture/mobile_flutter.md`](architecture/mobile_flutter.md) | Flutter mimari + UI rehberi: renk/tipografi/spacing, ortak widget'lar, **20 ekran** (§13). ⚠️ §14 veri modeli idealize — gerçeği modüllerde |
| [`architecture/design_system.md`](architecture/design_system.md) | Ortak görsel token + Atomic/CBD (Flutter & Angular) — token tek doğruluk kaynağı |
| [`architecture/widgets.md`](architecture/widgets.md) | **Ortak widget kataloğu**: paylaşılan bileşenlerin API + kural + durumu (🟢/🟡/🔴) |
| [`tab_widget.md`](tab_widget.md) | Tab/Segment widget tasarımı + Flutter kodu (katalogda `AppSegmentedTab`) |

## 5. Sayfa (Ekran) Dokümanları (`doc/pages/`)

> Kodda **var olan** her Flutter ekranı için birer md. İndeks → [`pages/00_pages_index.md`](pages/00_pages_index.md). Öğretmen, **öğrenci** (4-sekme çalışma paneli) ve **veli** (parent paneli) ekranları kodda mevcuttur; yalnızca yeni özellik (mesajlaşma/ilan/üyelik/paywall) ekranları **planlanandır**.

## 5.1 Rol Sayfa Mimarisi Diyagramları (`doc/diagrams/rol_sayfa_mimarisi/`)

> Her rol için **fonksiyonel dokümandan türetilen** (uygulamadan değil) sayfa yapısı (IA), içerik ve ilişki/veri-akışı diyagramları (mermaid). Kaynak fonksiyonel dokümanlar **arşivlendi** (2026-08-19) → `doc/_arsiv/*_rolu_fonksiyonel_dokuman_v1.md`; güncel otorite `doc/roles/`'tedir. İndeks → [`rol_sayfa_mimarisi/README.md`](diagrams/rol_sayfa_mimarisi/README.md).

| Rol | Dosya | Kaynak fonksiyonel doküman (⚠️ arşiv) |
|-----|-------|----------------------------|
| 🎓 Öğrenci | [`ogrenci`](diagrams/rol_sayfa_mimarisi/ogrenci.md) | [`_arsiv/ogrenci_rolu_fonksiyonel_dokuman_v1`](_arsiv/ogrenci_rolu_fonksiyonel_dokuman_v1.md) |
| 👨‍🏫 Öğretmen | [`ogretmen`](diagrams/rol_sayfa_mimarisi/ogretmen.md) | [`_arsiv/ogretmen_rolu_fonksiyonel_dokuman_v1`](_arsiv/ogretmen_rolu_fonksiyonel_dokuman_v1.md) |
| 👪 Veli | [`veli`](diagrams/rol_sayfa_mimarisi/veli.md) | [`_arsiv/veli_rolu_fonksiyonel_dokuman_v1`](_arsiv/veli_rolu_fonksiyonel_dokuman_v1.md) |

## 6. Türev / Operasyon Dosyaları

| Dosya | Ne işe yarar |
|-------|--------------|
| [`jira_backlog_from_modules.csv`](jira_backlog_from_modules.csv) | Jira backlog (Epic/Story/Task). Her görevde `faz-N` etiketi → [`yol_haritasi.md`](yol_haritasi.md) ile hizalı. Modül docs değişince güncellenmeli |
| [`denetim/2026-06-30_kapsamli_kod_denetimi.md`](denetim/2026-06-30_kapsamli_kod_denetimi.md) | **Kapsamlı kod denetimi (2026-06-30)**: mimari/güvenlik/DDD/persistence/mobil/operasyon bulguları, skor tablosu, önceliklendirilmiş yol haritası. Anlık denetim artefaktı |
| [`_arsiv/`](_arsiv/) | **Arşiv (⚠️ tarihî):** PRD v2.0'dan türetilen eski fonksiyonel/iş-akışı dokümanları (`*_rolu_fonksiyonel_dokuman_v1.md`, `is_akislari.md`). Geçmiş referans; güncel otorite `doc/roles/` + `doc/modules/`. 2026-08-19'da birleştirilip arşivlendi |

## 6.1 Bilgi Tabanı Makinesi (Dilim A + B)

> Dokümanları koddan doğrulanmış, gezilebilir tutan health-check makinesi. Her `doc/**/*.md` başında makine-okunur frontmatter bulunur. Tasarım/plan: `docs/superpowers/specs|plans/2026-08-20-bilgi-tabani-dilim-a*`.

| Dosya | Ne işe yarar |
|-------|--------------|
| [`00_kb_konvansiyon.md`](00_kb_konvansiyon.md) | **Frontmatter konvansiyonu**: şema (summary/tags/status/authority/code_refs/updated) + authority kuralları + code_refs aile kalıpları. Tüm doc/ bunu izler |
| `_tools/kb_healthcheck.sh` | Deterministik health-check (saf bash): kırık link/fence/kanonik/frontmatter/code_refs/tarih/öksüz. `/kb-healthcheck` slash-komutu bunu sarar (+ `--deep` LLM kod-drift) |
| [`_health/`](_health/) | Health-check rapor çıktıları (`YYYY-MM-DD-healthcheck.md`) — pass/fail, severity'li bulgular, tespit edilen kod-drift |
| [`_obsidian_kurulum.md`](_obsidian_kurulum.md) | **Obsidian görünümü** (Dilim B): vault'u açma, Dataview kurulumu, graph renk anlamları, `_assets` görsel konvansiyonu |
| [`_dashboards/`](_dashboards/) | Dataview panoları (frontmatter'dan canlı): `modul_durum_panosu.md` (INDEX §3'ün oto-üretilen hali) + `kod_dokuman_envanteri.md` (drift riski) |
| `.obsidian/`, `_assets/` | Obsidian vault config (authority-renkli graph, ek klasörü) + görsel/ek klasörü. Kişisel dosyalar `.gitignore`'da |

---

## 7. Hangi Soru → Hangi Doküman?

- **"Ürün ne yapıyor, hangi modül hangi fazda?"** → [`PRD v2.1`](ozel_ders_platformu_PRD_v2.md) §6 + [`modules/00_genel_bakis`](modules/00_genel_bakis.md)
- **"Bu rol neler yapabilir / akışı nedir?"** → [`roles/`](roles/00_roller_genel_bakis.md)
- **"Gerçekte hangi endpoint/domain var?"** → [`modules/00_genel_bakis`](modules/00_genel_bakis.md) §4 + ilgili `mNN_*`
- **"Tablolar nasıl ilişkili?"** → [`modules/veri_modeli`](modules/veri_modeli.md)
- **"Sistem mimarisi / katmanlar / event akışı nasıl?"** → [`architecture/00_genel_bakis`](architecture/00_genel_bakis.md) (backend → `architecture/backend.md`, mobil → `architecture/mobile_flutter.md`)
- **"Bu ekran nasıl görünmeli / ne yapıyor?"** → [`architecture/mobile_flutter`](architecture/mobile_flutter.md) §13 + [`pages/`](pages/00_pages_index.md)
- **"Hangi açıkları düzeltmeliyim?"** → [`modules/mimari_inceleme`](modules/mimari_inceleme.md)
- **"Hangi sırayla / hangi fazda geliştireyim?"** → [`yol_haritasi.md`](yol_haritasi.md) + [`jira_backlog_from_modules.csv`](jira_backlog_from_modules.csv)
