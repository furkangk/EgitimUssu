---
title: "Takvim / Scheduling Ekranı"
summary: "Ders takvimi (gün/hafta/ay); SchedulingCubit gerçek API'ye bağlı, initState'te demo seed ile karışık"
tags: [sayfa, scheduling, ogretmen]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/scheduling/presentation/pages/scheduling_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-09-04
---

# Takvim / Scheduling (`/scheduling`)

> **Feature:** `scheduling` · **Dosya:** `mobile/lib/features/scheduling/presentation/pages/scheduling_page.dart`
> **State:** Stateful + `SchedulingCubit`, `StudentsCubit` · **Veri:** Karışık (initState'te demo seed) · **Güncelleme:** 2026-09-04

## Amaç
Ders takvimi; gün/hafta/ay görünümü. Dersler, müsait olmayan slotlar, ödevler ve ödeme vadeleri gösterilir. Yeni ders için FAB.

## Teknik
- Takvim paketi: `syncfusion_flutter_calendar`.
- Görünüm switcher (gün/hafta/ay), tarih navigasyonu + bugün butonu, ders detay sheet.
- **"Ders Planla" FAB**, dersler ekranındaki "Ders Ekle" ile **ortak** `LessonFormSheet` (`scheduling/presentation/widgets/lesson_form_sheet.dart`) açar — tek/tekrarlı ders, öğrenci-bazlı ders seçimi, başlangıç/bitiş saati, format ve haftalık program önizlemesi. Form kapanınca `setState` ile takvim yenilenir.

## Veri / API
- `SchedulingCubit` üzerinden; başlangıçta demo dersler seed ediliyor. Backend: `GET /api/scheduling/teachers/{teacherUserId}/lessons`, `POST /api/scheduling/lessons`.
- **Tekrar açılımı (M04-1, 2026-09-04):** Liste ucu artık seriyi değil **somut oluşumları** döner — haftalık bir ders takvimde her haftasında görünür. Açılım backend'de (`RecurrenceExpander`) yapılır; ekranda istemci tarafı genişletme yoktur. Aynı seriye ait satırlar aynı `id`'yi taşır, saatleri farklıdır; `occurrenceStartAtUtc` alanı tek oluşum iptal/erteleme uçlarının hedef anahtarıdır (mobil henüz bu uçları kullanmıyor).
- Çakışma: ortak form haftalık önizlemede "Çakışma var/Uygun" rozetiyle **yumuşak uyarı** gösterir; sert engelleme backend'de (`HasTeacherConflictAsync` → 409, bkz. [`../modules/m04_scheduling.md`](../modules/m04_scheduling.md) §161).
- ⚠️ Eksik: istemci tarafı ders güncelleme (yalnızca oluştur + iptal var).

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.6 · Modül: M04
