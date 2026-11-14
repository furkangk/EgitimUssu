import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_detail_page.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../helpers/fake_scheduling_repository.dart';

void main() {
  tearDown(injector.reset);

  testWidgets(
    'start sends original key of rescheduled occurrence to repository',
    (tester) async {
      tester.view.physicalSize = const Size(900, 1200);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final repository = _Sessions();
      injector.registerSingleton<LessonSessionRepository>(repository);
      injector.registerSingleton<SchedulingRepository>(
        FakeSchedulingRepository(),
      );
      final original = DateTime.utc(2026, 12, 14, 14);
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
      await tester.pumpWidget(
        MaterialApp(
          locale: const Locale('tr'),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: LessonDetailPage(
            payload: LessonDetailPayload(
              studentName: 'Öğrenci',
              subject: 'Matematik',
              dateLabel: '14 Aralık 2026',
              timeLabel: '19:00',
              modeLabel: 'Online',
              accent: Colors.blue,
              lessonId: 'series',
              lessonStatus: 'Planned',
              lesson: lesson,
            ),
          ),
        ),
      );
      await tester.tap(
        find.textContaining('Başlat').evaluate().isNotEmpty
            ? find.textContaining('Başlat')
            : find.text('Dersi Baslat'),
      );
      await tester.pumpAndSettle();
      expect(repository.key, original);
      expect(repository.started, 'occurrence-session');
      expect(tester.takeException(), isNull);
    },
  );
}

class _Sessions implements LessonSessionRepository {
  DateTime? key;
  String? started;
  static const session = LessonSession(
    id: 'occurrence-session',
    lessonScheduleId: 'series',
    teacherUserId: 'teacher',
    studentId: 'student',
    subject: 'Matematik',
    status: 'InProgress',
  );
  @override
  Future<LessonSession> createSessionFromSchedule(
    String id, {
    DateTime? occurrenceStartAtUtc,
  }) async {
    key = occurrenceStartAtUtc;
    return session;
  }

  @override
  Future<LessonSession> startSession(String id) async {
    started = id;
    return session;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
