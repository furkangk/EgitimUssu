---
title: "Ders Notu Formu Ekranı"
summary: "Yeni ders notu oluşturma formu; öğrenci/ders seçimi, başlık, içerik, dosya eki — backend bağlantısı yok, tamamen demo/UI"
tags: [sayfa, lesson-sessions, note, demo]
status: "🔴"
authority: code
code_refs:
  - mobile/lib/features/lesson_sessions/presentation/pages/lesson_note_form_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-23
---

# Ders Notu Formu (`/lesson-notes/new`)

> **Feature:** `lesson_sessions` · **Dosya:** `mobile/lib/features/lesson_sessions/presentation/pages/lesson_note_form_page.dart`
> **State:** Stateful (form) · **Veri:** ⚠️ Demo/UI · **Güncelleme:** 2026-06-23

## Amaç
Yeni ders notu oluşturma: öğrenci/ders seçimi, başlık, not içeriği, dosya eki.

## Notlar
- Opsiyonel `LessonNoteFormContext` (studentName, lessonName, lockSelection) ile alanlar ön-doldurulabilir.
- ⚠️ Backend bağlantısı yok. İlgili: `POST /api/assignments/lesson-sessions/{id}/follow-up` (not+ödev birleşik).

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.8 · Modül: [`../modules/m06_assignments.md`](../modules/m06_assignments.md) (M06)
