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

  test('startSession posts to start endpoint and returns InProgress', () async {
    final apiClient = _FakeApiClient();
    final repository = LessonSessionRepositoryImpl(
      apiClient: apiClient,
      config: _config,
    );

    final session = await repository.startSession('session-1');

    expect(apiClient.lastPath, '/api/lesson-sessions/session-1/start');
    expect(session.status, 'InProgress');
  });

  test('cancelSession posts reason and returns Cancelled', () async {
    final apiClient = _FakeApiClient();
    final repository = LessonSessionRepositoryImpl(
      apiClient: apiClient,
      config: _config,
    );

    final session = await repository.cancelSession(
      'session-1',
      reason: 'Ogrenci hastalandi',
    );

    expect(apiClient.lastPath, '/api/lesson-sessions/session-1/cancel');
    expect(apiClient.lastData?['reason'], 'Ogrenci hastalandi');
    expect(session.status, 'Cancelled');
    expect(session.cancellationReason, 'Ogrenci hastalandi');
  });

  test('createSessionFromSchedule posts to from-schedule endpoint', () async {
    final apiClient = _FakeApiClient();
    final repository = LessonSessionRepositoryImpl(
      apiClient: apiClient,
      config: _config,
    );

    final session = await repository.createSessionFromSchedule('lesson-1');

    expect(apiClient.lastPath, '/api/lesson-sessions/from-schedule/lesson-1');
    expect(session.id, 'session-1');
    expect(session.status, 'Planned');
    expect(session.meetingUrl, 'https://meet.example.com/abc');
  });

  test('session model reads meeting and recording urls', () async {
    final apiClient = _FakeApiClient();
    final repository = LessonSessionRepositoryImpl(
      apiClient: apiClient,
      config: _config,
    );

    final session = await repository.startSession('session-1');

    expect(session.meetingUrl, 'https://meet.example.com/abc');
    expect(session.recordingUrl, 'https://kayit.example.com/1');
  });
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
      'status': switch (path) {
        final String p when p.endsWith('/complete') => 'Completed',
        final String p when p.endsWith('/start') => 'InProgress',
        final String p when p.endsWith('/cancel') => 'Cancelled',
        _ => 'Planned',
      },
      'meetingUrl': 'https://meet.example.com/abc',
      'recordingUrl': 'https://kayit.example.com/1',
      'cancellationReason': data?['reason'],
      'topicTitle': data?['topicTitle'] ?? 'Denklemler',
      'coveredContent': data?['coveredContent'],
      'actualStartAtUtc': data?['actualStartAtUtc'],
      'actualEndAtUtc': data?['actualEndAtUtc'],
      'attendanceStatus': 'Present',
    };
  }
}
