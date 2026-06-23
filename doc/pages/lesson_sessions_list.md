# Dersler / Ders Oturumları (`/lesson-sessions`)

> **Feature:** `lesson_sessions` · **Dosya:** `mobile/lib/features/lesson_sessions/presentation/pages/lesson_sessions_page.dart`
> **State:** Stateful + `StudentsCubit`, `SchedulingCubit` · **Veri:** Karışık (demo + cubit) · **Güncelleme:** 2026-06-23

## Amaç
Ders listesi; sekmeler: Yaklaşan / Geçmiş / İptal Edilen. Ders kartında öğrenci, branş, saat, mod (Online/Yüz yüze).

## Route
- `/lesson-sessions?create=1` → oluşturma diyaloğunu açar.

## Veri / API
- `StudentsCubit` + `SchedulingCubit` üzerinden; bir kısmı demo. Backend: `GET /api/scheduling/teachers/{teacherUserId}/lessons`, `POST /api/lesson-sessions/{id}/complete`.

## İlgili
- Tasarım: [`../tutormatch_flutter_ui_design.md`](../tutormatch_flutter_ui_design.md) §10.16, tab: [`../tab_widget.md`](../tab_widget.md) · Modül: [`../modules/m05_lesson_sessions.md`](../modules/m05_lesson_sessions.md) (M05)
