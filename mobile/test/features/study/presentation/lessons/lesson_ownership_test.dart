// Ç-06 kod gerçeği adaptasyonu: `LessonSchedule.teacherUserId` mobilde non-nullable
// `String` — backend `null` yerine `''` boş dize sentinel'i döner (bkz.
// `lesson_schedule_model.dart`, `student_teacher_page.dart`, `student_lesson_detail_page.dart`
// `.isEmpty` deseni). Bu yüzden imzalar brief'teki `String?`/`== null` yerine non-null
// `String` + boş-dize kontrolü kullanır.
import 'package:egitim_ussu_mobile/features/study/presentation/lessons/lesson_ownership.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  final lessons = <(String, String)>[
    ('a', ''), // kendi
    ('b', 't1'), // öğretmen
    ('c', ''), // kendi
  ];
  String teacherOf((String, String) l) => l.$2;

  test('isOwnLesson teacherUserId boşsa kendi', () {
    expect(isOwnLesson(''), isTrue);
    expect(isOwnLesson('t1'), isFalse);
    // Kenar durum: boşluklardan oluşan dize de "kendi" sayılır (trim).
    expect(isOwnLesson('   '), isTrue);
  });

  test('filterLessons her filtreyi uygular', () {
    expect(filterLessons(lessons, LessonFilter.all, teacherOf).length, 3);
    expect(filterLessons(lessons, LessonFilter.own, teacherOf).length, 2);
    expect(filterLessons(lessons, LessonFilter.teacher, teacherOf).length, 1);
  });

  test('partitionLessons kendi/öğretmen ayırır', () {
    final p = partitionLessons(lessons, teacherOf);
    expect(p.own.length, 2);
    expect(p.teacher.length, 1);
    expect(p.own.map((l) => l.$1), containsAll(<String>['a', 'c']));
    expect(p.teacher.map((l) => l.$1), containsAll(<String>['b']));
  });
}
