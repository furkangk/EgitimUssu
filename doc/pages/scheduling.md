# Takvim / Scheduling (`/scheduling`)

> **Feature:** `scheduling` · **Dosya:** `mobile/lib/features/scheduling/presentation/pages/scheduling_page.dart`
> **State:** Stateful + `SchedulingCubit`, `StudentsCubit` · **Veri:** Karışık (initState'te demo seed) · **Güncelleme:** 2026-06-23

## Amaç
Ders takvimi; gün/hafta/ay görünümü. Dersler, müsait olmayan slotlar, ödevler ve ödeme vadeleri gösterilir. Yeni ders için FAB.

## Teknik
- Takvim paketi: `syncfusion_flutter_calendar`.
- Görünüm switcher (gün/hafta/ay), tarih navigasyonu + bugün butonu, ders detay sheet.

## Veri / API
- `SchedulingCubit` üzerinden; başlangıçta demo dersler seed ediliyor. Backend: `GET /api/scheduling/teachers/{teacherUserId}/lessons`, `POST /api/scheduling/lessons`.
- ⚠️ Eksik: ders çakışması kontrolü, ders güncelleme (bkz. [`../modules/m04_scheduling.md`](../modules/m04_scheduling.md) §3.2).

## İlgili
- Tasarım: [`../tutormatch_flutter_ui_design.md`](../tutormatch_flutter_ui_design.md) §10.6 · Modül: M04
