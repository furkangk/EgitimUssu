import 'dart:typed_data';

import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/reporting/domain/reporting_contracts.dart';

class ReportingRepositoryImpl implements ReportingRepository {
  ReportingRepositoryImpl({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  static const _base = '/api/reporting';

  static int _int(dynamic v) => v is num ? v.toInt() : int.tryParse('$v') ?? 0;

  static double _double(dynamic v) =>
      v is num ? v.toDouble() : double.tryParse('$v') ?? 0;

  static DateTime? _dateOrNull(dynamic v) =>
      v == null ? null : DateTime.tryParse('$v');

  static DateTime _date(dynamic v) => _dateOrNull(v) ?? DateTime.utc(1970);

  /// `DateOnly` alan uçlar (çalışma/performans) için `yyyy-MM-dd`.
  static String _dateOnly(DateTime value) =>
      '${value.year.toString().padLeft(4, '0')}-'
      '${value.month.toString().padLeft(2, '0')}-'
      '${value.day.toString().padLeft(2, '0')}';

  @override
  Future<TeacherMonthlyReport> teacherMonthly({
    required String teacherUserId,
    int? year,
    int? month,
  }) async {
    final j = await _apiClient.get(
      '$_base/teachers/$teacherUserId/monthly',
      queryParameters: <String, dynamic>{
        if (year != null) 'year': year,
        if (month != null) 'month': month,
      },
    );
    return TeacherMonthlyReport(
      teacherUserId: '${j['teacherUserId']}',
      year: _int(j['year']),
      month: _int(j['month']),
      completedLessonCount: _int(j['completedLessonCount']),
      cancelledLessonCount: _int(j['cancelledLessonCount']),
      activeStudentCount: _int(j['activeStudentCount']),
      expectedAmount: _double(j['expectedAmount']),
      collectedAmount: _double(j['collectedAmount']),
      updatedOnUtc: _dateOrNull(j['updatedOnUtc']),
    );
  }

  @override
  Future<List<FreeSlot>> teacherFreeSlots({
    required String teacherUserId,
    DateTime? from,
    DateTime? to,
  }) async {
    final r = await _apiClient.getList(
      '$_base/teachers/$teacherUserId/free-slots',
      queryParameters: <String, dynamic>{
        if (from != null) 'from': from.toUtc().toIso8601String(),
        if (to != null) 'to': to.toUtc().toIso8601String(),
      },
    );
    return r.whereType<Map<String, dynamic>>().map((j) => FreeSlot(
          startAtUtc: _date(j['startAtUtc']),
          endAtUtc: _date(j['endAtUtc']),
          durationMinutes: _int(j['durationMinutes']),
        )).toList();
  }

  @override
  Future<List<StudentStudyWeek>> studentStudyAnalysis({
    required String studentId,
    DateTime? from,
    DateTime? to,
  }) async {
    final r = await _apiClient.getList(
      '$_base/students/$studentId/study-analysis',
      queryParameters: <String, dynamic>{
        if (from != null) 'from': _dateOnly(from),
        if (to != null) 'to': _dateOnly(to),
      },
    );
    return r.whereType<Map<String, dynamic>>().map((j) => StudentStudyWeek(
          year: _int(j['year']),
          weekOfYear: _int(j['weekOfYear']),
          weekStart: _date(j['weekStart']),
          studyMinutes: _int(j['studyMinutes']),
          sessionCount: _int(j['sessionCount']),
          testCount: _int(j['testCount']),
        )).toList();
  }

  @override
  Future<List<StudentPerformanceMonth>> studentPerformance({
    required String studentId,
    String? subject,
    DateTime? from,
    DateTime? to,
  }) async {
    final r = await _apiClient.getList(
      '$_base/students/$studentId/performance',
      queryParameters: <String, dynamic>{
        if (subject != null) 'subject': subject,
        if (from != null) 'from': _dateOnly(from),
        if (to != null) 'to': _dateOnly(to),
      },
    );
    return r.whereType<Map<String, dynamic>>().map((j) => StudentPerformanceMonth(
          subject: '${j['subject']}',
          year: _int(j['year']),
          month: _int(j['month']),
          masteryScore: _int(j['masteryScore']),
          averageNet: _double(j['averageNet']),
          testCount: _int(j['testCount']),
          delta: _int(j['delta']),
        )).toList();
  }

  @override
  Future<GeneratedStudentReport> generateStudentReport({
    required String studentId,
    DateTime? from,
    DateTime? to,
    String? note,
  }) async {
    final j = await _apiClient.post(
      '$_base/students/$studentId/report',
      data: <String, dynamic>{
        'from': from == null ? null : _dateOnly(from),
        'to': to == null ? null : _dateOnly(to),
        'note': note,
      },
    );
    return GeneratedStudentReport(
      reportId: '${j['reportId']}',
      studentId: '${j['studentId']}',
      periodStart: _date(j['periodStart']),
      periodEnd: _date(j['periodEnd']),
      fileName: '${j['fileName']}',
      sizeBytes: _int(j['sizeBytes']),
      createdOnUtc: _date(j['createdOnUtc']),
    );
  }

  @override
  Future<Uint8List?> downloadReport(String reportId) =>
      _apiClient.getBytes('$_base/reports/$reportId/file');
}
