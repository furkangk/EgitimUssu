import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';

/// `LessonResourceResponse` (M06-1) → domain nesnesi.
class LessonResourceModel extends LessonResource {
  const LessonResourceModel({
    required super.id,
    required super.teacherUserId,
    required super.title,
    required super.kind,
    required super.createdOnUtc,
    super.studentId,
    super.lessonSessionId,
    super.description,
    super.fileName,
    super.sizeBytes,
    super.url,
    super.downloadUrl,
  });

  factory LessonResourceModel.fromJson(Map<String, dynamic> json) {
    return LessonResourceModel(
      id: json['id'] as String,
      teacherUserId: json['teacherUserId'] as String,
      studentId: json['studentId'] as String?,
      lessonSessionId: json['lessonSessionId'] as String?,
      title: json['title'] as String? ?? '',
      description: json['description'] as String?,
      kind: json['kind'] as String? ?? 'Link',
      fileName: json['fileName'] as String?,
      sizeBytes: (json['sizeBytes'] as num?)?.toInt(),
      url: json['url'] as String?,
      downloadUrl: json['downloadUrl'] as String?,
      createdOnUtc: DateTime.parse(json['createdOnUtc'] as String),
    );
  }
}
