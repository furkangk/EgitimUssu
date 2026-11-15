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
    this.meetingUrl,
    this.recordingUrl,
    this.cancellationReason,
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

  /// M05-4: Online dersin toplanti baglantisi (varsa "Derse katil" icin).
  final String? meetingUrl;

  /// M05-4: Ders kaydinin baglantisi.
  final String? recordingUrl;

  /// M05-1: Ders iptal edildiyse nedeni.
  final String? cancellationReason;
}

abstract interface class LessonSessionRepository {
  Future<List<LessonSession>> listTeacherSessions(String teacherUserId);
  Future<LessonSession> createSession(LessonSession lessonSession);
  Future<LessonSession> getSession(String lessonSessionId);
  Future<LessonSession> completeSession(LessonSession lessonSession);

  /// M05-1: Planli dersi baslatir (Planned -> InProgress).
  Future<LessonSession> startSession(String lessonSessionId);

  /// M05-1: Planli veya devam eden dersi iptal eder.
  Future<LessonSession> cancelSession(String lessonSessionId, {String? reason});

  /// M05-2: Planlı dersten (M04) oturum türetir. Aynı oluşum için idempotent:
  /// ikinci çağrı yeni oturum açmaz, mevcut oturumu döner.
  Future<LessonSession> createSessionFromSchedule(
    String lessonScheduleId, {
    DateTime? occurrenceStartAtUtc,
  });
}
