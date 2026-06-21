import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';

class AssignmentItemModel extends AssignmentItem {
  const AssignmentItemModel({
    required super.title,
    required super.description,
    super.dueDateUtc,
    super.attachmentUrl,
    super.status,
  });

  factory AssignmentItemModel.fromJson(Map<String, dynamic> json) {
    return AssignmentItemModel(
      title: json['title']?.toString() ?? '',
      description: json['description']?.toString() ?? '',
      dueDateUtc: _parseDate(json['dueDateUtc']),
      attachmentUrl: json['attachmentUrl']?.toString(),
      status: json['status']?.toString(),
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

  static DateTime? _parseDate(Object? value) {
    if (value == null) {
      return null;
    }
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
            (assignment) => AssignmentItemModel(
              title: assignment.title,
              description: assignment.description,
              dueDateUtc: assignment.dueDateUtc,
              attachmentUrl: assignment.attachmentUrl,
            ).toCreatePayload(),
          )
          .toList(),
    };
  }
}
