import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';

class LessonReminderModel extends LessonReminder {
  const LessonReminderModel({
    required super.id,
    required super.lessonScheduleId,
    required super.teacherUserId,
    required super.studentId,
    required super.title,
    required super.message,
    required super.scheduledLessonStartAtUtc,
    required super.remindAtUtc,
    required super.channel,
    required super.status,
    required super.createdOnUtc,
  });

  factory LessonReminderModel.fromJson(Map<String, dynamic> json) {
    return LessonReminderModel(
      id: json['id'] as String,
      lessonScheduleId: json['lessonScheduleId'] as String,
      teacherUserId: json['teacherUserId'] as String,
      studentId: json['studentId'] as String,
      title: json['title'] as String,
      message: json['message'] as String,
      scheduledLessonStartAtUtc: DateTime.parse(json['scheduledLessonStartAtUtc'] as String),
      remindAtUtc: DateTime.parse(json['remindAtUtc'] as String),
      channel: json['channel'] as String,
      status: json['status'] as String,
      createdOnUtc: DateTime.parse(json['createdOnUtc'] as String),
    );
  }
}
