# Ders Detayı (`/lesson-sessions/detail`)

> **Feature:** `lesson_sessions` · **Dosya:** `mobile/lib/features/lesson_sessions/presentation/pages/lesson_detail_page.dart`
> **State:** Stateful (tab) · **Veri:** ⚠️ Demo/UI · **Güncelleme:** 2026-06-23

## Amaç
Tek dersin detayı; sekmeler: Ders Notu / Ödevler / Ödeme. Ders meta verisi, ekler, ilgili ödevler, ödeme durumu.

## Route
- Veri `route.extra` ile `LessonDetailPayload` olarak gelir: `studentName`, `subject`, `dateLabel`, `timeLabel`, `modeLabel`, `accent`.

## Veri / API
- ⚠️ Şu an demo/UI; backend bağlantısı yok. İlgili: `GET /api/lesson-sessions/{id}`, `.../follow-up`.

## İlgili
- Modül: [`../modules/m05_lesson_sessions.md`](../modules/m05_lesson_sessions.md) (M05/M06)
