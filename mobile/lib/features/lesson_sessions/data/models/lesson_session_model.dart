import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';

class LessonSessionModel extends LessonSession {
  const LessonSessionModel({
    required super.id,
    required super.lessonScheduleId,
    required super.teacherUserId,
    required super.studentId,
    required super.subject,
    required super.status,
    required super.topicTitle,
    super.coveredContent,
    super.teacherNotes,
    super.actualStartAtUtc,
    super.actualEndAtUtc,
    super.plannedStartAtUtc,
    super.durationMinutes,
    super.attendanceStatus,
  });

  factory LessonSessionModel.fromJson(Map<String, dynamic> json) {
    return LessonSessionModel(
      id: json['id']?.toString() ?? '',
      lessonScheduleId: json['lessonScheduleId']?.toString(),
      teacherUserId: json['teacherUserId']?.toString() ?? '',
      studentId: json['studentId']?.toString() ?? '',
      subject: json['subject']?.toString() ?? '',
      status: json['status']?.toString() ?? 'Planned',
      topicTitle: json['topicTitle']?.toString(),
      coveredContent: json['coveredContent']?.toString(),
      teacherNotes: json['teacherNotes']?.toString(),
      actualStartAtUtc: _parseDate(json['actualStartAtUtc']),
      actualEndAtUtc: _parseDate(json['actualEndAtUtc']),
      plannedStartAtUtc: _parseDate(json['plannedStartAtUtc']),
      durationMinutes: json['durationMinutes'] as int?,
      attendanceStatus: json['attendanceStatus']?.toString(),
    );
  }

  factory LessonSessionModel.demo({
    required String id,
    required String? lessonScheduleId,
    required String teacherUserId,
    required String studentId,
    required String subject,
    required DateTime plannedStartAtUtc,
    String status = 'Planned',
  }) {
    return LessonSessionModel(
      id: id,
      lessonScheduleId: lessonScheduleId,
      teacherUserId: teacherUserId,
      studentId: studentId,
      subject: subject,
      status: status,
      topicTitle: '$subject tekrar dersi',
      plannedStartAtUtc: plannedStartAtUtc,
      attendanceStatus: 'Unknown',
    );
  }

  Map<String, dynamic> toCreatePayload() {
    return <String, dynamic>{
      'lessonScheduleId': _emptyToNull(lessonScheduleId),
      'teacherUserId': teacherUserId,
      'studentId': studentId,
      'subject': subject,
      'plannedStartAtUtc': (plannedStartAtUtc ?? DateTime.now().toUtc())
          .toIso8601String(),
      'topicTitle': topicTitle ?? subject,
    };
  }

  Map<String, dynamic> toCompletePayload() {
    return <String, dynamic>{
      'actualStartAtUtc': (actualStartAtUtc ?? DateTime.now().toUtc())
          .toIso8601String(),
      'actualEndAtUtc': (actualEndAtUtc ?? DateTime.now().toUtc())
          .toIso8601String(),
      'attendanceStatus': _attendanceStatusToApi(attendanceStatus),
      'topicTitle': topicTitle ?? subject,
      'coveredContent': coveredContent,
      'teacherNotes': teacherNotes,
    };
  }

  Map<String, dynamic> toJson() {
    return <String, dynamic>{
      'id': id,
      'lessonScheduleId': lessonScheduleId,
      'teacherUserId': teacherUserId,
      'studentId': studentId,
      'subject': subject,
      'status': status,
      'topicTitle': topicTitle,
      'coveredContent': coveredContent,
      'teacherNotes': teacherNotes,
      'actualStartAtUtc': actualStartAtUtc?.toIso8601String(),
      'actualEndAtUtc': actualEndAtUtc?.toIso8601String(),
      'plannedStartAtUtc': plannedStartAtUtc?.toIso8601String(),
      'durationMinutes': durationMinutes,
      'attendanceStatus': attendanceStatus,
    };
  }

  static DateTime? _parseDate(Object? value) {
    if (value == null) {
      return null;
    }
    return DateTime.tryParse(value.toString())?.toUtc();
  }

  static Object? _emptyToNull(String? value) {
    if (value == null || value.trim().isEmpty) {
      return null;
    }
    return value;
  }

  static int _attendanceStatusToApi(String? value) {
    return switch (value) {
      'Present' => 1,
      'Absent' => 2,
      'Late' => 3,
      _ => 0,
    };
  }
}
