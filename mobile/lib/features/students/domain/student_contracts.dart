class StudentSubjectTarget {
  const StudentSubjectTarget({
    required this.subject,
    required this.targetLevel,
    this.id = '',
  });

  /// Sunucudaki branş kimliği (M03-2). Silme ucu **kimlikle** çalışır; henüz
  /// kaydedilmemiş (yerel) branşlarda boştur.
  final String id;
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

  StudentProfile copyWith({
    List<StudentSubjectTarget>? subjects,
    bool? isActive,
  }) {
    return StudentProfile(
      id: id,
      fullName: fullName,
      gradeLevel: gradeLevel,
      origin: origin,
      subjects: subjects ?? this.subjects,
      teacherUserId: teacherUserId,
      contactEmail: contactEmail,
      contactPhone: contactPhone,
      goalSummary: goalSummary,
      levelNotes: levelNotes,
      isActive: isActive ?? this.isActive,
    );
  }
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

  /// M03-2: Profili baştan güncellemeden tek branş ekler; sunucunun verdiği kimlikle döner.
  Future<StudentSubjectTarget> addSubject({
    required String studentId,
    required String subject,
    String? targetLevel,
  });

  /// M03-2: Tek branşı kimliğiyle siler.
  Future<void> removeSubject({
    required String studentId,
    required String subjectId,
  });

  /// M03-1: Kayıtlı bir veli kullanıcısını öğrenciye bağlama talebi açar.
  /// Bağ **onay bekler** (`Pending`); profil velisi ancak onaydan sonra dolar.
  Future<void> linkParent({
    required String studentId,
    required String parentUserId,
  });
}
