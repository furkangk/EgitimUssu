import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/cubit/lesson_sessions_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../../helpers.dart';

void main() {
  test('loads planned lessons and starts a lesson session', () async {
    final cache = InMemoryLocalCache();
    final lessonSessionRepository = _FakeLessonSessionRepository();
    final schedulingRepository = _FakeSchedulingRepository();
    final cubit = LessonSessionsCubit(
      lessonSessionRepository: lessonSessionRepository,
      schedulingRepository: schedulingRepository,
      localCache: cache,
    );

    await cubit.load('teacher-1');
    await cubit.startFromLesson(cubit.state.lessons.first);

    expect(cubit.state.lessons, hasLength(1));
    expect(cubit.state.sessions, hasLength(1));
    expect(cubit.state.sessions.first.lessonScheduleId, 'lesson-1');
    expect(cubit.state.successMessage, 'Ders oturumu acildi.');
    expect(lessonSessionRepository.created?.subject, 'Matematik');

    await cubit.close();
  });

  test('completes lesson session and stores completed state', () async {
    final cache = InMemoryLocalCache();
    final cubit = LessonSessionsCubit(
      lessonSessionRepository: _FakeLessonSessionRepository(),
      schedulingRepository: _FakeSchedulingRepository(),
      localCache: cache,
    );
    await cubit.load('teacher-1');
    await cubit.startFromLesson(cubit.state.lessons.first);
    final session = cubit.state.sessions.single;

    await cubit.completeSession(
      session: session,
      actualStartAtUtc: DateTime.utc(2026, 5, 7, 12),
      actualEndAtUtc: DateTime.utc(2026, 5, 7, 13),
      attendanceStatus: 'Present',
      topicTitle: 'Denklemler',
      coveredContent: 'Birinci derece denklemler',
      teacherNotes: 'Odev kontrol edilecek',
    );

    expect(cubit.state.sessions.single.status, 'Completed');
    expect(cubit.state.sessions.single.topicTitle, 'Denklemler');
    expect(cubit.state.successMessage, 'Ders oturumu tamamlandi.');

    await cubit.close();
  });
}

class _FakeLessonSessionRepository implements LessonSessionRepository {
  LessonSession? created;

  @override
  Future<LessonSession> completeSession(LessonSession lessonSession) async {
    return LessonSession(
      id: lessonSession.id,
      lessonScheduleId: lessonSession.lessonScheduleId,
      teacherUserId: lessonSession.teacherUserId,
      studentId: lessonSession.studentId,
      subject: lessonSession.subject,
      status: 'Completed',
      plannedStartAtUtc: lessonSession.plannedStartAtUtc,
      topicTitle: lessonSession.topicTitle,
      coveredContent: lessonSession.coveredContent,
      teacherNotes: lessonSession.teacherNotes,
      actualStartAtUtc: lessonSession.actualStartAtUtc,
      actualEndAtUtc: lessonSession.actualEndAtUtc,
      attendanceStatus: lessonSession.attendanceStatus,
    );
  }

  @override
  Future<LessonSession> createSession(LessonSession lessonSession) async {
    created = lessonSession;
    return LessonSession(
      id: 'session-1',
      lessonScheduleId: lessonSession.lessonScheduleId,
      teacherUserId: lessonSession.teacherUserId,
      studentId: lessonSession.studentId,
      subject: lessonSession.subject,
      status: lessonSession.status,
      plannedStartAtUtc: lessonSession.plannedStartAtUtc,
      topicTitle: lessonSession.topicTitle,
    );
  }

  @override
  Future<LessonSession> getSession(String lessonSessionId) {
    throw UnimplementedError();
  }
}

class _FakeSchedulingRepository implements SchedulingRepository {
  @override
  Future<LessonSchedule> cancelLesson({
    required String lessonId,
    String? cancellationNote,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> completeLesson({required String lessonId}) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> createLesson(LessonSchedule lessonSchedule) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> updateLesson(LessonSchedule lessonSchedule) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> getLesson(String lessonId) {
    throw UnimplementedError();
  }

  @override
  Future<List<LessonSchedule>> listTeacherLessons({
    required String teacherUserId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
  }) async {
    return <LessonSchedule>[
      LessonSchedule(
        id: 'lesson-1',
        teacherUserId: teacherUserId,
        studentId: 'student-1',
        subject: 'Matematik',
        lessonFormat: 'Online',
        startAtUtc: DateTime.utc(2026, 5, 7, 12),
        endAtUtc: DateTime.utc(2026, 5, 7, 13),
        timeZone: 'Europe/Istanbul',
      ),
    ];
  }
}
