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
}

abstract interface class StudentRepository {
  Future<StudentProfile> createStudent(StudentProfile studentProfile);
  Future<StudentProfile> getStudent(String studentId);
  Future<List<StudentProfile>> listByTeacher(String teacherUserId);
}
