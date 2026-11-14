import 'package:egitim_ussu_mobile/features/scheduling/data/models/lesson_schedule_model.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:flutter_test/flutter_test.dart';

Map<String, dynamic> _json({
  String? recurrenceRule,
  String? occurrenceStartAtUtc,
  String status = 'Planned',
}) => <String, dynamic>{
  'id': 'lesson-id',
  'teacherUserId': 'teacher-id',
  'studentId': 'student-id',
  'subject': 'Matematik',
  'lessonFormat': 'Online',
  // Ertelenmiş oluşum: görünen saat Salı 15:00Z, orijinal (RECURRENCE-ID) Pazartesi 15:00Z.
  'startAtUtc': '2026-05-12T15:00:00Z',
  'endAtUtc': '2026-05-12T16:00:00Z',
  'timeZone': 'Europe/Istanbul',
  'status': status,
  'recurrenceRule': recurrenceRule,
  'reminderOffsetMinutes': 60,
  'occurrenceStartAtUtc': occurrenceStartAtUtc,
};

void main() {
  test('maps cancel payload and response status', () {
    final model = LessonScheduleModel.fromJson(_json(status: 'Cancelled'));

    expect(model.status, 'Cancelled');
    expect(
      LessonScheduleModel.cancelPayload(
        lesson: model,
        scope: OccurrenceScope.single,
        cancellationNote: 'Hasta',
      )['cancellationNote'],
      'Hasta',
    );
  });

  // P17 T1 / N-01: sunucu tekrarlı derste kapsamsız isteği "tüm seri" sayıyordu.
  test('recurring cancel sends the scope and the ORIGINAL occurrence key', () {
    final model = LessonScheduleModel.fromJson(
      _json(
        recurrenceRule: 'FREQ=WEEKLY;BYDAY=MO',
        occurrenceStartAtUtc: '2026-05-11T15:00:00Z',
      ),
    );

    final payload = LessonScheduleModel.cancelPayload(
      lesson: model,
      scope: OccurrenceScope.single,
    );

    expect(model.occurrenceStartAtUtc, DateTime.utc(2026, 5, 11, 15));
    expect(payload['scope'], 1);
    // Görünen (ertelenmiş) saat değil, orijinal oluşum (master §4).
    expect(payload['occurrenceStartAtUtc'], '2026-05-11T15:00:00.000Z');
  });

  test('whole-series cancel needs no occurrence key', () {
    final model = LessonScheduleModel.fromJson(
      _json(
        recurrenceRule: 'FREQ=WEEKLY;BYDAY=MO',
        occurrenceStartAtUtc: '2026-05-11T15:00:00Z',
      ),
    );

    final payload = LessonScheduleModel.cancelPayload(
      lesson: model,
      scope: OccurrenceScope.all,
    );

    expect(payload['scope'], 3);
    expect(payload.containsKey('occurrenceStartAtUtc'), isFalse);
  });

  test('single lesson sends no scope (server ignores it)', () {
    final model = LessonScheduleModel.fromJson(_json());

    final payload = LessonScheduleModel.cancelPayload(
      lesson: model,
      scope: OccurrenceScope.single,
    );

    expect(payload.containsKey('scope'), isFalse);
    expect(payload.containsKey('occurrenceStartAtUtc'), isFalse);
  });

  test('missing occurrence key falls back to the lesson start', () {
    final model = LessonScheduleModel.fromJson(
      _json(recurrenceRule: 'FREQ=WEEKLY;BYDAY=TU'),
    );

    expect(model.occurrenceStartAtUtc, isNull);
    expect(
      LessonScheduleModel.cancelPayload(
        lesson: model,
        scope: OccurrenceScope.thisAndFuture,
      )['occurrenceStartAtUtc'],
      '2026-05-12T15:00:00.000Z',
    );
  });
}
