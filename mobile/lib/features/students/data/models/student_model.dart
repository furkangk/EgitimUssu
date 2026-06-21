import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';

class StudentSubjectTargetModel extends StudentSubjectTarget {
  const StudentSubjectTargetModel({
    required super.subject,
    required super.targetLevel,
  });

  factory StudentSubjectTargetModel.fromJson(Map<String, dynamic> json) {
    return StudentSubjectTargetModel(
      subject: json['subject']?.toString() ?? '',
      targetLevel: json['targetLevel']?.toString() ?? '',
    );
  }

  Map<String, dynamic> toJson() {
    return <String, dynamic>{'subject': subject, 'targetLevel': targetLevel};
  }
}

class StudentProfileModel extends StudentProfile {
  const StudentProfileModel({
    required super.id,
    required super.fullName,
    required super.gradeLevel,
    required super.origin,
    required super.subjects,
    super.teacherUserId,
    super.contactEmail,
    super.contactPhone,
    super.goalSummary,
    super.levelNotes,
  });

  factory StudentProfileModel.fromJson(Map<String, dynamic> json) {
    return StudentProfileModel(
      id: json['id']?.toString() ?? '',
      fullName: json['fullName']?.toString() ?? '',
      gradeLevel: json['gradeLevel']?.toString() ?? '',
      origin: json['origin']?.toString() ?? 'TeacherManaged',
      teacherUserId: json['createdByTeacherUserId']?.toString(),
      contactEmail: json['contactEmail']?.toString(),
      contactPhone: json['contactPhone']?.toString(),
      goalSummary: json['goalSummary']?.toString(),
      levelNotes: json['levelNotes']?.toString(),
      subjects: ((json['subjects'] as List<dynamic>? ?? <dynamic>[]))
          .whereType<Map<String, dynamic>>()
          .map(StudentSubjectTargetModel.fromJson)
          .toList(),
    );
  }

  factory StudentProfileModel.fromSummaryJson(Map<String, dynamic> json) {
    return StudentProfileModel(
      id: json['id']?.toString() ?? '',
      fullName: json['fullName']?.toString() ?? '',
      gradeLevel: json['gradeLevel']?.toString() ?? '',
      origin: json['origin']?.toString() ?? 'TeacherManaged',
      subjects: const <StudentSubjectTargetModel>[],
    );
  }

  factory StudentProfileModel.demo({
    required String teacherUserId,
    required String id,
    required String fullName,
    required String gradeLevel,
  }) {
    return StudentProfileModel(
      id: id,
      fullName: fullName,
      gradeLevel: gradeLevel,
      origin: 'TeacherManaged',
      teacherUserId: teacherUserId,
      contactEmail: 'veli@example.com',
      contactPhone: '5554443322',
      goalSummary: 'LGS hazirligi',
      levelNotes: 'Temel guclendirilecek',
      subjects: const <StudentSubjectTargetModel>[
        StudentSubjectTargetModel(
          subject: 'Matematik',
          targetLevel: 'LGS 2026',
        ),
      ],
    );
  }

  Map<String, dynamic> toCreatePayload() {
    return <String, dynamic>{
      'userId': null,
      'createdByTeacherUserId': teacherUserId,
      'parentUserId': null,
      'fullName': fullName,
      'gradeLevel': gradeLevel,
      'contactEmail': contactEmail,
      'contactPhone': contactPhone,
      'goalSummary': goalSummary,
      'levelNotes': levelNotes,
      'origin': 1,
      'subjects': subjects
          .map(
            (subject) => StudentSubjectTargetModel(
              subject: subject.subject,
              targetLevel: subject.targetLevel,
            ).toJson(),
          )
          .toList(),
    };
  }
}
