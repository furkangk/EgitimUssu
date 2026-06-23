# Öğretmen Paneli / Dashboard (`/dashboard`)

> **Feature:** `dashboard` · **Dosya:** `mobile/lib/features/dashboard/presentation/pages/dashboard_page.dart`
> **State:** `DashboardCubit` / `DashboardState` · **Veri:** Cubit (`load(teacherUserId)`) · **Güncelleme:** 2026-06-23

## Amaç
Öğretmenin günlük operasyon ana ekranı: streak, bugünün ders sayısı, yaklaşan dersler, son aktiviteler, hızlı işlemler.

## State / API
- `DashboardCubit.create()..load(teacherUserId)`.
- ⚠️ Backend'de henüz tek "dashboard özeti" endpoint'i yok (bkz. [`../roles/ogretmen.md`](../roles/ogretmen.md) §8 — eksik dashboard özeti).

## Ana bileşenler
- Bildirim badge'li header, özet kartları (streak / bugünkü dersler), hızlı işlem kutucukları (Ders Planla / Ödev Ver / Not Ekle / Ödeme Ekle), yaklaşan dersler yatay liste, aktivite listesi.
- Bottom nav: Ana sayfa · Dersler · Öğrenciler · Takvim · Finans · Diğer.

## İlgili
- Tasarım: [`../tutormatch_flutter_ui_design.md`](../tutormatch_flutter_ui_design.md) §10.3
- Önizleme varyantı: [`dashboard_preview.md`](dashboard_preview.md)
