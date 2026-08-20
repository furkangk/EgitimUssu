---
title: "Takvim / Scheduling Ekranı"
summary: "Ders takvimi (gün/hafta/ay); SchedulingCubit gerçek API'ye bağlı, initState'te demo seed ile karışık"
tags: [sayfa, scheduling, ogretmen]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/scheduling/presentation/pages/scheduling_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-28
---

# Takvim / Scheduling (`/scheduling`)

> **Feature:** `scheduling` · **Dosya:** `mobile/lib/features/scheduling/presentation/pages/scheduling_page.dart`
> **State:** Stateful + `SchedulingCubit`, `StudentsCubit` · **Veri:** Karışık (initState'te demo seed) · **Güncelleme:** 2026-06-28

## Amaç
Ders takvimi; gün/hafta/ay görünümü. Dersler, müsait olmayan slotlar, ödevler ve ödeme vadeleri gösterilir. Yeni ders için FAB.

## Teknik
- Takvim paketi: `syncfusion_flutter_calendar`.
- Görünüm switcher (gün/hafta/ay), tarih navigasyonu + bugün butonu, ders detay sheet.
- **"Ders Planla" FAB**, dersler ekranındaki "Ders Ekle" ile **ortak** `LessonFormSheet` (`scheduling/presentation/widgets/lesson_form_sheet.dart`) açar — tek/tekrarlı ders, öğrenci-bazlı ders seçimi, başlangıç/bitiş saati, format ve haftalık program önizlemesi. Form kapanınca `setState` ile takvim yenilenir.

## Veri / API
- `SchedulingCubit` üzerinden; başlangıçta demo dersler seed ediliyor. Backend: `GET /api/scheduling/teachers/{teacherUserId}/lessons`, `POST /api/scheduling/lessons`.
- Çakışma: ortak form haftalık önizlemede "Çakışma var/Uygun" rozetiyle **yumuşak uyarı** gösterir; sert engelleme backend'de (`HasTeacherConflictAsync` → 409, bkz. [`../modules/m04_scheduling.md`](../modules/m04_scheduling.md) §161).
- ⚠️ Eksik: istemci tarafı ders güncelleme (yalnızca oluştur + iptal var).

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.6 · Modül: M04
