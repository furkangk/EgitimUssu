---
title: "Öğretmen Paneli / Dashboard Ekranı"
summary: "Öğretmenin günlük operasyon ana ekranı; günlük özet/yaklaşan dersler/son aktiviteler gerçek veriye bağlı, bekleyen ödev sayısı henüz placeholder"
tags: [sayfa, dashboard, ogretmen]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/dashboard/presentation/pages/dashboard_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-28
---

# Öğretmen Paneli / Dashboard (`/dashboard`)

> **Feature:** `dashboard` · **Dosya:** `mobile/lib/features/dashboard/presentation/pages/dashboard_page.dart`
> **State:** `DashboardCubit` / `DashboardState` · **Veri:** Cubit (`load(teacherUserId)`) — gerçek (Students/Scheduling/Payments) · **Güncelleme:** 2026-06-28

## Amaç
Öğretmenin günlük operasyon ana ekranı: karşılama + günlük özet (streak, bugünün dersi, bekleyen ödev, geciken ödeme), yaklaşan dersler, son aktiviteler, hızlı işlemler.

## State / API
- `DashboardCubit.create()..load(teacherUserId)` → Students/Scheduling/Payments repolarından **gerçek veri** (öğrenci sayısı, bugünkü ders/süre, streak, yaklaşan dersler, son aktiviteler, geciken/bekleyen ödeme tutarları).
- `pendingAssignmentCount` alanı state'te var ama ⚠️ **henüz backend'e bağlı değil** (mobilde öğretmen bazlı ödev listesi yok — bkz. [`../modules/m06_assignments.md`](../modules/m06_assignments.md)); şimdilik placeholder.
- ⚠️ Tek "dashboard özeti" endpoint'i yok; veriler ayrı çağrılardan toplanıyor (bkz. [`../roles/ogretmen.md`](../roles/ogretmen.md) §8).

## Ana bileşenler (2026-06-24 yeniden tasarım)
- **Karşılama başlığı:** "Merhaba, {ad} 👋" + günlük özet alt metni + bildirim ikonu (badge).
- **Durum bantları:** yükleniyor (ince progress), hata (tekrar dene), ve geciken ödeme varsa **aksiyon alınabilir uyarı bandı** (→ Ödemeler).
- **Özet (2×2 metrik kart):** Günlük streak · Bugünün dersleri (→ Takvim) · **Bekleyen ödev** (→ Dersler) · **Geciken ödeme** (→ Ödemeler). Kartlar tıklanabilir.
- **Hızlı işlemler:** Ders Ekle / Ödev Ver / Not Ekle / Ödeme Ekle. "Ders Ekle", takvim ve dersler ekranlarıyla **ortak** `LessonFormSheet`'i modal olarak açar (gezinmeden); form kapanınca dashboard yenilenir. Tüm girişler aynı formu açtığı için tek isim "Ders Ekle" kullanılır.
- **Yaklaşan dersler:** gerçek veriyle yatay liste (online/yüz yüze rozetli); boş durum paneli. Bugünün ve yaklaşan derslerin **her kartında durum rozeti** gösterilir: 🔵 **Planlandı** · 🟠 **Bekliyor** (saati geçmiş ama tamamlanmamış) · 🟢 **Tamamlandı** · 🔴 **İptal** · ⚪ **Taslak**. `status` zinciri `LessonSchedule.status` → `DashboardTodayLesson`/`DashboardUpcomingLesson.status`; rozet mantığı `_dashboardStatusBadge` (her zaman bir rozet döner). Hızlı tamamlama dersler listesinde yapılır (bkz. [`lesson_sessions_list.md`](lesson_sessions_list.md)). Karta tıklayıp açılan detayda tamamlama/düzenleme yapılıp geri dönülürse dashboard `DashboardCubit.load` ile **otomatik yenilenir** (detay `PopScope` ile `true` döndürür; tam `LessonSchedule` artık karttan taşındığı için detay birebir aynı çalışır).
- **Son aktiviteler:** gerçek veriyle liste; boş durum paneli.
- Bottom nav: Ana sayfa · Dersler · Öğrenciler · Takvim · Finans · Diğer.

> Not: Önceki sürümde yaklaşan dersler ve aktiviteler **statik** veriydi; artık `DashboardState`'teki gerçek verilere bağlı.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.3
- Önizleme varyantı: [`dashboard_preview.md`](dashboard_preview.md)
