class AssignmentItem {
  const AssignmentItem({
    this.id,
    required this.title,
    required this.description,
    this.studentId,
    this.dueDateUtc,
    this.attachmentUrl,
    this.status,
    this.createdOnUtc,
  });

  final String? id;
  final String title;
  final String description;
  final String? studentId;
  final DateTime? dueDateUtc;
  final String? attachmentUrl;
  final String? status;
  final DateTime? createdOnUtc;
}

class LessonNote {
  const LessonNote({
    required this.id,
    required this.lessonSessionId,
    required this.summary,
    this.coveredTopics,
    this.recommendations,
    required this.createdOnUtc,
  });

  final String id;
  final String lessonSessionId;
  final String summary;
  final String? coveredTopics;
  final String? recommendations;
  final DateTime createdOnUtc;
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
  Future<List<AssignmentItem>> listByTeacher(String teacherUserId);

  /// Öğrencinin kendi ödevleri (kendi StudentId'sine göre; sunucu sahiplik filtresini zorlar).
  Future<List<AssignmentItem>> listByStudent(String studentId);

  /// Öğrenci ödevi tamamlandı olarak işaretler.
  Future<AssignmentItem> completeAssignment(String assignmentId);

  /// Öğrenci ödev çözümünü (dosya) yükler; teslim sonrası ödev "devam ediyor"a geçer.
  Future<AssignmentItem> submitWork(String assignmentId, String filePath);
}
