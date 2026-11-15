import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';

/// K-21: Her iki ekran da yalnız seçilen oluşumun oturumunu tamamlar.
Future<LessonSession> completeScheduledSession(
  LessonSessionRepository repository,
  LessonSchedule lesson,
) async {
  final session = await repository.createSessionFromSchedule(
    lesson.id,
    occurrenceStartAtUtc: lesson.occurrenceKeyUtc,
  );
  if (session.status == 'Completed') return session;
  return repository.completeSession(
    LessonSession(
      id: session.id,
      lessonScheduleId: session.lessonScheduleId,
      teacherUserId: session.teacherUserId,
      studentId: session.studentId,
      subject: session.subject,
      status: session.status,
      plannedStartAtUtc: session.plannedStartAtUtc,
      actualStartAtUtc: session.actualStartAtUtc ?? lesson.startAtUtc,
      actualEndAtUtc: session.actualEndAtUtc ?? lesson.endAtUtc,
      topicTitle: session.topicTitle,
      coveredContent: session.coveredContent,
      teacherNotes: session.teacherNotes,
      attendanceStatus:
          session.attendanceStatus == null ||
              session.attendanceStatus == 'Unknown'
          ? 'Attended'
          : session.attendanceStatus,
    ),
  );
}
