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

  @override
  Future<ProgressOverview> getOverview(String studentId) async {
    final j = await _apiClient.get('$_base/students/$studentId/overview');
    return ProgressOverview(
      masteredCount: _int(j['masteredCount']),
      proficientCount: _int(j['proficientCount']),
      developingCount: _int(j['developingCount']),
      weakCount: _int(j['weakCount']),
      notStartedCount: _int(j['notStartedCount']),
      activeGoalCount: _int(j['activeGoalCount']),
      weakSpots: _masteryList(j['weakSpots']),
      strengths: _masteryList(j['strengths']),
    );
  }

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
