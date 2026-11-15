import 'dart:async';

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/pages/scheduling_page.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:syncfusion_flutter_calendar/calendar.dart';

import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_notification_repository.dart';
import '../../helpers/fake_scheduling_repository.dart';

void main() {
  tearDown(injector.reset);
  testWidgets('API boşken takvim kaynağında hiçbir sahte etkinlik yok', (
    tester,
  ) async {
    injector.registerSingleton<SchedulingRepository>(
      FakeSchedulingRepository(),
    );
    injector.registerSingleton<StudentRepository>(_Students());
    final auth = AuthCubit(
      FakeAuthRepository(
        session: FakeAuthRepository.defaultSession(
          fullName: 'Gerçek Kullanıcı',
        ),
      ),
    );
    await auth.restoreSession();
    final notifications = NotificationsCubit(FakeNotificationRepository());
    await tester.pumpWidget(
      MultiBlocProvider(
        providers: [
          BlocProvider<AuthCubit>.value(value: auth),
          BlocProvider<NotificationsCubit>.value(value: notifications),
        ],
        child: const MaterialApp(
          locale: Locale('tr'),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: SchedulingPage(),
        ),
      ),
    );
    await tester.pumpAndSettle();
    final calendar = tester.widget<SfCalendar>(find.byType(SfCalendar));
    expect(calendar.dataSource!.appointments, isEmpty);
    expect(find.text('Ahmet Bey'), findsNothing);
    await tester.pumpWidget(const SizedBox());
    await auth.close();
    await notifications.close();
  });
  testWidgets('gecikmeli gerçek ders yanıtı etkileşimsiz takvimde görünür', (
    tester,
  ) async {
    final repository = _DelayedScheduling();
    injector.registerSingleton<SchedulingRepository>(repository);
    injector.registerSingleton<StudentRepository>(_Students());
    final auth = AuthCubit(
      FakeAuthRepository(
        session: FakeAuthRepository.defaultSession(
          fullName: 'Gerçek Kullanıcı',
        ),
      ),
    );
    await auth.restoreSession();
    final notifications = NotificationsCubit(FakeNotificationRepository());
    addTearDown(auth.close);
    addTearDown(notifications.close);
    await tester.pumpWidget(
      MultiBlocProvider(
        providers: [
          BlocProvider<AuthCubit>.value(value: auth),
          BlocProvider<NotificationsCubit>.value(value: notifications),
        ],
        child: const MaterialApp(
          locale: Locale('tr'),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: SchedulingPage(),
        ),
      ),
    );
    // İlk takvim yerleşimi ve onViewChanged callback'leri tamamlanır;
    // gerçek ders yanıtı bu sırada hâlâ beklemektedir.
    await tester.pumpAndSettle();
    expect(
      tester
          .widget<SfCalendar>(find.byType(SfCalendar))
          .dataSource!
          .appointments,
      isEmpty,
    );
    expect(find.text('Gerçek Matematik'), findsNothing);
    final visibleDate = tester
        .widget<SfCalendar>(find.byType(SfCalendar))
        .initialSelectedDate!;
    final start = DateTime(
      visibleDate.year,
      visibleDate.month,
      visibleDate.day,
      10,
    ).toUtc();
    final lesson = LessonSchedule(
      id: 'real-lesson',
      teacherUserId: 'test-user',
      studentId: 'real-student',
      subject: 'Gerçek Matematik',
      lessonFormat: 'Online',
      startAtUtc: start,
      endAtUtc: start.add(const Duration(hours: 1)),
      timeZone: 'Europe/Istanbul',
    );
    repository.response.complete([lesson]);
    // Kullanıcı dokunuşu, tarih değişimi veya pumpWidget ile dış rebuild yok.
    await tester.pumpAndSettle();
    final calendar = tester.widget<SfCalendar>(find.byType(SfCalendar));
    expect(calendar.dataSource!.appointments, hasLength(1));
    expect(calendar.dataSource!.getStartTime(0), lesson.startAtUtc.toLocal());
    expect(find.text('Gerçek Matematik'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });
}

class _Students implements StudentRepository {
  @override
  Future<List<StudentProfile>> listByTeacher(String teacherUserId) async => [];
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _DelayedScheduling extends FakeSchedulingRepository {
  final response = Completer<List<LessonSchedule>>();

  @override
  Future<List<LessonSchedule>> listTeacherLessons({
    required String teacherUserId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
  }) => response.future;
}
