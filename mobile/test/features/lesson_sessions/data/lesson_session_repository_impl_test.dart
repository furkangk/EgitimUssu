import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/data/repositories/lesson_session_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../helpers.dart';

void main() {
  test('createSession posts to lesson sessions endpoint', () async {
    final apiClient = _FakeApiClient();
    final repository = LessonSessionRepositoryImpl(
      apiClient: apiClient,
      config: _config,
    );

    final session = await repository.createSession(
      LessonSession(
        id: '',
        lessonScheduleId: 'lesson-1',
        teacherUserId: 'teacher-1',
        studentId: 'student-1',
        subject: 'Matematik',
        status: 'Planned',
        plannedStartAtUtc: DateTime.utc(2026, 5, 7, 12),
        topicTitle: 'Denklemler',
      ),
    );

    expect(apiClient.lastPath, '/api/lesson-sessions');
    expect(apiClient.lastData?['lessonScheduleId'], 'lesson-1');
    expect(session.id, 'session-1');
    expect(session.topicTitle, 'Denklemler');
  });

  test(
    'completeSession posts completion payload to session endpoint',
    () async {
      final apiClient = _FakeApiClient();
      final repository = LessonSessionRepositoryImpl(
        apiClient: apiClient,
        config: _config,
      );

      final session = await repository.completeSession(
        LessonSession(
          id: 'session-1',
          lessonScheduleId: 'lesson-1',
          teacherUserId: 'teacher-1',
          studentId: 'student-1',
          subject: 'Matematik',
          status: 'Planned',
          actualStartAtUtc: DateTime.utc(2026, 5, 7, 12),
          actualEndAtUtc: DateTime.utc(2026, 5, 7, 13),
          attendanceStatus: 'Present',
          topicTitle: 'Denklemler',
          coveredContent: 'Birinci derece denklemler',
        ),
      );

      expect(apiClient.lastPath, '/api/lesson-sessions/session-1/complete');
      expect(apiClient.lastData?['attendanceStatus'], 1);
      expect(session.status, 'Completed');
    },
  );
}

const _config = AppConfig(
  apiBaseUrl: 'http://localhost',
  appEnvironment: 'test',
  useMockFallback: false,
  mockFallbackFeatures: <String>{},
);

class _FakeApiClient extends ApiClient {
  _FakeApiClient() : super(dio: Dio(), tokenStorage: InMemoryTokenStorage());

  String? lastPath;
  Map<String, dynamic>? lastData;

  @override
  Future<Map<String, dynamic>> post(
    String path, {
    Map<String, dynamic>? data,
  }) async {
    lastPath = path;
    lastData = data;
    return <String, dynamic>{
      'id': 'session-1',
      'lessonScheduleId': data?['lessonScheduleId'] ?? 'lesson-1',
      'teacherUserId': 'teacher-1',
      'studentId': 'student-1',
      'subject': 'Matematik',
      'status': path.endsWith('/complete') ? 'Completed' : 'Planned',
      'topicTitle': data?['topicTitle'] ?? 'Denklemler',
      'coveredContent': data?['coveredContent'],
      'actualStartAtUtc': data?['actualStartAtUtc'],
      'actualEndAtUtc': data?['actualEndAtUtc'],
      'attendanceStatus': 'Present',
    };
  }
}
