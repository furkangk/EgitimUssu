import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';

/// Takvim/ders testlerinin ortak sahtesi. Gercek [SchedulingRepository] imzasini birebir
/// uygular; imza degisirse yalniz burasi guncellenir.
///
/// Okuma metotlari yapicidan verilen listeleri doner (varsayilan bos), yazma metotlari
/// cagrilirsa [UnimplementedError] firlatir — testin beklemedigi bir yazma sessizce gecmez.
class FakeSchedulingRepository implements SchedulingRepository {
  FakeSchedulingRepository({
    this.teacherLessons = const <LessonSchedule>[],
    this.studentLessons = const <LessonSchedule>[],
    this.studentCalendar = const <CalendarOccurrence>[],
  });

  final List<LessonSchedule> teacherLessons;
  final List<LessonSchedule> studentLessons;
  final List<CalendarOccurrence> studentCalendar;

  @override
  Future<LessonSchedule> createLesson(LessonSchedule lessonSchedule) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> updateLesson(LessonSchedule lessonSchedule) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> getLesson(String lessonId) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> cancelLesson({
    required String lessonId,
    String? cancellationNote,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> completeLesson({required String lessonId}) {
    throw UnimplementedError();
  }

  @override
  Future<List<LessonSchedule>> listTeacherLessons({
    required String teacherUserId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
  }) async {
    return teacherLessons;
  }

  @override
  Future<List<LessonSchedule>> listStudentLessons({
    required String studentId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
  }) async {
    return studentLessons;
  }

  @override
  Future<List<CalendarOccurrence>> getStudentCalendar({
    required String studentId,
    required DateTime startAtUtc,
    required DateTime endAtUtc,
  }) async {
    return studentCalendar;
  }

  @override
  Future<StudyScheduleEntry> createStudyEntry({
    required String studentId,
    required StudyScheduleEntry entry,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<StudyScheduleEntry> updateStudyEntry(StudyScheduleEntry entry) {
    throw UnimplementedError();
  }

  @override
  Future<void> deleteStudyEntry(String entryId) {
    throw UnimplementedError();
  }
}
