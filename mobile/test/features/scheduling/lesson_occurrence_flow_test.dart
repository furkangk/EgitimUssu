import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/scheduled_session_completion.dart';
import 'package:egitim_ussu_mobile/features/scheduling/data/models/lesson_schedule_model.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_scheduling_repository.dart';

void main() {
  test(
    'three weeks retain original keys through completion, single cancellation and moved completion',
    () async {
      final first = DateTime.utc(2026, 11, 1, 12);
      LessonSchedule week(int days, {int movedHours = 0}) =>
          LessonScheduleModel.fromJson({
            'id': 'series',
            'teacherUserId': 'teacher',
            'studentId': 'student',
            'subject': 'Matematik',
            'lessonFormat': 'Online',
            'status': 'Planned',
            'startAtUtc': first
                .add(Duration(days: days, hours: movedHours))
                .toIso8601String(),
            'endAtUtc': first
                .add(Duration(days: days, hours: movedHours + 1))
                .toIso8601String(),
            'occurrenceStartAtUtc': first
                .add(Duration(days: days))
                .toIso8601String(),
            'timeZone': 'Europe/Istanbul',
            'recurrenceRule': 'FREQ=WEEKLY;COUNT=3',
          });
      final weeks = [week(0), week(7), week(14, movedHours: 3)];
      final scheduling = _Scheduling(weeks);
      final sessions = _Sessions();
      final cubit = SchedulingCubit(scheduling);
      addTearDown(cubit.close);
      await cubit.load('teacher');
      await completeScheduledSession(sessions, weeks[0]);
      await cubit.cancelLesson(
        lesson: weeks[1],
        scope: OccurrenceScope.single,
        reason: LessonCancellationReason.studentCancelled,
      );
      expect(cubit.state.lessons.map((l) => l.status), [
        'Planned',
        'Cancelled',
        'Planned',
      ]);
      expect(
        cubit.state.lessons.map((l) => l.startAtUtc),
        weeks.map((l) => l.startAtUtc),
      );
      final cancelledPayload = LessonScheduleModel.cancelPayload(
        lesson: weeks[1],
        scope: OccurrenceScope.single,
        reason: LessonCancellationReason.studentCancelled,
        isChargeable: true,
      );
      expect(
        cancelledPayload['occurrenceStartAtUtc'],
        first.add(const Duration(days: 7)).toIso8601String(),
      );
      expect(cancelledPayload['scope'], 1);
      await completeScheduledSession(sessions, weeks[2]);
      expect(sessions.completed.map((s) => s.id).toSet().length, 2);
      expect(sessions.completed.map((s) => s.actualStartAtUtc), [
        weeks[0].startAtUtc,
        weeks[2].startAtUtc,
      ]);
      expect(sessions.keys, [first, first.add(const Duration(days: 14))]);
      expect(scheduling.key, first.add(const Duration(days: 7)));
      expect(
        sessions.completed.every((s) => s.attendanceStatus == 'Attended'),
        isTrue,
      );
    },
  );
}

class _Scheduling extends FakeSchedulingRepository {
  _Scheduling(List<LessonSchedule> weeks) : super(teacherLessons: weeks);
  DateTime? key;
  @override
  Future<LessonSchedule> cancelLesson({
    required LessonSchedule lesson,
    required OccurrenceScope scope,
    LessonCancellationReason reason = LessonCancellationReason.teacherCancelled,
    bool isChargeable = true,
    String? cancellationNote,
  }) async {
    key = lesson.occurrenceKeyUtc;
    return teacherLessons
        .first; // Sunucunun çapa yanıtı komşu haftaları ezmemeli.
  }
}

class _Sessions implements LessonSessionRepository {
  final keys = <DateTime?>[];
  final completed = <LessonSession>[];
  @override
  Future<LessonSession> createSessionFromSchedule(
    String id, {
    DateTime? occurrenceStartAtUtc,
  }) async {
    keys.add(occurrenceStartAtUtc);
    return LessonSession(
      id: 'session-${occurrenceStartAtUtc!.toIso8601String()}',
      lessonScheduleId: id,
      teacherUserId: 'teacher',
      studentId: 'student',
      subject: 'Matematik',
      status: 'Planned',
      attendanceStatus: 'Unknown',
    );
  }

  @override
  Future<LessonSession> completeSession(LessonSession session) async {
    completed.add(session);
    return session;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
