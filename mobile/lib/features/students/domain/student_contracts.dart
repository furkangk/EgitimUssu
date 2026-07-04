class StudentSubjectTarget {
  const StudentSubjectTarget({
    required this.subject,
    required this.targetLevel,
  });

  final String subject;
  final String targetLevel;
}

class StudentProfile {
  const StudentProfile({
    required this.id,
    required this.fullName,
    required this.gradeLevel,
    required this.origin,
    required this.subjects,
    this.teacherUserId,
    this.contactEmail,
    this.contactPhone,
    this.goalSummary,
    this.levelNotes,
    this.isActive = true,
  });

  final String id;
  final String fullName;
  final String gradeLevel;
  final String origin;
  final String? teacherUserId;
  final String? contactEmail;
  final String? contactPhone;
  final String? goalSummary;
  final String? levelNotes;
  final List<StudentSubjectTarget> subjects;
  final bool isActive;
}

abstract interface class StudentRepository {
  Future<StudentProfile> createStudent(StudentProfile studentProfile);
  Future<StudentProfile> updateStudent(StudentProfile studentProfile);
  Future<StudentProfile> getStudent(String studentId);
  Future<List<StudentProfile>> listByTeacher(String teacherUserId);

  /// Oturum açan kullanıcıya bağlı öğrenci profilini getirir; yoksa `null`.
  Future<StudentProfile?> getByUser(String userId);

  /// Öğrencinin kendi kaydı (SelfRegistered) — profili yoksa oluşturur.
  Future<StudentProfile> createSelfProfile({
    required String userId,
    required String fullName,
    required String gradeLevel,
  });
}
