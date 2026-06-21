import 'package:egitim_ussu_mobile/features/lesson_sessions/data/models/lesson_session_model.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('maps create and complete payloads to backend contract', () {
    final plannedStart = DateTime.utc(2026, 5, 12, 15);
    final actualStart = DateTime.utc(2026, 5, 12, 15, 5);
    final actualEnd = DateTime.utc(2026, 5, 12, 16, 5);
    final model = LessonSessionModel(
      id: 'session-id',
      lessonScheduleId: 'lesson-id',
      teacherUserId: 'teacher-id',
      studentId: 'student-id',
      subject: 'Matematik',
      status: 'Planned',
      plannedStartAtUtc: plannedStart,
      actualStartAtUtc: actualStart,
      actualEndAtUtc: actualEnd,
      attendanceStatus: 'Late',
      topicTitle: 'Oran oranti',
      coveredContent: 'Temel tekrar',
      teacherNotes: 'Odev verildi',
    );

    expect(model.toCreatePayload(), <String, dynamic>{
      'lessonScheduleId': 'lesson-id',
      'teacherUserId': 'teacher-id',
      'studentId': 'student-id',
      'subject': 'Matematik',
      'plannedStartAtUtc': plannedStart.toIso8601String(),
      'topicTitle': 'Oran oranti',
    });
    expect(model.toCompletePayload()['attendanceStatus'], 3);
    expect(model.toCompletePayload()['coveredContent'], 'Temel tekrar');
  });
}
