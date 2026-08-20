---
title: "Dersler / Ders Oturumları Listesi Ekranı"
summary: "Ders listesi (Yaklaşan/Geçmiş/İptal); durum takibi ve hızlı tamamlama SchedulingCubit üzerinden gerçek API'ye bağlı, bazı alanlar demo"
tags: [sayfa, lesson-sessions, ogretmen]
status: "🟡"
authority: code
code_refs:
  - mobile/lib/features/lesson_sessions/presentation/pages/lesson_sessions_page.dart
  - mobile/lib/core/routing/app_router.dart
updated: 2026-06-28
---

# Dersler / Ders Oturumları (`/lesson-sessions`)

> **Feature:** `lesson_sessions` · **Dosya:** `mobile/lib/features/lesson_sessions/presentation/pages/lesson_sessions_page.dart`
> **State:** Stateful + `StudentsCubit`, `SchedulingCubit` · **Veri:** Karışık (demo + cubit) · **Güncelleme:** 2026-06-28

## Amaç
Ders listesi; sekmeler: Yaklaşan / Geçmiş / İptal Edilen. Ders kartında öğrenci, branş, saat, mod (Online/Yüz yüze).

## Durum takibi & hızlı tamamlama
- Her ders kartında bir **durum rozeti** vardır (öğretmen tek bakışta takip eder): **Tamamlandı** (yeşil) · **İptal edildi** (kırmızı) · **Bekliyor** (amber — başlama saati geçmiş ama henüz tamamlanmamış planlı ders) · **Planlandı** (mavi — gelecekteki planlı ders) · **Taslak** (gri). Mantık `_LessonStatusView.of(status, lesson)`.
- **Bekliyor** durumundaki kartlarda kartın altında yeşil **"Dersi Tamamla"** butonu çıkar; detay ekranına girmeden tek dokunuşla `SchedulingCubit.completeLesson` çağrılır (optimistic güncelleme + snackbar geri bildirimi). İstek sürerken buton spinner'a döner. İptal sekmesinde tamamlama butonu gösterilmez.
- Detay ekranındaki "Dersi Tamamla" akışı da korunur; ayrıca detay başlık kartına Tamamlandı/İptal rozeti eklendi.
- **Detaydan dönünce otomatik yenileme:** Karta tıklayıp açılan detayda tamamlama/kalıcı düzenleme yapılıp geri dönülürse liste `SchedulingCubit.loadForCalendar` ile otomatik tazelenir (detay `PopScope` ile `true` döndürür).

## Route
- `/lesson-sessions?create=1` → oluşturma diyaloğunu açar.

## Ders ekleme
- "Ders Ekle" FAB, takvimdeki "Ders Planla" ile **ortak** `LessonFormSheet` (`scheduling/presentation/widgets/lesson_form_sheet.dart`) açar. Önceden iki ekran ayrı form kullanıyordu; tek forma indirgendi. Yüz yüze format değeri kanonik `InPerson` (eskiden burada `FaceToFace` yazılıyordu — düzeltildi).
- **Online format** seçilince toplantı linki input'u çıkar; link `LessonSchedule.meetingUrl` alanına yazılır (`locationLabel` her zaman 'Online'/'Yüz yüze' mod etiketidir).
- **Tekrar:** "Tek Ders / Tekrarlı Ders" sekmesi tek karar noktasıdır (ayrı "tekrar etsin" switch'i yok). Tekrarlı modda sıklık: **Günlük / Haftalık / Aylık**. "Hangi günler" seçimi **yalnızca Haftalık**'ta görünür; Günlük her gün, Aylık başlangıç tarihinin ayın o gününde tekrarlanır (RRULE `FREQ=DAILY|WEEKLY;BYDAY=...|MONTHLY` + `UNTIL`).

## Veri / API
- `StudentsCubit` + `SchedulingCubit` üzerinden; bir kısmı demo. Backend: `GET /api/scheduling/teachers/{teacherUserId}/lessons`, `POST /api/scheduling/lessons/{lessonId}/complete`, `POST /api/scheduling/lessons/{lessonId}/cancel`.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.16, tab: [`../tab_widget.md`](../tab_widget.md) · Modül: [`../modules/m05_lesson_sessions.md`](../modules/m05_lesson_sessions.md) (M05)
