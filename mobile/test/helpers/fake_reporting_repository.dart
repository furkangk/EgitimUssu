import 'dart:typed_data';

import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/reporting/domain/reporting_contracts.dart';

/// Ortak `ReportingRepository` sahtesi (mobile_flutter.md §17.1 — sahteler tek noktada).
/// Varsayılan: boş seriler. Hata senaryoları kurucudaki `*Error` alanlarıyla kurulur.
class FakeReportingRepository implements ReportingRepository {
  FakeReportingRepository({
    this.monthly,
    this.freeSlots = const <FreeSlot>[],
    this.weeks = const <StudentStudyWeek>[],
    this.performance = const <StudentPerformanceMonth>[],
    this.generated,
    this.fileBytes,
    this.loadError,
    this.generateError,
    this.downloadError,
  });

  final TeacherMonthlyReport? monthly;
  final List<FreeSlot> freeSlots;
  final List<StudentStudyWeek> weeks;
  final List<StudentPerformanceMonth> performance;
  final GeneratedStudentReport? generated;
  final Uint8List? fileBytes;

  final ApiException? loadError;
  final ApiException? generateError;
  final ApiException? downloadError;

  final List<String> monthlyCalls = <String>[];
  final List<String> studyCalls = <String>[];
  final List<String> generateCalls = <String>[];
  final List<String> downloadCalls = <String>[];

  @override
  Future<TeacherMonthlyReport> teacherMonthly({
    required String teacherUserId,
    int? year,
    int? month,
  }) async {
    monthlyCalls.add('$teacherUserId:$year-$month');
    if (loadError != null) throw loadError!;
    return monthly ??
        const TeacherMonthlyReport(
          teacherUserId: '',
          year: 0,
          month: 0,
          completedLessonCount: 0,
          cancelledLessonCount: 0,
          activeStudentCount: 0,
          expectedAmount: 0,
          collectedAmount: 0,
        );
  }

  @override
  Future<List<FreeSlot>> teacherFreeSlots({
    required String teacherUserId,
    DateTime? from,
    DateTime? to,
  }) async {
    if (loadError != null) throw loadError!;
    return freeSlots;
  }

  @override
  Future<List<StudentStudyWeek>> studentStudyAnalysis({
    required String studentId,
    DateTime? from,
    DateTime? to,
  }) async {
    studyCalls.add(studentId);
    if (loadError != null) throw loadError!;
    return weeks;
  }

  @override
  Future<List<StudentPerformanceMonth>> studentPerformance({
    required String studentId,
    String? subject,
    DateTime? from,
    DateTime? to,
  }) async {
    if (loadError != null) throw loadError!;
    return performance;
  }

  @override
  Future<GeneratedStudentReport> generateStudentReport({
    required String studentId,
    DateTime? from,
    DateTime? to,
    String? note,
  }) async {
    generateCalls.add(studentId);
    if (generateError != null) throw generateError!;
    return generated ??
        GeneratedStudentReport(
          reportId: 'report-1',
          studentId: studentId,
          periodStart: DateTime.utc(2026, 8, 1),
          periodEnd: DateTime.utc(2026, 8, 31),
          fileName: 'rapor.pdf',
          sizeBytes: 2048,
          createdOnUtc: DateTime.utc(2026, 9, 1),
        );
  }

  @override
  Future<Uint8List?> downloadReport(String reportId) async {
    downloadCalls.add(reportId);
    if (downloadError != null) throw downloadError!;
    return fileBytes;
  }
}

/// PDF'i cihazda açan bağımlılığın sahtesi — testte gerçek dosya sistemi kullanılmaz.
class FakeReportFileOpener implements ReportFileOpener {
  FakeReportFileOpener({this.failure});

  final Object? failure;
  final List<String> openedFileNames = <String>[];

  @override
  Future<String> open({
    required String fileName,
    required Uint8List bytes,
  }) async {
    if (failure != null) throw failure!;
    openedFileNames.add(fileName);
    return '/tmp/$fileName';
  }
}
