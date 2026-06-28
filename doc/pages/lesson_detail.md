# Ders Detayı (`/lesson-sessions/detail`)

> **Feature:** `lesson_sessions` · **Dosya:** `mobile/lib/features/lesson_sessions/presentation/pages/lesson_detail_page.dart`
> **State:** Stateful (tab) · **Veri:** ⚠️ Demo/UI · **Güncelleme:** 2026-06-28

## Amaç
Tek dersin detayı; sekmeler: Ders Notu / Ödevler / Ödeme. Ders meta verisi, ekler, ilgili ödevler, ödeme durumu.

## Route
- Veri `route.extra` ile `LessonDetailPayload` olarak gelir: `studentName`, `subject`, `dateLabel`, `timeLabel`, `modeLabel`, `accent`, `lessonId?`, `lessonStatus?`, `meetingUrl?`, **`lesson?`** (tam `LessonSchedule` — kalıcı düzenleme için).

## Davranış
- **Online + `meetingUrl` doluysa** "Toplantıya Katıl" kartı görünür; tıklayınca link panoya kopyalanır (`Clipboard`; URL'yi açmak için ileride `url_launcher` eklenebilir). `meetingUrl` hem dersler listesinden hem **ana sayfa ders kartlarından** taşınır (dashboard: `DashboardTodayLesson`/`DashboardUpcomingLesson` → `meetingUrl`).
- **Dersi Düzenle:** Payload'da tam `lesson` (LessonSchedule) varsa — yani dersler listesinden açıldıysa — ortak **`LessonFormSheet` edit modu** açılır ve `SchedulingCubit.updateLesson` ile **kalıcı** olarak günceller (`PUT /api/scheduling/lessons/{id}`). Edit modunda öğrenci salt-okunur, konu serbest metin, tekrar seçimi gizli; tarih/saat seçicileri + format + online toplantı linki vardır. Kaydedince detay kartı güncel derse göre yenilenir.
- **Kozmetik düzenleme (fallback):** `lesson` yoksa (demo veya dashboard kartından açılan detay) eski sheet kullanılır; yalnızca detay kartını (`_editedPayload`) günceller, kalıcı değildir. Bu sheet'te de tarih/saat seçicileri ve segmented format var (eski `DropdownButtonFormField` kaldırılmıştı — `_dependents.isEmpty` hatası giderildi).

## Veri / API
- ⚠️ Şu an demo/UI; backend bağlantısı yok. İlgili: `GET /api/lesson-sessions/{id}`, `.../follow-up`.

## İlgili
- Modül: [`../modules/m05_lesson_sessions.md`](../modules/m05_lesson_sessions.md) (M05/M06)
