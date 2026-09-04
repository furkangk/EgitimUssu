import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/progress/domain/progress_contracts.dart';

class ProgressRepositoryImpl implements ProgressRepository {
  ProgressRepositoryImpl({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  static const _base = '/api/progress-tracking';

  static double _double(dynamic v) =>
      v is num ? v.toDouble() : double.tryParse('$v') ?? 0;

  static int _int(dynamic v) => v is num ? v.toInt() : int.tryParse('$v') ?? 0;

  static TopicMastery _mastery(Map<String, dynamic> j) => TopicMastery(
        subject: '${j['subject']}',
        topic: '${j['topic']}',
        masteryLevel: '${j['masteryLevel']}',
        masteryScore: _double(j['masteryScore']),
        totalStudyMinutes: _int(j['totalStudyMinutes']),
        testAttemptCount: _int(j['testAttemptCount']),
        averageNetRatio:
            j['averageNetRatio'] == null ? null : _double(j['averageNetRatio']),
        trend: '${j['trend']}',
        isWeakSpot: j['isWeakSpot'] as bool? ?? false,
        isStrength: j['isStrength'] as bool? ?? false,
      );

  static List<TopicMastery> _masteryList(dynamic raw) => (raw as List<dynamic>? ??
          <dynamic>[])
      .whereType<Map<String, dynamic>>()
      .map(_mastery)
      .toList();

  static String _dateOnly(DateTime value) =>
      '${value.year.toString().padLeft(4, '0')}-'
      '${value.month.toString().padLeft(2, '0')}-'
      '${value.day.toString().padLeft(2, '0')}';

  static ProgressOverview _overview(Map<String, dynamic> j) => ProgressOverview(
      masteredCount: _int(j['masteredCount']),
      proficientCount: _int(j['proficientCount']),
      developingCount: _int(j['developingCount']),
      weakCount: _int(j['weakCount']),
      notStartedCount: _int(j['notStartedCount']),
      activeGoalCount: _int(j['activeGoalCount']),
      weakSpots: _masteryList(j['weakSpots']),
      strengths: _masteryList(j['strengths']),
    );

  @override
  Future<ProgressOverview> getOverview(String studentId) async =>
      _overview(await _apiClient.get('$_base/students/$studentId/overview'));

  @override
  Future<ProgressOverview> getTeacherStudentOverview({
    required String teacherUserId,
    required String studentId,
  }) async => _overview(
        await _apiClient.get(
          '$_base/teachers/$teacherUserId/students/$studentId/overview',
        ),
      );

  @override
  Future<ProgressOverview> getParentChildOverview({
    required String parentUserId,
    required String studentId,
  }) async => _overview(
        await _apiClient.get(
          '$_base/parents/$parentUserId/children/$studentId/overview',
        ),
      );

  @override
  Future<List<ProgressSnapshot>> listSnapshots(
    String studentId, {
    String? subject,
    String? period,
    DateTime? from,
    DateTime? to,
  }) async {
    final query = <String, dynamic>{
      if (subject != null) 'subject': subject,
      if (period != null) 'period': period,
      if (from != null) 'from': _dateOnly(from),
      if (to != null) 'to': _dateOnly(to),
    };
    final r = await _apiClient.getList(
      '$_base/students/$studentId/snapshots',
      queryParameters: query.isEmpty ? null : query,
    );
    return r.whereType<Map<String, dynamic>>().map(_snapshot).toList();
  }

  static ProgressSnapshot _snapshot(Map<String, dynamic> j) => ProgressSnapshot(
        subject: '${j['subject']}',
        period: '${j['period']}',
        periodStart:
            DateTime.tryParse('${j['periodStart']}') ?? DateTime.utc(1970),
        masteryScore: _int(j['masteryScore']),
        studyMinutes: _int(j['studyMinutes']),
        testCount: _int(j['testCount']),
        averageNet: _double(j['averageNet']),
        trend: '${j['trend']}',
      );

  @override
  Future<List<TopicMastery>> listMastery(String studentId, {String? subject}) async {
    final r = await _apiClient.getList(
      '$_base/students/$studentId/mastery',
      queryParameters:
          subject == null ? null : <String, dynamic>{'subject': subject},
    );
    return r.whereType<Map<String, dynamic>>().map(_mastery).toList();
  }
}
