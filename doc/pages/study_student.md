# 📱 Öğrenci — Bireysel Çalışma Ekranları (M08 `study` feature)

> **Feature:** `mobile/lib/features/study/` (`data` / `domain` / `presentation`) · **Backend:** `/api/study` (bkz. [`../modules/m08_study.md`](../modules/m08_study.md))
> **Rol:** Öğrenci (`Student`). Rol bazlı `redirect` öğrenciyi `/student-home`'a yönlendirir. Öğrenci StudentId'si M03 `by-user` ile çözülür; profil yoksa `SelfRegistered` olarak oluşturulur.
> **Veri:** 🟢 gerçek API. **Güncelleme:** 2026-07-04

---

## Ekranlar

| Route | Sayfa (dosya) | State | İçerik |
|-------|---------------|-------|--------|
| `/student-home` | `student_home_page.dart` (`StudyHomeCubit`) | dashboard | Selamlama, bugünkü hedef ilerlemesi (gradient kart), streak/hafta/rekor stat'ları, hızlı işlem ızgarası, son deneme + son çalışmalar. Pull-to-refresh. |
| `/study/timer` | `study_timer_page.dart` (`StudyTimerCubit`) | aktif seans | Ders/konu seçimi → başlat; canlı kronometre (HH:MM:SS), mola/devam, bitir, iptal. Açılışta devam eden seansı geri yükler. |
| `/study/test` | `test_entry_page.dart` | form | Ders/konu/deneme adı/tür + toplam/doğru/yanlış/boş + süre; **canlı net önizleme**; `d+y+b=toplam` doğrulaması. |
| `/study/goals` | `study_goals_page.dart` | form | Günlük/haftalık/net hedef; veli & öğretmenle **paylaşım anahtarları** (çalışma/test ayrı). |
| `/study/history` | `study_history_page.dart` | sekmeler | Seanslar / Denemeler / Haftalık (gün-bazlı çubuk grafik + ders dağılımı) + **manuel seans** ekleme (bottom sheet). |
| `/study/achievements` | `achievements_page.dart` | liste | Rozet kataloğu + kazanım durumu + eşik ilerlemesi. |

## Navigasyon
- `student_home_page` hızlı işlem ızgarasından alt ekranlara `context.push('<route>?studentId=<id>')` ile gidilir (studentId query param).
- `app_router.dart`: `Student` (öğretmen değil) → `/student-home`; öğretmene özel ekranlara düşerse geri alınır.

## İlgili
- Teknik modül → [`../modules/m08_study.md`](../modules/m08_study.md) · Rol → [`../roles/ogrenci.md`](../roles/ogrenci.md)

---
*Öğrenci Bireysel Çalışma Ekranları | Güncelleme: 2026-07-04*
