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
    super.meetingUrl,
    super.notes,
    super.occurrenceStartAtUtc,
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
      meetingUrl: json['meetingUrl']?.toString(),
      notes: json['notes']?.toString(),
      occurrenceStartAtUtc: _parseUtc(json['occurrenceStartAtUtc']),
    );
  }

  factory LessonScheduleModel.demo({
    required String teacherUserId,
    required String studentId,
    required String id,
    required String subject,
    required DateTime startAtUtc,
    required DateTime endAtUtc,
    String? meetingUrl,
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
      meetingUrl: meetingUrl ?? 'https://zoom.us/j/$id',
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
      'meetingUrl': meetingUrl,
      'notes': notes,
    };
  }

  Map<String, dynamic> toUpdatePayload() {
    return <String, dynamic>{
      'subject': subject,
      'lessonFormat': _formatToInt(lessonFormat),
      'startAtUtc': startAtUtc.toIso8601String(),
      'endAtUtc': endAtUtc.toIso8601String(),
      'timeZone': timeZone,
      'recurrenceRule': recurrenceRule,
      'reminderOffsetMinutes': reminderOffsetMinutes ?? 60,
      'locationLabel': locationLabel,
      'notes': notes,
    };
  }

  /// P17 T1 / N-01: tekrarlı derste kapsam + oluşumun **orijinal** anahtarı gönderilir
  /// (ertelenmiş oluşumda görünen saat değil — master §4). Eskiden yük yalnız notu
  /// taşıyordu ve sunucu bunu "tüm seri" sayıyordu. Tekil derste kapsam gönderilmez.
  static Map<String, dynamic> cancelPayload({
    required LessonSchedule lesson,
    required OccurrenceScope scope,
    LessonCancellationReason reason = LessonCancellationReason.teacherCancelled,
    bool isChargeable = true,
    String? cancellationNote,
  }) {
    return <String, dynamic>{
      'reason': reason.apiValue,
      'isChargeable': isChargeable,
      'cancellationNote': cancellationNote,
      if (lesson.isRecurring) ...<String, dynamic>{
        'scope': scope.apiValue,
        if (scope != OccurrenceScope.all)
          'occurrenceStartAtUtc': lesson.occurrenceKeyUtc
              .toUtc()
              .toIso8601String(),
      },
    };
  }

  static DateTime? _parseUtc(Object? value) {
    if (value == null) return null;
    return DateTime.tryParse(value.toString())?.toUtc();
  }

  static int _formatToInt(String format) {
    switch (format) {
      case 'InPerson':
        return 1;
      case 'Hybrid':
      case 'OnlineAndInPerson':
        return 3;
      default:
        return 2; // Online
    }
  }
}
