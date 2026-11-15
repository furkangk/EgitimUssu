import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_detail_page.dart';
import 'package:egitim_ussu_mobile/features/scheduling/data/models/lesson_schedule_model.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_cubit.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_scheduling_repository.dart';

LessonSchedule lesson(int day, {String status = 'Planned'}) => LessonSchedule(
  id: 'series',
  teacherUserId: 'teacher',
  studentId: 'student',
  subject: 'Matematik',
  lessonFormat: 'Online',
  startAtUtc: DateTime.utc(2026, 11, day, 15),
  endAtUtc: DateTime.utc(2026, 11, day, 16),
  timeZone: 'Europe/Istanbul',
  recurrenceRule: 'FREQ=WEEKLY',
  status: status,
);

void main() {
  tearDown(injector.reset);
  testWidgets(
    'detail cancels schedule with scope and waiver without deriving a session',
    (tester) async {
      tester.view.physicalSize = const Size(900, 1400);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final repo = _Repo();
      injector.registerSingleton<SchedulingRepository>(repo);
      final source = repo.teacherLessons[1];
      await tester.pumpWidget(
        MaterialApp(
          locale: const Locale('tr'),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: LessonDetailPage(
            payload: LessonDetailPayload(
              studentName: 'Öğrenci',
              subject: 'Matematik',
              dateLabel: '8 Kasım',
              timeLabel: '18:00',
              modeLabel: 'Online',
              accent: Colors.blue,
              lessonId: source.id,
              lessonStatus: 'Planned',
              lesson: source,
            ),
          ),
        ),
      );
      await tester.tap(find.text('Dersi İptal Et'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Devam et'));
      await tester.pumpAndSettle();
      await tester.tap(
        find.byType(DropdownButtonFormField<LessonCancellationReason>),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('Öğrenci iptal etti').last);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Ücretsiz iptal et'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Dersi iptal et'));
      await tester.pumpAndSettle();
      expect(repo.reason, LessonCancellationReason.studentCancelled);
      expect(repo.isChargeable, false);
      expect(repo.key, source.occurrenceKeyUtc);
      expect(repo.scope, OccurrenceScope.single);
      expect(find.text('Dersi İptal Et'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
  test(
    'cancellation payload carries reason and a free cancellation override',
    () {
      final payload = LessonScheduleModel.cancelPayload(
        lesson: lesson(8),
        scope: OccurrenceScope.single,
        reason: LessonCancellationReason.studentCancelled,
        isChargeable: false,
      );
      expect(payload['reason'], 2);
      expect(payload['isChargeable'], false);
    },
  );
  for (final scope in OccurrenceScope.values) {
    test(
      'cancellation preserves all original occurrence times for $scope',
      () async {
        final repo = _Repo();
        final cubit = SchedulingCubit(repo);
        await cubit.load('teacher');
        await cubit.cancelLesson(
          lesson: repo.teacherLessons[1],
          scope: scope,
          reason: LessonCancellationReason.studentCancelled,
          isChargeable: false,
        );
        expect(
          cubit.state.lessons.map((e) => e.startAtUtc),
          repo.teacherLessons.map((e) => e.startAtUtc),
        );
        expect(
          cubit.state.lessons[0].status,
          scope == OccurrenceScope.all ? 'Cancelled' : 'Planned',
        );
        expect(cubit.state.lessons[1].status, 'Cancelled');
        expect(
          cubit.state.lessons[2].status,
          scope == OccurrenceScope.single ? 'Planned' : 'Cancelled',
        );
        expect(repo.reason, LessonCancellationReason.studentCancelled);
        expect(repo.isChargeable, false);
        await cubit.close();
      },
    );
  }
}

class _Repo extends FakeSchedulingRepository {
  _Repo() : super(teacherLessons: [lesson(1), lesson(8), lesson(15)]);
  LessonCancellationReason? reason;
  bool? isChargeable;
  DateTime? key;
  OccurrenceScope? scope;
  @override
  Future<LessonSchedule> cancelLesson({
    required LessonSchedule lesson,
    required OccurrenceScope scope,
    LessonCancellationReason reason = LessonCancellationReason.teacherCancelled,
    bool isChargeable = true,
    String? cancellationNote,
  }) async {
    key = lesson.occurrenceKeyUtc;
    this.scope = scope;
    this.reason = reason;
    this.isChargeable = isChargeable;
    return teacherLessons
        .first; // API responds with the anchor row, not the expanded occurrence.
  }
}
