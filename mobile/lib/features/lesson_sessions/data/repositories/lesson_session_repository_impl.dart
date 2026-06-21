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
    try {
      final response = await _apiClient.get(
        '/api/lesson-sessions/$lessonSessionId',
      );
      return LessonSessionModel.fromJson(response);
    } on ApiException {
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
