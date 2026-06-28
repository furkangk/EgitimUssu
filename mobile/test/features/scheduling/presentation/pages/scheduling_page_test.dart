import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/pages/scheduling_page.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  setUp(() {
    injector
      ..registerFactory<SchedulingRepository>(_FakeSchedulingRepository.new)
      ..registerFactory<StudentRepository>(_FakeStudentRepository.new);
  });

  tearDown(injector.reset);

  testWidgets('renders calendar views with single add lesson FAB', (
    tester,
  ) async {
    await tester.pumpWidget(
      BlocProvider<NotificationsCubit>(
        create: (_) => NotificationsCubit(_FakeNotificationRepository()),
        child: const MaterialApp(home: SchedulingPage()),
      ),
    );

    expect(find.text('Takvim'), findsWidgets);
    expect(find.text('Aylik'), findsOneWidget);
    expect(find.text('Haftalik'), findsOneWidget);
    expect(find.text('Gunluk'), findsOneWidget);
    // Eski "etkinlik ekle" menu chrome'u yok; tek ekleme girisi "Ders Ekle" FAB.
    expect(find.text('Etkinlik ekle'), findsNothing);
    expect(find.text('Ders Ekle'), findsOneWidget);
  });
}

class _FakeSchedulingRepository implements SchedulingRepository {
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
    return const <LessonSchedule>[];
  }
}

class _FakeNotificationRepository implements NotificationRepository {
  @override
  Future<List<LessonReminder>> listReminders({
    required String teacherUserId,
    bool activeOnly = false,
  }) async {
    return const <LessonReminder>[];
  }
}

class _FakeStudentRepository implements StudentRepository {
  @override
  Future<StudentProfile> createStudent(StudentProfile studentProfile) {
    throw UnimplementedError();
  }

  @override
  Future<StudentProfile> updateStudent(StudentProfile studentProfile) {
    throw UnimplementedError();
  }

  @override
  Future<StudentProfile> getStudent(String studentId) {
    throw UnimplementedError();
  }

  @override
  Future<List<StudentProfile>> listByTeacher(String teacherUserId) async {
    return const <StudentProfile>[];
  }
}
