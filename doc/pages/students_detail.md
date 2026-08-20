---
title: "Öğrenci Detayı Ekranı"
summary: "Öğrenci profili + sekmeli görünüm (Genel/Dersler/Performans/Ödemeler); demo veriye düşebilen kısmi gerçek bağlantı"
tags: [sayfa, students, ogretmen]
status: "🔴"
authority: code
code_refs:
  - mobile/lib/features/students/presentation/pages/student_detail_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-28
---

# Öğrenci Detayı (`/students/:studentId`)

> **Feature:** `students` · **Dosya:** `mobile/lib/features/students/presentation/pages/student_detail_page.dart`
> **State:** Stateful (tab seçimi) + `StudentDetailCubit`/`StudentDetailState` · **Veri:** ⚠️ Demo'ya düşebilen gerçek bağlantı · **Güncelleme:** 2026-06-28

## Amaç
Bir öğrencinin profili + sekmeli görünüm: Genel / Dersler / Performans / Ödemeler.

## Veri / API
- ⚠️ Demo veri. Gerçek bağlantı: `GET /api/students/profiles/{studentId}` + ders/ödeme endpoint'leri.

## Ana bileşenler
- Avatarlı profil başlığı, tab switcher, sekme içerikleri (ders listesi, performans metrikleri, ödeme listesi).

## Aksiyonlar
- **Yeni Ders** (Genel sekmesi) ve **Yeni ders** (Dersler sekmesi başlığı): ortak `LessonFormSheet`'i **modal** açar — bu öğrenci tek elemanlı `students` listesi olarak verildiği için form öğrenciyi otomatik seçer (`_openLessonForm`). Sheet kendi `SchedulingCubit`'ini sağlar; başarılı kayıtta kapanır ve öğrenci detayı (`StudentDetailCubit.load`) tazelenir. (Önceden bu butonlar yalnızca `/scheduling`'e gidiyordu.)
- **Ödeme Ekle:** `/payments` ekranına gider.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.5 · Modül: [`../modules/m03_students.md`](../modules/m03_students.md) (M03)
