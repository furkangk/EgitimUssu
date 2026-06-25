import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';

class AssignmentItemModel extends AssignmentItem {
  const AssignmentItemModel({
    super.id,
    required super.title,
    required super.description,
    super.studentId,
    super.dueDateUtc,
    super.attachmentUrl,
    super.status,
    super.createdOnUtc,
  });

  factory AssignmentItemModel.fromJson(Map<String, dynamic> json) {
    return AssignmentItemModel(
      id: json['id']?.toString(),
      title: json['title']?.toString() ?? '',
      description: json['description']?.toString() ?? '',
      studentId: json['studentId']?.toString(),
      dueDateUtc: _parseDate(json['dueDateUtc']),
      attachmentUrl: json['attachmentUrl']?.toString(),
      status: json['status']?.toString(),
      createdOnUtc: _parseDate(json['createdOnUtc']),
    );
  }

  Map<String, dynamic> toCreatePayload() {
    return <String, dynamic>{
      'title': title,
      'description': description,
      'dueDateUtc': dueDateUtc?.toIso8601String(),
      'attachmentUrl': attachmentUrl,
    };
  }

  static List<AssignmentItemModel> demoList() {
    final now = DateTime.now().toUtc();
    return <AssignmentItemModel>[
      AssignmentItemModel(
        id: 'demo-1',
        title: 'Polinomlar Alıştırmaları',
        description: '42–45. sayfa 24 soru çöz.',
        dueDateUtc: now.add(const Duration(days: 3)),
        status: 'Pending',
        createdOnUtc: now.subtract(const Duration(days: 1)),
      ),
      AssignmentItemModel(
        id: 'demo-2',
        title: 'Kuvvet ve Hareket Tekrar',
        description: 'Hız–zaman grafiği soruları.',
        dueDateUtc: now.add(const Duration(days: 5)),
        status: 'InProgress',
        createdOnUtc: now.subtract(const Duration(days: 2)),
      ),
      AssignmentItemModel(
        id: 'demo-3',
        title: 'Trigonometri Sınavı Hazırlık',
        description: '30 soruluk test çöz.',
        dueDateUtc: now.subtract(const Duration(days: 1)),
        status: 'Completed',
        createdOnUtc: now.subtract(const Duration(days: 7)),
      ),
    ];
  }

  static DateTime? _parseDate(Object? value) {
    if (value == null) return null;
    return DateTime.tryParse(value.toString())?.toUtc();
  }
}

class LessonNoteModel extends LessonNote {
  const LessonNoteModel({
    required super.id,
    required super.lessonSessionId,
    required super.summary,
    super.coveredTopics,
    super.recommendations,
    required super.createdOnUtc,
  });

  factory LessonNoteModel.fromJson(Map<String, dynamic> json) {
    return LessonNoteModel(
      id: json['id']?.toString() ?? '',
      lessonSessionId: json['lessonSessionId']?.toString() ?? '',
      summary: json['summary']?.toString() ?? '',
      coveredTopics: json['coveredTopics']?.toString(),
      recommendations: json['recommendations']?.toString(),
      createdOnUtc: _parseDate(json['createdOnUtc']) ?? DateTime.now().toUtc(),
    );
  }

  static DateTime? _parseDate(Object? value) {
    if (value == null) return null;
    return DateTime.tryParse(value.toString())?.toUtc();
  }
}

class FollowUpAssignmentModel extends FollowUpAssignment {
  const FollowUpAssignmentModel({
    required super.lessonSessionId,
    required super.summary,
    required super.coveredTopics,
    required super.recommendations,
    required super.assignments,
  });

  factory FollowUpAssignmentModel.fromJson(Map<String, dynamic> json) {
    final note = json['note'] as Map<String, dynamic>?;
    return FollowUpAssignmentModel(
      lessonSessionId: json['lessonSessionId']?.toString() ?? '',
      summary:
          note?['summary']?.toString() ?? json['summary']?.toString() ?? '',
      coveredTopics:
          note?['coveredTopics']?.toString() ??
          json['coveredTopics']?.toString() ??
          '',
      recommendations:
          note?['recommendations']?.toString() ??
          json['recommendations']?.toString() ??
          '',
      assignments: ((json['assignments'] as List<dynamic>? ?? <dynamic>[]))
          .whereType<Map<String, dynamic>>()
          .map(AssignmentItemModel.fromJson)
          .toList(),
    );
  }

  factory FollowUpAssignmentModel.demo(String lessonSessionId) {
    return FollowUpAssignmentModel(
      lessonSessionId: lessonSessionId,
      summary: 'Ders tamamlandi, tekrar plani hazir.',
      coveredTopics: 'Konu tekrari ve ornek soru cozumu',
      recommendations: 'Bir sonraki derse kadar temel sorular tekrar edilecek.',
      assignments: const <AssignmentItemModel>[
        AssignmentItemModel(
          title: '20 soru tekrar',
          description: 'Derste islenen konu icin 20 soru coz.',
          status: 'Pending',
        ),
      ],
    );
  }

  Map<String, dynamic> toCreatePayload() {
    return <String, dynamic>{
      'summary': summary,
      'coveredTopics': coveredTopics,
      'recommendations': recommendations,
      'assignments': assignments
          .map(
            (a) => AssignmentItemModel(
              title: a.title,
              description: a.description,
              dueDateUtc: a.dueDateUtc,
              attachmentUrl: a.attachmentUrl,
            ).toCreatePayload(),
          )
          .toList(),
    };
  }
}
