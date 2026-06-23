# Öğrenci Listesi (`/students`)

> **Feature:** `students` · **Dosya:** `mobile/lib/features/students/presentation/pages/students_page.dart`
> **State:** Stateful (yerel arama) · **Veri:** ⚠️ Demo (`StudentDemoData`) · **Güncelleme:** 2026-06-23

## Amaç
Öğretmenin öğrencilerini arama/filtreyle listelemesi; ad, sınıf, kayıt sayısı.

## Veri / API
- ⚠️ Şu an **demo veri**. Backend hazır: `GET /api/students/profiles/by-teacher/{teacherUserId}` ile bağlanmalı.

## Ana bileşenler
- Arama alanı, öğrenci kartları, kayıt sayısı rozeti → karta dokununca `/students/{studentId}`.

## İlgili
- Tasarım: [`../tutormatch_flutter_ui_design.md`](../tutormatch_flutter_ui_design.md) §10.4 · Modül: [`../modules/m03_students.md`](../modules/m03_students.md) (M03)
