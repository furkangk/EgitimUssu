class LessonSchedule {
  const LessonSchedule({
    required this.id,
    required this.teacherUserId,
    required this.studentId,
    required this.subject,
    required this.lessonFormat,
    required this.startAtUtc,
    required this.endAtUtc,
    required this.timeZone,
    this.status = 'Planned',
    this.recurrenceRule,
    this.reminderOffsetMinutes,
    this.locationLabel,
    this.meetingUrl,
    this.notes,
  });

  final String id;
  final String teacherUserId;
  final String studentId;
  final String subject;
  final String lessonFormat;
  final DateTime startAtUtc;
  final DateTime endAtUtc;
  final String timeZone;
  final String status;
  final String? recurrenceRule;
  final int? reminderOffsetMinutes;
  final String? locationLabel;

  /// Online dersler icin toplanti baglantisi (Zoom/Meet vb.).
  final String? meetingUrl;
  final String? notes;
}

abstract interface class SchedulingRepository {
  Future<LessonSchedule> createLesson(LessonSchedule lessonSchedule);
  Future<LessonSchedule> updateLesson(LessonSchedule lessonSchedule);
  Future<LessonSchedule> getLesson(String lessonId);
  Future<LessonSchedule> cancelLesson({
    required String lessonId,
    String? cancellationNote,
  });
  Future<LessonSchedule> completeLesson({required String lessonId});
  Future<List<LessonSchedule>> listTeacherLessons({
    required String teacherUserId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
  });
}
