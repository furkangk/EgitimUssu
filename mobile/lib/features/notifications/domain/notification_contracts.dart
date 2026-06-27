class LessonReminder {
  const LessonReminder({
    required this.id,
    required this.lessonScheduleId,
    required this.teacherUserId,
    required this.studentId,
    required this.title,
    required this.message,
    required this.scheduledLessonStartAtUtc,
    required this.remindAtUtc,
    required this.channel,
    required this.status,
    required this.createdOnUtc,
  });

  final String id;
  final String lessonScheduleId;
  final String teacherUserId;
  final String studentId;
  final String title;
  final String message;
  final DateTime scheduledLessonStartAtUtc;
  final DateTime remindAtUtc;
  final String channel;
  final String status;
  final DateTime createdOnUtc;

  bool get isPending => status == 'Pending';
  bool get isSent => status == 'Sent';
  bool get isCancelled => status == 'Cancelled';
}

abstract interface class NotificationRepository {
  Future<List<LessonReminder>> listReminders({
    required String teacherUserId,
    bool activeOnly = false,
  });
}
