import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/scheduled_session_completion.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_detail_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_sessions_page.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../helpers/fake_auth_repository.dart';
import '../../../helpers/fake_notification_repository.dart';
import '../../../helpers/fake_scheduling_repository.dart';

void main() {
  tearDown(injector.reset);
  final original = DateTime.utc(2026, 11, 2, 14);
  final lesson = LessonSchedule(
    id: 'series',
    teacherUserId: 'teacher',
    studentId: 'student',
    subject: 'Matematik',
    lessonFormat: 'Online',
    startAtUtc: original.add(const Duration(hours: 2)),
    endAtUtc: original.add(const Duration(hours: 3)),
    timeZone: 'Europe/Istanbul',
    recurrenceRule: 'FREQ=WEEKLY',
    occurrenceStartAtUtc: original,
  );

  test(
    'quick completion derives original key and preserves session notes and attendance',
    () async {
      final repository = _Sessions();
      await completeScheduledSession(repository, lesson);
      expect(repository.calls, ['derive', 'complete']);
      expect(repository.key, original);
      expect(repository.completed?.id, 'session');
      expect(repository.completed?.actualStartAtUtc, lesson.startAtUtc);
      expect(repository.completed?.actualEndAtUtc, lesson.endAtUtc);
      expect(repository.completed?.teacherNotes, 'Notlar');
      expect(repository.completed?.attendanceStatus, 'Absent');
    },
  );

  testWidgets(
    'detail completes occurrence through session and removes completion action',
    (tester) async {
      _largeScreen(tester);
      final repository = _Sessions();
      injector.registerSingleton<LessonSessionRepository>(repository);
      injector.registerSingleton<SchedulingRepository>(
        FakeSchedulingRepository(),
      );
      await tester.pumpWidget(
        _app(
          LessonDetailPage(
            payload: LessonDetailPayload(
              studentName: 'Öğrenci',
              subject: 'Matematik',
              dateLabel: '2 Kasım',
              timeLabel: '19:00',
              modeLabel: 'Online',
              accent: Colors.blue,
              lessonId: lesson.id,
              lessonStatus: 'Planned',
              lesson: lesson,
            ),
          ),
        ),
      );
      await tester.tap(find.text('Dersi Tamamla'));
      await tester.pumpAndSettle();
      expect(repository.calls, ['derive', 'complete']);
      expect(repository.key, original);
      expect(find.text('Dersi Tamamla'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'past card completes only selected week and preserves sibling action',
    (tester) async {
      _largeScreen(tester);
      final repository = _Sessions();
      injector.registerSingleton<LessonSessionRepository>(repository);
      injector.registerSingleton<StudentRepository>(_Students());
      final next = LessonSchedule(
        id: lesson.id,
        teacherUserId: 'teacher',
        studentId: 'student',
        subject: 'Matematik',
        lessonFormat: 'Online',
        startAtUtc: lesson.startAtUtc.add(const Duration(days: 7)),
        endAtUtc: lesson.endAtUtc.add(const Duration(days: 7)),
        timeZone: lesson.timeZone,
        recurrenceRule: lesson.recurrenceRule,
        occurrenceStartAtUtc: original.add(const Duration(days: 7)),
      );
      injector.registerSingleton<SchedulingRepository>(
        FakeSchedulingRepository(teacherLessons: [next, lesson]),
      );
      final auth = AuthCubit(FakeAuthRepository());
      addTearDown(auth.close);
      final notifications = NotificationsCubit(FakeNotificationRepository());
      addTearDown(notifications.close);
      await tester.pumpWidget(
        MultiBlocProvider(
          providers: [
            BlocProvider.value(value: auth),
            BlocProvider.value(value: notifications),
          ],
          child: _app(const LessonSessionsPage()),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Geçmiş'));
      await tester.pumpAndSettle();
      expect(find.text('Dersi Tamamla'), findsNWidgets(2));
      await tester.tap(find.text('Dersi Tamamla').last);
      await tester.pumpAndSettle();
      expect(repository.calls, ['derive', 'complete']);
      expect(repository.key, original);
      expect(find.text('Dersi Tamamla'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
}

Widget _app(Widget child) => MaterialApp(
  locale: const Locale('tr'),
  localizationsDelegates: AppLocalizations.localizationsDelegates,
  supportedLocales: AppLocalizations.supportedLocales,
  home: child,
);
void _largeScreen(WidgetTester tester) {
  tester.view.physicalSize = const Size(900, 1400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

class _Students implements StudentRepository {
  @override
  Future<List<StudentProfile>> listByTeacher(String id) async => [];
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _Sessions implements LessonSessionRepository {
  final calls = <String>[];
  DateTime? key;
  LessonSession? completed;
  @override
  Future<LessonSession> createSessionFromSchedule(
    String id, {
    DateTime? occurrenceStartAtUtc,
  }) async {
    calls.add('derive');
    key = occurrenceStartAtUtc;
    return LessonSession(
      id: 'session',
      lessonScheduleId: id,
      teacherUserId: 'teacher',
      studentId: 'student',
      subject: 'Matematik',
      status: 'Planned',
      topicTitle: 'Kesirler',
      teacherNotes: 'Notlar',
      attendanceStatus: 'Absent',
    );
  }

  @override
  Future<LessonSession> completeSession(LessonSession session) async {
    calls.add('complete');
    completed = session;
    return LessonSession(
      id: session.id,
      lessonScheduleId: session.lessonScheduleId,
      teacherUserId: session.teacherUserId,
      studentId: session.studentId,
      subject: session.subject,
      status: 'Completed',
    );
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
