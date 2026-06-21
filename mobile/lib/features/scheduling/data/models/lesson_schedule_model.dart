import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';

class LessonScheduleModel extends LessonSchedule {
  const LessonScheduleModel({
    required super.id,
    required super.teacherUserId,
    required super.studentId,
    required super.subject,
    required super.lessonFormat,
    required super.startAtUtc,
    required super.endAtUtc,
    required super.timeZone,
    super.status,
    super.recurrenceRule,
    super.reminderOffsetMinutes,
    super.locationLabel,
    super.notes,
  });

  factory LessonScheduleModel.fromJson(Map<String, dynamic> json) {
    return LessonScheduleModel(
      id: json['id']?.toString() ?? '',
      teacherUserId: json['teacherUserId']?.toString() ?? '',
      studentId: json['studentId']?.toString() ?? '',
      subject: json['subject']?.toString() ?? '',
      lessonFormat: json['lessonFormat']?.toString() ?? 'Online',
      startAtUtc: DateTime.parse(json['startAtUtc'].toString()).toUtc(),
      endAtUtc: DateTime.parse(json['endAtUtc'].toString()).toUtc(),
      timeZone: json['timeZone']?.toString() ?? 'Europe/Istanbul',
      status: json['status']?.toString() ?? 'Planned',
      recurrenceRule: json['recurrenceRule']?.toString(),
      reminderOffsetMinutes: json['reminderOffsetMinutes'] as int?,
      locationLabel: json['locationLabel']?.toString(),
      notes: json['notes']?.toString(),
    );
  }

  factory LessonScheduleModel.demo({
    required String teacherUserId,
    required String studentId,
    required String id,
    required String subject,
    required DateTime startAtUtc,
    required DateTime endAtUtc,
  }) {
    return LessonScheduleModel(
      id: id,
      teacherUserId: teacherUserId,
      studentId: studentId,
      subject: subject,
      lessonFormat: 'Online',
      startAtUtc: startAtUtc,
      endAtUtc: endAtUtc,
      timeZone: 'Europe/Istanbul',
      status: 'Planned',
      reminderOffsetMinutes: 60,
      locationLabel: 'Zoom',
      notes: 'Demo ders',
    );
  }

  Map<String, dynamic> toCreatePayload() {
    return <String, dynamic>{
      'teacherUserId': teacherUserId,
      'studentId': studentId,
      'subject': subject,
      'lessonFormat': lessonFormat == 'OnlineAndInPerson' ? 3 : 2,
      'startAtUtc': startAtUtc.toIso8601String(),
      'endAtUtc': endAtUtc.toIso8601String(),
      'timeZone': timeZone,
      'recurrenceRule': recurrenceRule,
      'reminderOffsetMinutes': reminderOffsetMinutes ?? 60,
      'locationLabel': locationLabel,
      'notes': notes,
    };
  }

  Map<String, dynamic> toCancelPayload(String? cancellationNote) {
    return <String, dynamic>{'cancellationNote': cancellationNote};
  }
}
