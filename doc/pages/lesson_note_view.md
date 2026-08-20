---
title: "Ders Notu Görüntüleme Ekranı"
summary: "Bir ders notunu görüntüleme ekranı (başlık/meta/metin/dosya önizleme); backend bağlantısı yok, tamamen demo/UI"
tags: [sayfa, lesson-sessions, note, demo]
status: "🔴"
authority: code
code_refs:
  - mobile/lib/features/lesson_sessions/presentation/pages/lesson_note_view_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-23
---

# Ders Notu Görüntüleme (`/lesson-sessions/detail/note`)

> **Feature:** `lesson_sessions` · **Dosya:** `mobile/lib/features/lesson_sessions/presentation/pages/lesson_note_view_page.dart`
> **State:** Yok (Stateless) · **Veri:** ⚠️ Demo/UI · **Güncelleme:** 2026-06-23

## Amaç
Bir ders notunu görüntüleme: başlık, meta (boyut/tarih), tam not metni, dosya önizleme/indirme.

## Notlar
- Veri `route.extra` ile `LessonNoteViewPayload`: `title`, `meta`, `noteText`, `accent`, `sourceFilePath`.
- ⚠️ Backend bağlantısı yok.

## İlgili
- Modül: [`../modules/m06_assignments.md`](../modules/m06_assignments.md) (M06)
