# Ödev / Ders Sonrası Takip (`/assignments/new`, `/assignments/:lessonSessionId`)

> **Feature:** `assignments` · **Dosya:** `mobile/lib/features/assignments/presentation/pages/assignment_follow_up_page.dart`
> **State:** `AssignmentFollowUpCubit` / `AssignmentFollowUpState` · **Veri:** Gerçek API · **Güncelleme:** 2026-06-23

## Amaç
Ödev oluşturma/takip: öğrenci/ders seçimi, başlık, açıklama, son tarih, opsiyonel dosya. Dashboard'dan veya ders detayından açılır.

## Route
- `/assignments/new` (lessonSessionId boş) veya `/assignments/:lessonSessionId`.
- `AssignmentFormContext` (studentName, lessonName, lockSelection) ile ön-doldurma.

## State / API
- `AssignmentFollowUpCubit.create()` → `POST /api/assignments/lesson-sessions/{lessonSessionId}/follow-up`.

## İlgili
- Tasarım: [`../tutormatch_flutter_ui_design.md`](../tutormatch_flutter_ui_design.md) §10.9 · Modül: M06
