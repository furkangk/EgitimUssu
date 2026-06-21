class AssignmentItem {
  const AssignmentItem({
    required this.title,
    required this.description,
    this.dueDateUtc,
    this.attachmentUrl,
    this.status,
  });

  final String title;
  final String description;
  final DateTime? dueDateUtc;
  final String? attachmentUrl;
  final String? status;
}

class FollowUpAssignment {
  const FollowUpAssignment({
    required this.lessonSessionId,
    required this.summary,
    required this.coveredTopics,
    required this.recommendations,
    required this.assignments,
  });

  final String lessonSessionId;
  final String summary;
  final String coveredTopics;
  final String recommendations;
  final List<AssignmentItem> assignments;
}

abstract interface class AssignmentRepository {
  Future<FollowUpAssignment> getFollowUp(String lessonSessionId);
  Future<FollowUpAssignment> saveFollowUp(
    FollowUpAssignment followUpAssignment,
  );
}
