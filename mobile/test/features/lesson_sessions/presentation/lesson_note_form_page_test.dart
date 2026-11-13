import 'dart:async';

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_theme.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_note_form_page.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../helpers/fake_auth_repository.dart';

void main() {
  tearDown(injector.reset);

  testWidgets('fits a 320 pixel screen with 200 percent text', (tester) async {
    await _pump(tester, size: const Size(320, 568), textScale: 2);
    await tester.drag(find.byType(ListView), const Offset(0, -800));
    await tester.pumpAndSettle();
    expect(find.byType(FilledButton).hitTestable(), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  for (final ambiguous in [false, true]) {
    testWidgets(
      'unresolved context requires explicit selection (ambiguous=$ambiguous)',
      (tester) async {
        final sessions = _SessionRepository()..duplicateCompleted = ambiguous;
        final notes = _NoteRepository();
        await _pump(
          tester,
          sessions: sessions,
          notes: notes,
          initialContext: LessonNoteFormContext(
            studentName: ambiguous ? 'Gerçek Öğrenci' : 'Başka Öğrenci',
            lessonName: 'Matematik',
            lockSelection: true,
          ),
        );
        expect(find.textContaining('Öğrenci ve ders seçin.'), findsOneWidget);
        expect(notes.loadCalls, 0);
        expect(
          tester.widget<FilledButton>(find.byType(FilledButton)).onPressed,
          isNull,
        );
        expect(
          _textFields(tester).every((field) => field.controller!.text.isEmpty),
          isTrue,
        );
      },
    );
  }

  testWidgets(
    'loads real students and only completed sessions without demo content',
    (tester) async {
      final sessions = _SessionRepository();
      await _pump(tester, sessions: sessions);

      expect(sessions.requestedTeacher, 'teacher-live');
      expect(find.text('Gerçek Öğrenci'), findsOneWidget);
      expect(find.textContaining('Matematik ·'), findsOneWidget);
      expect(find.textContaining('Fizik ·'), findsNothing);
      expect(find.text('Zeynep Demir'), findsNothing);
      expect(find.text('Dosya ekle'), findsNothing);
      expect(_textFields(tester)[1].controller!.text, 'Önceki ders özeti');
    },
  );

  testWidgets(
    'save persists entered note and preserves fresh metadata and visibility',
    (tester) async {
      final notes = _NoteRepository();
      await _pump(tester, notes: notes);
      await _enterNote(tester);
      notes.existing = _followUp(
        coveredTopics: 'Son konu',
        recommendations: 'Son öneri',
      );
      await tester.tap(find.text('Ders Notunu Kaydet'));
      await tester.pumpAndSettle();

      expect(notes.saved!.lessonSessionId, 'session-completed');
      expect(notes.saved!.summary, 'Yeni başlık\n\nGerçek ders içeriği');
      expect(notes.saved!.coveredTopics, 'Son konu');
      expect(notes.saved!.recommendations, 'Son öneri');
      expect(notes.saved!.visibility, 'StudentAndParent');
      expect(notes.saved!.assignments, isEmpty);
      expect(find.text('Ders notu kaydedildi.'), findsOneWidget);
    },
  );

  testWidgets('failed save retains input and never shows success', (
    tester,
  ) async {
    final notes = _NoteRepository()
      ..saveError = const ApiException(message: 'Sunucuya ulaşılamadı.');
    await _pump(tester, notes: notes);
    await _enterNote(tester);
    await tester.tap(find.text('Ders Notunu Kaydet'));
    await tester.pumpAndSettle();

    expect(find.text('Sunucuya ulaşılamadı.'), findsOneWidget);
    expect(find.text('Ders notu kaydedildi.'), findsNothing);
    expect(_textFields(tester)[0].controller!.text, 'Yeni başlık');
    expect(_textFields(tester)[1].controller!.text, 'Gerçek ders içeriği');
    expect(
      tester.widget<FilledButton>(find.byType(FilledButton)).onPressed,
      isNotNull,
    );
    notes.saveError = null;
    await tester.ensureVisible(find.text('Tekrar dene'));
    await tester.tap(find.text('Tekrar dene'));
    await tester.pumpAndSettle();
    expect(notes.saved!.summary, 'Yeni başlık\n\nGerçek ders içeriği');
    expect(notes.saveCalls, 2);
    expect(find.text('Ders notu kaydedildi.'), findsOneWidget);
  });

  testWidgets('save is disabled until the pending request completes', (
    tester,
  ) async {
    final pending = Completer<FollowUpAssignment>();
    final notes = _NoteRepository()..pendingSave = pending;
    await _pump(tester, notes: notes);
    await _enterNote(tester);
    await tester.tap(find.text('Ders Notunu Kaydet'));
    await tester.pump();

    expect(
      tester.widget<FilledButton>(find.byType(FilledButton)).onPressed,
      isNull,
    );
    expect(notes.saveCalls, 1);
    expect(find.text('Ders notu kaydedildi.'), findsNothing);
    pending.complete(notes.saved!);
    await tester.pumpAndSettle();
    expect(find.text('Ders notu kaydedildi.'), findsOneWidget);
  });

  testWidgets('existing assignments are preserved and not resubmitted', (
    tester,
  ) async {
    final notes = _NoteRepository()
      ..existing = _followUp(withAssignments: true);
    await _pump(tester, notes: notes);

    expect(find.textContaining('Mevcut takip korunur'), findsOneWidget);
    expect(
      tester.widget<FilledButton>(find.byType(FilledButton)).onPressed,
      isNull,
    );
    expect(notes.saveCalls, 0);
  });

  testWidgets('assignments added after opening prevent a stale note save', (
    tester,
  ) async {
    final notes = _NoteRepository();
    await _pump(tester, notes: notes);
    await _enterNote(tester);
    notes.existing = _followUp(withAssignments: true);
    await tester.tap(find.text('Ders Notunu Kaydet'));
    await tester.pumpAndSettle();

    expect(notes.saveCalls, 0);
    expect(find.text('Ders notu kaydedildi.'), findsNothing);
    expect(
      find.textContaining('Mevcut not ve ödevler değiştirilmedi.'),
      findsOneWidget,
    );
  });

  testWidgets('loading error exposes retry without fake student choices', (
    tester,
  ) async {
    final sessions = _SessionRepository()
      ..loadError = const ApiException(message: 'Dersler alınamadı.');
    await _pump(tester, sessions: sessions);

    expect(find.text('Dersler alınamadı.'), findsOneWidget);
    expect(find.byType(DropdownButtonFormField<String>), findsNothing);
    expect(
      tester.widget<FilledButton>(find.byType(FilledButton)).onPressed,
      isNull,
    );
    sessions.loadError = null;
    await tester.tap(find.text('Tekrar dene'));
    await tester.pumpAndSettle();
    expect(find.text('Gerçek Öğrenci'), findsOneWidget);
  });

  testWidgets('legacy context locks only a matching real session', (
    tester,
  ) async {
    await _pump(
      tester,
      initialContext: const LessonNoteFormContext(
        studentName: 'Gerçek Öğrenci',
        lessonName: 'Matematik',
        lockSelection: true,
      ),
    );

    final selections = tester.widgetList<DropdownButtonFormField<String>>(
      find.byType(DropdownButtonFormField<String>),
    );
    expect(selections.length, 2);
    expect(selections.every((field) => field.onChanged == null), isTrue);
  });
}

Future<void> _pump(
  WidgetTester tester, {
  _SessionRepository? sessions,
  _NoteRepository? notes,
  LessonNoteFormContext? initialContext,
  Size size = const Size(500, 900),
  double textScale = 1,
}) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  injector.registerSingleton<LessonSessionRepository>(
    sessions ?? _SessionRepository(),
  );
  injector.registerSingleton<StudentRepository>(_StudentRepository());
  injector.registerSingleton<AssignmentRepository>(notes ?? _NoteRepository());
  final auth = AuthCubit(
    FakeAuthRepository(
      session: FakeAuthRepository.defaultSession(userId: 'teacher-live'),
    ),
  );
  await auth.login(email: 'teacher@example.com', password: 'password');
  addTearDown(auth.close);
  await tester.pumpWidget(
    BlocProvider.value(
      value: auth,
      child: MaterialApp(
        theme: AppTheme.lightTheme,
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(
            context,
          ).copyWith(textScaler: TextScaler.linear(textScale)),
          child: child!,
        ),
        home: LessonNoteFormPage(initialContext: initialContext),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

List<TextFormField> _textFields(WidgetTester tester) =>
    tester.widgetList<TextFormField>(find.byType(TextFormField)).toList();

Future<void> _enterNote(WidgetTester tester) async {
  await tester.enterText(find.byType(TextFormField).at(0), 'Yeni başlık');
  await tester.enterText(
    find.byType(TextFormField).at(1),
    'Gerçek ders içeriği',
  );
}

FollowUpAssignment _followUp({
  bool withAssignments = false,
  String coveredTopics = 'Korunacak konu',
  String recommendations = 'Korunacak öneri',
}) => FollowUpAssignment(
  lessonSessionId: 'session-completed',
  summary: 'Önceki ders özeti',
  coveredTopics: coveredTopics,
  recommendations: recommendations,
  visibility: 'StudentAndParent',
  assignments: withAssignments
      ? [
          const AssignmentItem(
            id: 'existing-assignment',
            title: 'Mevcut ödev',
            description: 'Korunacak',
          ),
        ]
      : [],
);

class _SessionRepository implements LessonSessionRepository {
  String? requestedTeacher;
  ApiException? loadError;
  bool duplicateCompleted = false;
  @override
  Future<List<LessonSession>> listTeacherSessions(String teacherUserId) async {
    requestedTeacher = teacherUserId;
    if (loadError != null) throw loadError!;
    return [
      if (duplicateCompleted)
        LessonSession(
          id: 'another-session',
          lessonScheduleId: 'another-schedule',
          teacherUserId: teacherUserId,
          studentId: 'student-real',
          subject: 'Matematik',
          status: 'Completed',
          actualEndAtUtc: DateTime.utc(2026, 10, 14, 10),
        ),
      LessonSession(
        id: 'session-completed',
        lessonScheduleId: 'schedule-real',
        teacherUserId: teacherUserId,
        studentId: 'student-real',
        subject: 'Matematik',
        status: 'Completed',
        actualEndAtUtc: DateTime.utc(2026, 10, 15, 10),
      ),
      LessonSession(
        id: 'session-planned',
        lessonScheduleId: 'schedule-future',
        teacherUserId: teacherUserId,
        studentId: 'student-real',
        subject: 'Fizik',
        status: 'Planned',
        plannedStartAtUtc: DateTime.utc(2026, 10, 18, 10),
      ),
    ];
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _StudentRepository implements StudentRepository {
  @override
  Future<List<StudentProfile>> listByTeacher(String teacherUserId) async => [
    const StudentProfile(
      id: 'student-real',
      fullName: 'Gerçek Öğrenci',
      gradeLevel: '10',
      origin: 'TeacherCreated',
      subjects: [],
    ),
  ];
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _NoteRepository implements AssignmentRepository {
  FollowUpAssignment existing = _followUp();
  FollowUpAssignment? saved;
  ApiException? saveError;
  Completer<FollowUpAssignment>? pendingSave;
  int saveCalls = 0;
  int loadCalls = 0;
  @override
  Future<FollowUpAssignment> getFollowUp(String lessonSessionId) async {
    loadCalls++;
    return existing;
  }

  @override
  Future<FollowUpAssignment> saveFollowUp(FollowUpAssignment followUp) async {
    saveCalls++;
    if (saveError != null) throw saveError!;
    saved = followUp;
    return pendingSave == null ? followUp : await pendingSave!.future;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
