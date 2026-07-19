import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';

/// `StudyScheduleEntry` JSON serileştirme yardımcıları. Backend `scheduling.study_schedule_entries`
/// ve `GET /students/{id}/calendar` yanıtlarını domain nesnelerine çevirir.
class StudyScheduleEntryModel extends StudyScheduleEntry {
  const StudyScheduleEntryModel({
    required super.id,
    required super.studentId,
    required super.subject,
    required super.startAtUtc,
    required super.endAtUtc,
    required super.timeZone,
    super.topic,
    super.recurrenceRule,
    super.reminderOffsetMinutes,
    super.colorHex,
    super.notes,
    super.status,
  });

  factory StudyScheduleEntryModel.fromJson(Map<String, dynamic> json) {
    return StudyScheduleEntryModel(
      id: json['id']?.toString() ?? '',
      studentId: json['studentId']?.toString() ?? '',
      subject: json['subject']?.toString() ?? '',
      topic: json['topic']?.toString(),
      startAtUtc: DateTime.parse(json['startAtUtc'].toString()).toUtc(),
      endAtUtc: DateTime.parse(json['endAtUtc'].toString()).toUtc(),
      timeZone: json['timeZone']?.toString() ?? 'Europe/Istanbul',
      recurrenceRule: json['recurrenceRule']?.toString(),
      reminderOffsetMinutes: json['reminderOffsetMinutes'] as int?,
      colorHex: json['colorHex']?.toString(),
      notes: json['notes']?.toString(),
      status: json['status']?.toString() ?? 'Active',
    );
  }

  static Map<String, dynamic> toPayload(StudyScheduleEntry entry) {
    return <String, dynamic>{
      'subject': entry.subject,
      'topic': entry.topic,
      'startAtUtc': entry.startAtUtc.toIso8601String(),
      'endAtUtc': entry.endAtUtc.toIso8601String(),
      'timeZone': entry.timeZone,
      'recurrenceRule': entry.recurrenceRule,
      'reminderOffsetMinutes': entry.reminderOffsetMinutes ?? 0,
      'colorHex': entry.colorHex,
      'notes': entry.notes,
    };
  }
}

/// `CalendarOccurrence` JSON serileştirme yardımcısı (birleşik takvim yanıtı).
class CalendarOccurrenceModel extends CalendarOccurrence {
  const CalendarOccurrenceModel({
    required super.entryId,
    required super.source,
    required super.subject,
    required super.startAtUtc,
    required super.endAtUtc,
    required super.isEditable,
    super.topic,
    super.lessonFormat,
    super.locationLabel,
    super.colorHex,
    super.recurrenceRule,
    super.notes,
    super.completed,
  });

  factory CalendarOccurrenceModel.fromJson(Map<String, dynamic> json) {
    return CalendarOccurrenceModel(
      entryId: json['entryId']?.toString() ?? '',
      source: json['source']?.toString() ?? 'Self',
      subject: json['subject']?.toString() ?? '',
      topic: json['topic']?.toString(),
      startAtUtc: DateTime.parse(json['startAtUtc'].toString()).toUtc(),
      endAtUtc: DateTime.parse(json['endAtUtc'].toString()).toUtc(),
      lessonFormat: json['lessonFormat']?.toString(),
      locationLabel: json['locationLabel']?.toString(),
      colorHex: json['colorHex']?.toString(),
      recurrenceRule: json['recurrenceRule']?.toString(),
      notes: json['notes']?.toString(),
      isEditable: json['isEditable'] as bool? ?? false,
      completed: json['completed'] as bool? ?? false,
    );
  }
}
