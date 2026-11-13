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
    this.teacherFeedback,
    this.score,
    this.gradedOnUtc,
  });

  final String? id;
  final String title;
  final String description;
  final String? studentId;
  final DateTime? dueDateUtc;
  final String? attachmentUrl;
  final String? status;
  final DateTime? createdOnUtc;

  /// Öğretmenin onay/geri gönderme sırasında bıraktığı geri bildirim.
  final String? teacherFeedback;

  /// M06-4: Öğretmenin verdiği puan (0–100); puanlanmadıysa `null`.
  final int? score;

  /// M06-4: Puanın verildiği an.
  final DateTime? gradedOnUtc;

  bool get isGraded => score != null;
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
    this.visibility = 'Private',
  });

  final String lessonSessionId;
  final String summary;
  final String coveredTopics;
  final String recommendations;
  final List<AssignmentItem> assignments;
  final String visibility;
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

  /// Öğretmen ödevi onaylar; isteğe bağlı geri bildirim bırakır.
  Future<AssignmentItem> approveAssignment(
    String assignmentId,
    String? feedback,
  );

  /// M06-4: Öğretmen onayladığı ödeve 0–100 arası puan verir.
  Future<AssignmentItem> gradeAssignment(
    String assignmentId,
    int score,
    String? feedback,
  );

  // ---- M06-1 (P04): ders kaynağı paylaşımı ----

  /// Öğretmen kaynak ekler: [filePath] doluysa dosya, [url] doluysa bağlantı kaynağı.
  Future<LessonResource> createLessonResource({
    required String title,
    String? description,
    String? url,
    String? filePath,
    String? studentId,
    String? lessonSessionId,
  });

  /// Kaynakları listeler. Öğretmen `teacherUserId`, öğrenci `studentId` verir (sunucu sahipliği zorlar).
  Future<List<LessonResource>> listLessonResources({
    String? teacherUserId,
    String? studentId,
    String? lessonSessionId,
  });

  /// Kaynağı arşivler (yumuşak silme); yalnız kaynağın öğretmeni.
  Future<void> archiveLessonResource(String resourceId);
}

/// M06-1: Öğretmenin paylaştığı ders materyali (dosya ya da bağlantı).
class LessonResource {
  const LessonResource({
    required this.id,
    required this.teacherUserId,
    required this.title,
    required this.kind,
    required this.createdOnUtc,
    this.studentId,
    this.lessonSessionId,
    this.description,
    this.fileName,
    this.sizeBytes,
    this.url,
    this.downloadUrl,
  });

  final String id;
  final String teacherUserId;

  /// `null` ise öğretmenin tüm öğrencilerine açık.
  final String? studentId;
  final String? lessonSessionId;
  final String title;
  final String? description;

  /// `File` veya `Link`.
  final String kind;
  final String? fileName;
  final int? sizeBytes;

  /// Bağlantı kaynağının dış adresi.
  final String? url;

  /// Dosya kaynağının yetkili indirme rotası.
  final String? downloadUrl;
  final DateTime createdOnUtc;

  bool get isFile => kind == 'File';
}
