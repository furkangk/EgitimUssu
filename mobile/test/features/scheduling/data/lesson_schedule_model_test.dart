import 'package:egitim_ussu_mobile/features/scheduling/data/models/lesson_schedule_model.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('maps cancel payload and response status', () {
    final model = LessonScheduleModel.fromJson(<String, dynamic>{
      'id': 'lesson-id',
      'teacherUserId': 'teacher-id',
      'studentId': 'student-id',
      'subject': 'Matematik',
      'lessonFormat': 'Online',
      'startAtUtc': '2026-05-12T15:00:00Z',
      'endAtUtc': '2026-05-12T16:00:00Z',
      'timeZone': 'Europe/Istanbul',
      'status': 'Cancelled',
      'reminderOffsetMinutes': 60,
    });

    expect(model.status, 'Cancelled');
    expect(model.toCancelPayload('Hasta')['cancellationNote'], 'Hasta');
  });
}
