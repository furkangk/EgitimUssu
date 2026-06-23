# 📚 EğitimÜssü — Doküman Haritası (INDEX)

> **Bu dosyanın amacı:** Projedeki tüm dokümanların **tek listesi**. Yapay zekâya tüm md'leri tek tek vermek yerine
> yalnızca bu dosyayı verin; hangi dokümanın ne işe yaradığını ve ne zaman okunması gerektiğini buradan görür.
>
> **Bu dosya her zaman güncel tutulmalıdır** (yeni doküman eklenince/silinince/amacı değişince) — bkz. kökteki `CLAUDE.md` → "Doküman bakımı".
>
> **Son güncelleme:** 2026-06-24

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
| [`ai_ready_architecture.md`](ai_ready_architecture.md) | Yüksek seviye / erken dönem mimari (soyut) | ⚠️ Eski — kod gerçeği için `modules/00_genel_bakis` |
| [`design.md`](design.md) | Frontend tasarım yaklaşımı (Atomic/CBD, klasör yapısı, faz) | Yön gösterici |

## 2. Roller (`doc/roles/`) — Rol Perspektifi

> Her rolün yetenekleri, kullanıcı yolculuğu, ekranları ve rol-özel kuralları. **Teknik detay modüllerdedir.**

| Doküman | Kapsam |
|---------|--------|
| [`roles/00_roller_genel_bakis.md`](roles/00_roller_genel_bakis.md) | Rol×yetenek matrisi + roller-arası kurallar (mesajlaşma çiftleri, üyelik, veli=gerçek kişi, bireysel-önce strateji) |
| [`roles/ogretmen.md`](roles/ogretmen.md) | 👨‍🏫 Öğretmen — takvim-merkezli yönetim (Faz 1, 🟢) |
| [`roles/ogrenci.md`](roles/ogrenci.md) | 🎓 Öğrenci — bireysel çalışma + gelişim (Faz 2, 🟡) |
| [`roles/veli.md`](roles/veli.md) | 👪 Veli — gelişim/ödeme takibi (Faz 2-3, 🔴) |
| [`roles/admin.md`](roles/admin.md) | 🛡️ Admin — doğrulama, moderasyon, destek |

## 3. Modüller (`doc/modules/`) — Saf Teknik (Koddan Doğrulanmış)

> Her backend modülünün domain modeli, API, iş kuralları, durum ve eksikleri. **Buradan başla:** [`modules/00_genel_bakis.md`](modules/00_genel_bakis.md).

| M | Dosya | Durum | M | Dosya | Durum |
|---|-------|-------|---|-------|-------|
| M01 | [`m01_identity`](modules/m01_identity.md) | 🟢 | M10 | [`m10_progress_tracking`](modules/m10_progress_tracking.md) | 🔴 |
| M02 | [`m02_teachers`](modules/m02_teachers.md) | 🟢 | M11 | [`m11_notifications`](modules/m11_notifications.md) | 🟡 |
| M03 | [`m03_students`](modules/m03_students.md) | 🟢/🟡 | M12 | [`m12_matching`](modules/m12_matching.md) | 🔴 |
| M04 | [`m04_scheduling`](modules/m04_scheduling.md) | 🟢 | M13 | [`m13_reviews`](modules/m13_reviews.md) | 🔴 |
| M05 | [`m05_lesson_sessions`](modules/m05_lesson_sessions.md) | 🟢 | M14 | [`m14_reporting`](modules/m14_reporting.md) | 🔴 |
| M06 | [`m06_assignments`](modules/m06_assignments.md) | 🟢 | M15 | [`m15_settings`](modules/m15_settings.md) | 🟡 |
| M07 | [`m07_payments`](modules/m07_payments.md) | 🟢 | M16 | [`m16_messaging`](modules/m16_messaging.md) | 🔴 yeni |
| M08 | [`m08_study`](modules/m08_study.md) | 🔴 | M17 | [`m17_membership`](modules/m17_membership.md) | 🔴 yeni |
| M09 | [`m09_parents`](modules/m09_parents.md) | 🔴 | M18 | [`m18_feedback`](modules/m18_feedback.md) | 🔴 yeni |

**Çapraz-kesit:** [`modules/00_genel_bakis.md`](modules/00_genel_bakis.md) (indeks + tech stack + endpoint envanteri) · [`modules/mimari_inceleme.md`](modules/mimari_inceleme.md) (hata/güvenlik/öncelik) · [`modules/veri_modeli.md`](modules/veri_modeli.md) (ER şeması).

## 4. Mobil UI / Tasarım Sistemi

| Doküman | Ne işe yarar |
|---------|--------------|
| [`tutormatch_flutter_ui_design.md`](tutormatch_flutter_ui_design.md) | Flutter UI rehberi: renk/tipografi/spacing, ortak widget'lar, 20 ekran tasarımı. ⚠️ §13 (veri modeli) ve §19 (API) idealize/eski — gerçeği modüllerde |
| [`tab_widget.md`](tab_widget.md) | Tab/Segment widget tasarımı + Flutter kodu |

## 5. Sayfa (Ekran) Dokümanları (`doc/pages/`)

> Kodda **var olan** her Flutter ekranı için birer md. İndeks → [`pages/00_pages_index.md`](pages/00_pages_index.md). Öğrenci/veli ve yeni özellik (mesajlaşma/ilan/üyelik) ekranları **planlanandır**.

## 6. Türev / Operasyon Dosyaları

| Dosya | Ne işe yarar |
|-------|--------------|
| [`jira_backlog_from_modules.csv`](jira_backlog_from_modules.csv) | Modül eksiklerinden türetilmiş Jira backlog. Modül docs değişince yeniden türetilmeli |

---

## 7. Hangi Soru → Hangi Doküman?

- **"Ürün ne yapıyor, hangi modül hangi fazda?"** → [`PRD v2.1`](ozel_ders_platformu_PRD_v2.md) §6 + [`modules/00_genel_bakis`](modules/00_genel_bakis.md)
- **"Bu rol neler yapabilir / akışı nedir?"** → [`roles/`](roles/00_roller_genel_bakis.md)
- **"Gerçekte hangi endpoint/domain var?"** → [`modules/00_genel_bakis`](modules/00_genel_bakis.md) §4 + ilgili `mNN_*`
- **"Tablolar nasıl ilişkili?"** → [`modules/veri_modeli`](modules/veri_modeli.md)
- **"Bu ekran nasıl görünmeli / ne yapıyor?"** → [`tutormatch_flutter_ui_design`](tutormatch_flutter_ui_design.md) + [`pages/`](pages/00_pages_index.md)
- **"Hangi açıkları düzeltmeliyim?"** → [`modules/mimari_inceleme`](modules/mimari_inceleme.md)
