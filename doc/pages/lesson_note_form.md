# Ders Notu Formu (`/lesson-notes/new`)

> **Feature:** `lesson_sessions` · **Dosya:** `mobile/lib/features/lesson_sessions/presentation/pages/lesson_note_form_page.dart`
> **State:** Stateful (form) · **Veri:** ⚠️ Demo/UI · **Güncelleme:** 2026-06-23

## Amaç
Yeni ders notu oluşturma: öğrenci/ders seçimi, başlık, not içeriği, dosya eki.

## Notlar
- Opsiyonel `LessonNoteFormContext` (studentName, lessonName, lockSelection) ile alanlar ön-doldurulabilir.
- ⚠️ Backend bağlantısı yok. İlgili: `POST /api/assignments/lesson-sessions/{id}/follow-up` (not+ödev birleşik).

## İlgili
- Tasarım: [`../tutormatch_flutter_ui_design.md`](../tutormatch_flutter_ui_design.md) §10.8 · Modül: [`../modules/m06_assignments.md`](../modules/m06_assignments.md) (M06)
