import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/study/data/models/study_models.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';

class StudyRepositoryImpl implements StudyRepository {
  StudyRepositoryImpl({required ApiClient apiClient}) : _apiClient = apiClient;

  final ApiClient _apiClient;

  static const _base = '/api/study';

  @override
  Future<StudyDashboard> getDashboard(String studentId) async {
    final r = await _apiClient.get('$_base/students/$studentId/dashboard');
    return StudyMappers.dashboard(r);
  }

  @override
  Future<StudyStreak> getStreak(String studentId) async {
    final r = await _apiClient.get('$_base/students/$studentId/streak');
    return StudyMappers.streak(r);
  }

  @override
  Future<StudyGoal?> getGoals(String studentId) async {
    final r = await _apiClient.get('$_base/students/$studentId/goals');
    if (r.isEmpty || r['dailyGoalMinutes'] == null) {
      return null;
    }
    return StudyMappers.goal(r);
  }

  @override
  Future<StudyGoal> updateGoals(
    String studentId, {
    required int dailyGoalMinutes,
    int? weeklyGoalMinutes,
    double? targetNet,
    String? subject,
  }) async {
    final r = await _apiClient.put(
      '$_base/students/$studentId/goals',
      data: <String, dynamic>{
        'dailyGoalMinutes': dailyGoalMinutes,
        'weeklyGoalMinutes': weeklyGoalMinutes,
        'targetNet': targetNet,
        'targetScore': null,
        'subject': subject,
      },
    );
    return StudyMappers.goal(r)!;
  }

  @override
  Future<List<StudySession>> listSessions(String studentId) async {
    final r = await _apiClient.getList('$_base/students/$studentId/sessions');
    return r
        .whereType<Map<String, dynamic>>()
        .map(StudyMappers.session)
        .toList();
  }

  @override
  Future<StudySession> startSession(
    String studentId, {
    required String subject,
    String? topic,
  }) async {
    final r = await _apiClient.post(
      '$_base/sessions/start',
      data: <String, dynamic>{
        'studentId': studentId,
        'subject': subject,
        'topic': topic,
      },
    );
    return StudyMappers.session(r);
  }

  @override
  Future<StudySession> pauseSession(String sessionId) async =>
      StudyMappers.session(await _apiClient.post('$_base/sessions/$sessionId/pause'));

  @override
  Future<StudySession> resumeSession(String sessionId) async =>
      StudyMappers.session(await _apiClient.post('$_base/sessions/$sessionId/resume'));

  @override
  Future<StudySession> completeSession(
    String sessionId, {
    String? personalNote,
  }) async {
    final r = await _apiClient.post(
      '$_base/sessions/$sessionId/complete',
      data: <String, dynamic>{'personalNote': personalNote},
    );
    return StudyMappers.session(r);
  }

  @override
  Future<void> discardSession(String sessionId) async {
    await _apiClient.post('$_base/sessions/$sessionId/discard');
  }

  @override
  Future<StudySession> createManualSession(
    String studentId, {
    required String subject,
    String? topic,
    required int effectiveMinutes,
    required DateTime studiedOnUtc,
    String? personalNote,
  }) async {
    final r = await _apiClient.post(
      '$_base/sessions/manual',
      data: <String, dynamic>{
        'studentId': studentId,
        'subject': subject,
        'topic': topic,
        'effectiveMinutes': effectiveMinutes,
        'studiedOnUtc': studiedOnUtc.toUtc().toIso8601String(),
        'personalNote': personalNote,
      },
    );
    return StudyMappers.session(r);
  }

  @override
  Future<List<TestResult>> listTests(String studentId) async {
    final r = await _apiClient.getList('$_base/students/$studentId/test-results');
    return r.whereType<Map<String, dynamic>>().map(StudyMappers.test).toList();
  }

  @override
  Future<TestResult> recordTest(
    String studentId, {
    required String subject,
    String? topic,
    required String testType,
    String? testName,
    required int totalQuestions,
    required int correct,
    required int wrong,
    required int blank,
    int? penaltyDivisor,
    int? durationMinutes,
    required DateTime takenOnUtc,
  }) async {
    final r = await _apiClient.post(
      '$_base/test-results',
      data: <String, dynamic>{
        'studentId': studentId,
        'subject': subject,
        'topic': topic,
        'testType': testType,
        'testName': testName,
        'totalQuestions': totalQuestions,
        'correct': correct,
        'wrong': wrong,
        'blank': blank,
        'penaltyDivisor': penaltyDivisor,
        'durationMinutes': durationMinutes,
        'takenOnUtc': takenOnUtc.toUtc().toIso8601String(),
      },
    );
    return StudyMappers.test(r);
  }

  @override
  Future<List<StudyAchievement>> getAchievements(String studentId) async {
    final r = await _apiClient.getList('$_base/students/$studentId/achievements');
    return r
        .whereType<Map<String, dynamic>>()
        .map(StudyMappers.achievement)
        .toList();
  }

  @override
  Future<WeeklySummary> getWeeklySummary(String studentId) async {
    final r = await _apiClient.get('$_base/students/$studentId/weekly-summary');
    return StudyMappers.weekly(r);
  }

  @override
  Future<StudySharing> getSharing(String studentId) async {
    final r = await _apiClient.get('$_base/students/$studentId/sharing');
    return StudyMappers.sharing(r);
  }

  @override
  Future<StudySharing> updateSharing(
    String studentId, {
    required bool shareStudyWithParent,
    required bool shareTestsWithParent,
    required bool shareStudyWithTeacher,
    required bool shareTestsWithTeacher,
  }) async {
    final r = await _apiClient.put(
      '$_base/students/$studentId/sharing',
      data: <String, dynamic>{
        'shareStudyWithParent': shareStudyWithParent,
        'shareTestsWithParent': shareTestsWithParent,
        'shareStudyWithTeacher': shareStudyWithTeacher,
        'shareTestsWithTeacher': shareTestsWithTeacher,
      },
    );
    return StudyMappers.sharing(r);
  }
}
