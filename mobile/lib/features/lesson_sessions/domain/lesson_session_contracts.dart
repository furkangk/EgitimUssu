class LessonSession {
  const LessonSession({
    required this.id,
    required this.lessonScheduleId,
    required this.teacherUserId,
    required this.studentId,
    required this.subject,
    required this.status,
    this.plannedStartAtUtc,
    this.topicTitle,
    this.coveredContent,
    this.teacherNotes,
    this.actualStartAtUtc,
    this.actualEndAtUtc,
    this.durationMinutes,
    this.attendanceStatus,
  });

  final String id;
  final String? lessonScheduleId;
  final String teacherUserId;
  final String studentId;
  final String subject;
  final String status;
  final DateTime? plannedStartAtUtc;
  final String? topicTitle;
  final String? coveredContent;
  final String? teacherNotes;
  final DateTime? actualStartAtUtc;
  final DateTime? actualEndAtUtc;
  final int? durationMinutes;
  final String? attendanceStatus;
}

abstract interface class LessonSessionRepository {
  Future<LessonSession> createSession(LessonSession lessonSession);
  Future<LessonSession> getSession(String lessonSessionId);
  Future<LessonSession> completeSession(LessonSession lessonSession);
}
