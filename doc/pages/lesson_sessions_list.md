# Dersler / Ders Oturumları (`/lesson-sessions`)

> **Feature:** `lesson_sessions` · **Dosya:** `mobile/lib/features/lesson_sessions/presentation/pages/lesson_sessions_page.dart`
> **State:** Stateful + `StudentsCubit`, `SchedulingCubit` · **Veri:** Karışık (demo + cubit) · **Güncelleme:** 2026-06-28

## Amaç
Ders listesi; sekmeler: Yaklaşan / Geçmiş / İptal Edilen. Ders kartında öğrenci, branş, saat, mod (Online/Yüz yüze).

## Route
- `/lesson-sessions?create=1` → oluşturma diyaloğunu açar.

## Ders ekleme
- "Ders Ekle" FAB, takvimdeki "Ders Planla" ile **ortak** `LessonFormSheet` (`scheduling/presentation/widgets/lesson_form_sheet.dart`) açar. Önceden iki ekran ayrı form kullanıyordu; tek forma indirgendi. Yüz yüze format değeri kanonik `InPerson` (eskiden burada `FaceToFace` yazılıyordu — düzeltildi).
- **Online format** seçilince toplantı linki input'u çıkar; link `LessonSchedule.meetingUrl` alanına yazılır (`locationLabel` her zaman 'Online'/'Yüz yüze' mod etiketidir).
- **Tekrar:** "Tek Ders / Tekrarlı Ders" sekmesi tek karar noktasıdır (ayrı "tekrar etsin" switch'i yok). Tekrarlı modda sıklık: **Günlük / Haftalık / Aylık**. "Hangi günler" seçimi **yalnızca Haftalık**'ta görünür; Günlük her gün, Aylık başlangıç tarihinin ayın o gününde tekrarlanır (RRULE `FREQ=DAILY|WEEKLY;BYDAY=...|MONTHLY` + `UNTIL`).

## Veri / API
- `StudentsCubit` + `SchedulingCubit` üzerinden; bir kısmı demo. Backend: `GET /api/scheduling/teachers/{teacherUserId}/lessons`, `POST /api/lesson-sessions/{id}/complete`.

## İlgili
- Tasarım: [`../architecture/mobile_flutter.md`](../architecture/mobile_flutter.md) §13.16, tab: [`../tab_widget.md`](../tab_widget.md) · Modül: [`../modules/m05_lesson_sessions.md`](../modules/m05_lesson_sessions.md) (M05)
