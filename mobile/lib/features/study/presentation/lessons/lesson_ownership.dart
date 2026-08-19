/// Ders sahipliği (kendi/öğretmen) filtre + gruplama saf yardımcıları.
///
/// Ç-06: backend `LessonSchedule.TeacherUserId` `null` → öğrencinin kendi dersi. Mobil
/// tarafta bu alan (`LessonScheduleModel.fromJson`) non-nullable `String` olarak modellenir
/// ve backend `null` yerine `''` boş dize sentinel'i döner (aynı sözleşim
/// `student_teacher_page.dart` ve `student_lesson_detail_page.dart`'ta da kullanılır,
/// `.isEmpty`/`.isNotEmpty` ile). Bu yüzden burada `String?`/`== null` değil, non-null
/// `String` parametre + boş-dize (trim) kontrolü kullanılır.
enum LessonFilter { all, own, teacher }

bool isOwnLesson(String teacherUserId) => teacherUserId.trim().isEmpty;

List<T> filterLessons<T>(
  List<T> lessons,
  LessonFilter filter,
  String Function(T) teacherOf,
) {
  switch (filter) {
    case LessonFilter.all:
      return List<T>.from(lessons);
    case LessonFilter.own:
      return lessons.where((l) => isOwnLesson(teacherOf(l))).toList();
    case LessonFilter.teacher:
      return lessons.where((l) => !isOwnLesson(teacherOf(l))).toList();
  }
}

({List<T> own, List<T> teacher}) partitionLessons<T>(
  List<T> lessons,
  String Function(T) teacherOf,
) {
  final own = <T>[];
  final teacher = <T>[];
  for (final l in lessons) {
    (isOwnLesson(teacherOf(l)) ? own : teacher).add(l);
  }
  return (own: own, teacher: teacher);
}
