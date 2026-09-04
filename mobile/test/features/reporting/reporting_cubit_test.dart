import 'dart:typed_data';

import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/reporting/domain/reporting_contracts.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/student_report_cubit.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/teacher_reports_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_reporting_repository.dart';

/// P08 Task 5 (M14): Rapor ekranlarının durum makinesi.
/// Kritik davranış — premium gerektiren uçta `402` (ve öğretmen üyeliği yoksa `403`)
/// **hata değil ayrı bir durum**tur: ekran "kilitli" gösterir, hata balonu değil.
void main() {
  group('TeacherReportsCubit', () {
    test('load aylik ozeti ve bos zamanlari doldurur', () async {
      final repository = FakeReportingRepository(
        monthly: _monthly(),
        freeSlots: <FreeSlot>[_slot()],
      );
      final cubit = TeacherReportsCubit(repository);

      await cubit.load('teacher-1', year: 2026, month: 8);

      expect(cubit.state.status, ReportingStatus.loaded);
      expect(cubit.state.monthly?.completedLessonCount, 12);
      expect(cubit.state.freeSlots, hasLength(1));
      expect(repository.monthlyCalls.single, 'teacher-1:2026-8');
      await cubit.close();
    });

    test('load hata verirse failure olur', () async {
      final repository = FakeReportingRepository(
        loadError: const ApiException(message: 'Sunucuya baglanilamadi.'),
      );
      final cubit = TeacherReportsCubit(repository);

      await cubit.load('teacher-1');

      expect(cubit.state.status, ReportingStatus.failure);
      expect(cubit.state.errorMessage, 'Sunucuya baglanilamadi.');
      await cubit.close();
    });

    test('onceki ay yil sinirindan geriye dogru gecer', () async {
      final repository = FakeReportingRepository(monthly: _monthly());
      final cubit = TeacherReportsCubit(repository);
      await cubit.load('teacher-1', year: 2026, month: 1);

      await cubit.showPreviousMonth();

      expect(cubit.state.year, 2025);
      expect(cubit.state.month, 12);
      expect(repository.monthlyCalls.last, 'teacher-1:2025-12');
      await cubit.close();
    });

    test('sonraki ay yil sinirindan ileriye dogru gecer', () async {
      final repository = FakeReportingRepository(monthly: _monthly());
      final cubit = TeacherReportsCubit(repository);
      await cubit.load('teacher-1', year: 2026, month: 12);

      await cubit.showNextMonth();

      expect(cubit.state.year, 2027);
      expect(cubit.state.month, 1);
      await cubit.close();
    });
  });

  group('StudentReportCubit', () {
    test('load calisma ve performans serilerini doldurur', () async {
      final repository = FakeReportingRepository(
        weeks: <StudentStudyWeek>[_week()],
        performance: <StudentPerformanceMonth>[_performance()],
      );
      final cubit = StudentReportCubit(repository, FakeReportFileOpener());

      await cubit.load('student-1');

      expect(cubit.state.status, ReportingStatus.loaded);
      expect(cubit.state.weeks.single.studyMinutes, 180);
      expect(cubit.state.performance.single.subject, 'Matematik');
      expect(repository.studyCalls.single, 'student-1');
      await cubit.close();
    });

    test('load hata verirse failure olur', () async {
      final repository = FakeReportingRepository(
        loadError: const ApiException(message: 'Analiz alinamadi.'),
      );
      final cubit = StudentReportCubit(repository, FakeReportFileOpener());

      await cubit.load('student-1');

      expect(cubit.state.status, ReportingStatus.failure);
      await cubit.close();
    });

    test('bos seri hata degil bos durumdur (gizlilik kapali olabilir)', () async {
      final repository = FakeReportingRepository();
      final cubit = StudentReportCubit(repository, FakeReportFileOpener());

      await cubit.load('student-1');

      expect(cubit.state.status, ReportingStatus.loaded);
      expect(cubit.state.isEmpty, isTrue);
      await cubit.close();
    });

    test('rapor uretilir, indirilir ve cihazda acilir', () async {
      final opener = FakeReportFileOpener();
      final repository = FakeReportingRepository(
        fileBytes: Uint8List.fromList(<int>[37, 80, 68, 70]),
      );
      final cubit = StudentReportCubit(repository, opener);
      await cubit.load('student-1');

      await cubit.generateAndOpenReport();

      expect(cubit.state.reportStatus, ReportingStatus.loaded);
      expect(cubit.state.isGenerating, isFalse);
      expect(cubit.state.lastReport?.fileName, 'rapor.pdf');
      expect(cubit.state.openedFilePath, '/tmp/rapor.pdf');
      expect(repository.downloadCalls.single, 'report-1');
      expect(opener.openedFileNames.single, 'rapor.pdf');
      await cubit.close();
    });

    test('402 premium gerektirir durumuna dusurur, hata mesaji basmaz', () async {
      final repository = FakeReportingRepository(
        generateError: const ApiException(
          message: 'PDF rapor premium uyelik gerektirir.',
          code: 'reporting.premium_required',
          statusCode: 402,
        ),
      );
      final cubit = StudentReportCubit(repository, FakeReportFileOpener());
      await cubit.load('student-1');

      await cubit.generateAndOpenReport();

      expect(cubit.state.reportStatus, ReportingStatus.premiumRequired);
      expect(cubit.state.status, ReportingStatus.loaded);
      expect(cubit.state.isGenerating, isFalse);
      await cubit.close();
    });

    test('403 de premium/paylasim kilidi olarak gosterilir', () async {
      final repository = FakeReportingRepository(
        generateError: const ApiException(
          message: 'Bu rapora erisim yetkiniz yok.',
          kind: ApiErrorKind.auth,
          statusCode: 403,
        ),
      );
      final cubit = StudentReportCubit(repository, FakeReportFileOpener());
      await cubit.load('student-1');

      await cubit.generateAndOpenReport();

      expect(cubit.state.reportStatus, ReportingStatus.premiumRequired);
      expect(cubit.state.errorMessage, 'Bu rapora erisim yetkiniz yok.');
      await cubit.close();
    });

    test('indirilen dosya bos gelirse failure olur', () async {
      final repository = FakeReportingRepository();
      final cubit = StudentReportCubit(repository, FakeReportFileOpener());
      await cubit.load('student-1');

      await cubit.generateAndOpenReport();

      expect(cubit.state.reportStatus, ReportingStatus.failure);
      expect(cubit.state.errorMessage, isNotNull);
      await cubit.close();
    });

    test('dosya acilamazsa failure olur ama rapor kaydi durur', () async {
      final repository = FakeReportingRepository(
        fileBytes: Uint8List.fromList(<int>[37, 80, 68, 70]),
      );
      final cubit = StudentReportCubit(
        repository,
        FakeReportFileOpener(failure: StateError('acilamadi')),
      );
      await cubit.load('student-1');

      await cubit.generateAndOpenReport();

      expect(cubit.state.reportStatus, ReportingStatus.failure);
      expect(cubit.state.lastReport, isNotNull);
      expect(cubit.state.openedFilePath, isNull);
      await cubit.close();
    });

    test('uretim suresince isGenerating true kalir', () async {
      final repository = FakeReportingRepository(
        fileBytes: Uint8List.fromList(<int>[37, 80, 68, 70]),
      );
      final cubit = StudentReportCubit(repository, FakeReportFileOpener());
      await cubit.load('student-1');
      final seen = <bool>[];
      final subscription = cubit.stream.listen((s) => seen.add(s.isGenerating));

      await cubit.generateAndOpenReport();
      await Future<void>.delayed(Duration.zero);

      expect(seen.first, isTrue);
      expect(seen.last, isFalse);
      await subscription.cancel();
      await cubit.close();
    });
  });
}

TeacherMonthlyReport _monthly() => const TeacherMonthlyReport(
      teacherUserId: 'teacher-1',
      year: 2026,
      month: 8,
      completedLessonCount: 12,
      cancelledLessonCount: 1,
      activeStudentCount: 5,
      expectedAmount: 4800,
      collectedAmount: 3600,
    );

FreeSlot _slot() => FreeSlot(
      startAtUtc: DateTime.utc(2026, 9, 7, 9),
      endAtUtc: DateTime.utc(2026, 9, 7, 11),
      durationMinutes: 120,
    );

StudentStudyWeek _week() => StudentStudyWeek(
      year: 2026,
      weekOfYear: 35,
      weekStart: DateTime.utc(2026, 8, 24),
      studyMinutes: 180,
      sessionCount: 4,
      testCount: 2,
    );

StudentPerformanceMonth _performance() => const StudentPerformanceMonth(
      subject: 'Matematik',
      year: 2026,
      month: 8,
      masteryScore: 72,
      averageNet: 0.68,
      testCount: 6,
      delta: 5,
    );
