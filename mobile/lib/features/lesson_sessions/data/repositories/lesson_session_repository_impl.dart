import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/data/models/lesson_session_model.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';

class LessonSessionRepositoryImpl implements LessonSessionRepository {
  LessonSessionRepositoryImpl({
    required ApiClient apiClient,
    required AppConfig config,
  }) : _apiClient = apiClient,
       _config = config;

  final ApiClient _apiClient;
  final AppConfig _config;

  @override
  Future<List<LessonSession>> listTeacherSessions(String teacherUserId) async {
    final response = await _apiClient.getList(
      '/api/lesson-sessions',
      queryParameters: <String, dynamic>{'teacherUserId': teacherUserId},
    );
    return response
        .whereType<Map<String, dynamic>>()
        .map(LessonSessionModel.fromJson)
        .toList();
  }

  @override
  Future<LessonSession> createSession(LessonSession lessonSession) async {
    final model = _toModel(lessonSession);
    try {
      final response = await _apiClient.post(
        '/api/lesson-sessions',
        data: model.toCreatePayload(),
      );
      return LessonSessionModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('lesson_sessions')) {
        return LessonSessionModel.demo(
          id: DateTime.now().microsecondsSinceEpoch.toString(),
          lessonScheduleId: lessonSession.lessonScheduleId,
          teacherUserId: lessonSession.teacherUserId,
          studentId: lessonSession.studentId,
          subject: lessonSession.subject,
          plannedStartAtUtc:
              lessonSession.plannedStartAtUtc ?? DateTime.now().toUtc(),
        );
      }
      rethrow;
    }
  }

  @override
  Future<LessonSession> getSession(String lessonSessionId) async {
    if (_config.isMockFallbackEnabled('lesson_sessions')) {
      return LessonSessionModel.demo(
        id: lessonSessionId,
        lessonScheduleId: 'lesson-1',
        teacherUserId: 'mock-teacher-user',
        studentId: 'student-1',
        subject: 'Matematik',
        plannedStartAtUtc: DateTime.now().toUtc(),
      );
    }
    try {
      final response = await _apiClient.get(
        '/api/lesson-sessions/$lessonSessionId',
      );
      return LessonSessionModel.fromJson(response);
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<LessonSession> completeSession(LessonSession lessonSession) async {
    final model = _toModel(lessonSession);
    try {
      final response = await _apiClient.post(
        '/api/lesson-sessions/${lessonSession.id}/complete',
        data: model.toCompletePayload(),
      );
      return LessonSessionModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('lesson_sessions')) {
        return LessonSessionModel(
          id: lessonSession.id,
          lessonScheduleId: lessonSession.lessonScheduleId,
          teacherUserId: lessonSession.teacherUserId,
          studentId: lessonSession.studentId,
          subject: lessonSession.subject,
          status: 'Completed',
          topicTitle: lessonSession.topicTitle,
          coveredContent: lessonSession.coveredContent,
          teacherNotes: lessonSession.teacherNotes,
          actualStartAtUtc: lessonSession.actualStartAtUtc,
          actualEndAtUtc: lessonSession.actualEndAtUtc,
          plannedStartAtUtc: lessonSession.plannedStartAtUtc,
          durationMinutes:
              lessonSession.actualStartAtUtc == null ||
                  lessonSession.actualEndAtUtc == null
              ? null
              : lessonSession.actualEndAtUtc!
                    .difference(lessonSession.actualStartAtUtc!)
                    .inMinutes,
          attendanceStatus: lessonSession.attendanceStatus ?? 'Present',
        );
      }
      rethrow;
    }
  }

  @override
  Future<LessonSession> startSession(String lessonSessionId) async {
    final response = await _apiClient.post(
      '/api/lesson-sessions/$lessonSessionId/start',
    );
    return LessonSessionModel.fromJson(response);
  }

  @override
  Future<LessonSession> cancelSession(
    String lessonSessionId, {
    String? reason,
  }) async {
    final response = await _apiClient.post(
      '/api/lesson-sessions/$lessonSessionId/cancel',
      data: <String, dynamic>{'reason': reason},
    );
    return LessonSessionModel.fromJson(response);
  }

  @override
  Future<LessonSession> createSessionFromSchedule(
    String lessonScheduleId, {
    DateTime? occurrenceStartAtUtc,
  }) async {
    final response = await _apiClient.post(
      '/api/lesson-sessions/from-schedule',
      data: <String, dynamic>{
        'lessonScheduleId': lessonScheduleId,
        if (occurrenceStartAtUtc != null)
          'occurrenceStartAtUtc': occurrenceStartAtUtc
              .toUtc()
              .toIso8601String(),
      },
    );
    return LessonSessionModel.fromJson(response);
  }

  LessonSessionModel _toModel(LessonSession lessonSession) {
    return LessonSessionModel(
      id: lessonSession.id,
      lessonScheduleId: lessonSession.lessonScheduleId,
      teacherUserId: lessonSession.teacherUserId,
      studentId: lessonSession.studentId,
      subject: lessonSession.subject,
      status: lessonSession.status,
      topicTitle: lessonSession.topicTitle,
      coveredContent: lessonSession.coveredContent,
      teacherNotes: lessonSession.teacherNotes,
      actualStartAtUtc: lessonSession.actualStartAtUtc,
      actualEndAtUtc: lessonSession.actualEndAtUtc,
      plannedStartAtUtc: lessonSession.plannedStartAtUtc,
      durationMinutes: lessonSession.durationMinutes,
      attendanceStatus: lessonSession.attendanceStatus,
    );
  }
}
