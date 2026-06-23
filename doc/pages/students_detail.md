# Öğrenci Detayı (`/students/:studentId`)

> **Feature:** `students` · **Dosya:** `mobile/lib/features/students/presentation/pages/student_detail_page.dart`
> **State:** Stateful (tab seçimi) · **Veri:** ⚠️ Demo (`StudentDemoData`) · **Güncelleme:** 2026-06-23

## Amaç
Bir öğrencinin profili + sekmeli görünüm: Genel / Dersler / Performans / Ödemeler.

## Veri / API
- ⚠️ Demo veri. Gerçek bağlantı: `GET /api/students/profiles/{studentId}` + ders/ödeme endpoint'leri.

## Ana bileşenler
- Avatarlı profil başlığı, tab switcher, sekme içerikleri (ders listesi, performans metrikleri, ödeme listesi).

## İlgili
- Tasarım: [`../tutormatch_flutter_ui_design.md`](../tutormatch_flutter_ui_design.md) §10.5 · Modül: [`../modules/m03_students.md`](../modules/m03_students.md) (M03)
