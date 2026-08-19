/// Ders Detayı ekleme/düzenleme yetkileri. Öğretmen dersinde (isOwn=false)
/// öğretmenin ödev/konusu salt görüntüleme; öğrenci yalnız kendi not/testini ekler.
class LessonDetailPermissions {
  const LessonDetailPermissions({
    required this.canAddHomework,
    required this.canAddTopic,
    required this.canAddNote,
    required this.canAddTest,
  });

  final bool canAddHomework;
  final bool canAddTopic;
  final bool canAddNote;
  final bool canAddTest;

  factory LessonDetailPermissions.forOwnership(bool isOwn) =>
      LessonDetailPermissions(
        canAddHomework: isOwn,
        canAddTopic: isOwn,
        canAddNote: true,
        canAddTest: true,
      );
}
