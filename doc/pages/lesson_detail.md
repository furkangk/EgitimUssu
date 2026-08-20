---
title: "Ders Detayı Ekranı"
summary: "Tek dersin detayı (Ders Notu/Ödevler/Ödeme sekmeleri); tamamlama ve düzenleme gerçek backend'e bağlı, diğer sekmeler demo/UI"
tags: [sayfa, lesson-sessions, ogretmen]
status: "🔴"
authority: code
code_refs:
  - mobile/lib/features/lesson_sessions/presentation/pages/lesson_detail_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-28
---

# Ders Detayı (`/lesson-sessions/detail`)

> **Feature:** `lesson_sessions` · **Dosya:** `mobile/lib/features/lesson_sessions/presentation/pages/lesson_detail_page.dart`
> **State:** Stateful (tab) · **Veri:** ⚠️ Demo/UI · **Güncelleme:** 2026-06-28

## Amaç
Tek dersin detayı; sekmeler: Ders Notu / Ödevler / Ödeme. Ders meta verisi, ekler, ilgili ödevler, ödeme durumu.

## Route
- Veri `route.extra` ile `LessonDetailPayload` olarak gelir: `studentName`, `subject`, `dateLabel`, `timeLabel`, `modeLabel`, `accent`, `lessonId?`, `lessonStatus?`, `meetingUrl?`, **`lesson?`** (tam `LessonSchedule` — kalıcı düzenleme için).
- Tek sayfa/route'tur (`/lesson-sessions/detail`); hem **Dersler listesi** hem **Ana sayfa** ders kartları aynı sayfayı açar. İkisi de artık tam payload (`lessonId` + `lesson`) gönderir → tamamlama butonu, kalıcı düzenleme ve doğru "Bekliyor/Tamamlandı" durumu **her iki girişte birebir aynı** çalışır. (Önceden ana sayfa ince payload gönderiyordu; `DashboardTodayLesson`/`DashboardUpcomingLesson.lesson` alanı eklenerek giderildi.)

## Davranış
- **Online + `meetingUrl` doluysa** "Toplantıya Katıl" kartı görünür; tıklayınca link panoya kopyalanır (`Clipboard`; URL'yi açmak için ileride `url_launcher` eklenebilir). `meetingUrl` hem dersler listesinden hem **ana sayfa ders kartlarından** taşınır (dashboard: `DashboardTodayLesson`/`DashboardUpcomingLesson` → `meetingUrl`).
- **Dersi Düzenle:** Payload'da tam `lesson` (LessonSchedule) varsa — yani dersler listesinden açıldıysa — ortak **`LessonFormSheet` edit modu** açılır ve `SchedulingCubit.updateLesson` ile **kalıcı** olarak günceller (`PUT /api/scheduling/lessons/{id}`). Edit modunda öğrenci salt-okunur, konu serbest metin, tekrar seçimi gizli; tarih/saat seçicileri + format + online toplantı linki vardır. Kaydedince detay kartı güncel derse göre yenilenir.
- **Kozmetik düzenleme (fallback):** `lesson` yoksa (demo veya dashboard kartından açılan detay) eski sheet kullanılır; yalnızca detay kartını (`_editedPayload`) günceller, kalıcı değildir. Bu sheet'te de tarih/saat seçicileri ve segmented format var (eski `DropdownButtonFormField` kaldırılmıştı — `_dependents.isEmpty` hatası giderildi).
- **Durum rozeti:** Başlık kartında (`_HeroCard`) dersin durumu **her zaman** gösterilir: **Planlandı** (mavi) · **Bekliyor** (amber — başlama saati geçmiş planlı ders) · **Tamamlandı** (yeşil) · **İptal edildi** (kırmızı) · **Taslak** (gri). Durum bilinmiyorsa (payload'da `lessonStatus` yoksa) rozet gizlenir.
- **Dersi Tamamla:** `lessonId != null && status == 'Planned'` ise aksiyon kartlarının altında yeşil "Dersi Tamamla" butonu görünür; `SchedulingCubit.completeLesson` → `POST /api/scheduling/lessons/{id}/complete`. Tamamlanınca durum `Completed` olur ve başlık rozeti **Tamamlandı**'ya döner. Aynı hızlı tamamlama dersler listesinde kart üzerinden de yapılabilir (bkz. [`lesson_sessions_list.md`](lesson_sessions_list.md)).
- **Kaynağı tazeleme:** Detayda kalıcı bir değişiklik (tamamlama veya kalıcı düzenleme) olursa `_didChange` işaretlenir ve geri dönülürken `PopScope` ile pop sonucu `true` döndürülür. Kaynak ekranlar (dersler listesi → `SchedulingCubit.loadForCalendar`, ana sayfa → `DashboardCubit.load`) bu sonucu bekleyip **otomatik yenilenir**. Değişiklik yoksa (yalnızca görüntüleme) yenileme yapılmaz.

## Veri / API
- Ders notu/ödev/ödeme sekmeleri ⚠️ demo/UI. **Tamamlama ve düzenleme** ise `SchedulingCubit` üzerinden gerçek backend'e bağlı (`lesson`/`lessonId` doluysa): `POST /api/scheduling/lessons/{id}/complete`, `PUT /api/scheduling/lessons/{id}`.

## İlgili
- Modül: [`../modules/m05_lesson_sessions.md`](../modules/m05_lesson_sessions.md) (M05/M06)
