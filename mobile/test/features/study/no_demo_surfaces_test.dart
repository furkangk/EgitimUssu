import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_goals_overview_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_home_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_profile_page.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_tests_page.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_notification_repository.dart';
import '../../helpers/fake_scheduling_repository.dart';

void main() {
  tearDown(injector.reset);
  Future<void> pump(
    WidgetTester tester,
    Widget page, {
    _Study? study,
    _Assignments? assignments,
    Locale locale = const Locale('tr'),
  }) async {
    tester.view.physicalSize = const Size(1000, 8000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    injector.registerSingleton<StudentRepository>(_Students());
    injector.registerSingleton<StudyRepository>(study ?? _Study());
    injector.registerSingleton<SchedulingRepository>(
      FakeSchedulingRepository(),
    );
    injector.registerSingleton<AssignmentRepository>(
      assignments ?? _Assignments(),
    );
    final auth = AuthCubit(
      FakeAuthRepository(
        session: FakeAuthRepository.defaultSession(roles: ['Student']),
      ),
    );
    await auth.restoreSession();
    final notifications = NotificationsCubit(FakeNotificationRepository());
    addTearDown(notifications.close);
    await tester.pumpWidget(
      MultiBlocProvider(
        providers: [
          BlocProvider<AuthCubit>.value(value: auth),
          BlocProvider<NotificationsCubit>.value(value: notifications),
        ],
        child: MaterialApp(
          locale: locale,
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: page,
        ),
      ),
    );
    await tester.pumpAndSettle();
    addTearDown(auth.close);
  }

  testWidgets('İngilizce profilde rozet sayısı Türkçe kalmaz', (tester) async {
    await pump(tester, const StudentProfilePage(), locale: const Locale('en'));
    expect(find.text('0 / 0 badges earned'), findsOneWidget);
    expect(find.text('0 / 0 rozet kazanıldı'), findsNothing);
  });
  testWidgets('İngilizce performansta ay seçimi Türkçe kalmaz', (tester) async {
    await pump(tester, const StudentTestsPage(), locale: const Locale('en'));
    expect(find.text('Month'), findsOneWidget);
    expect(find.text('Ay'), findsNothing);
  });
  testWidgets('ana sayfa yalnız gerçek çalışma sayılarını gösterir', (
    tester,
  ) async {
    await pump(tester, const StudentHomePage());
    expect(find.text('Ödev (hafta)'), findsNothing);
    expect(find.text('Ders (hafta)'), findsNothing);
    expect(find.text('Demo'), findsNothing);
    expect(find.text('7'), findsOneWidget);
    expect(find.text('Çalışmaya Başla'), findsOneWidget);
  });
  testWidgets('ödev API hatası gerçek hata ve yeniden deneme sunar', (
    tester,
  ) async {
    final assignments = _Assignments(fail: true);
    await pump(tester, const StudentHomePage(), assignments: assignments);
    expect(find.text('Demo'), findsNothing);
    expect(find.text('Ödevler alınamadı'), findsOneWidget);
    expect(find.text('Tekrar dene'), findsWidgets);
    assignments.fail = false;
    await tester.tap(find.text('Tekrar dene').first);
    await tester.pumpAndSettle();
    expect(find.text('Yaklaşan ödev yok'), findsOneWidget);
  });
  testWidgets(
    'hedef belirlenmeden performans tahmini hedef ya da konu puanı göstermez',
    (tester) async {
      await pump(tester, const StudentTestsPage());
      expect(find.text('Hedefe kalan'), findsNothing);
      expect(find.text('Hedef net takibi'), findsNothing);
      expect(find.text('Konu eksiği'), findsNothing);
      expect(find.text('Demo'), findsNothing);
      expect(find.text('Test / Deneme gir'), findsOneWidget);
    },
  );
  testWidgets('gerçek net hedefi performansta korunur', (tester) async {
    await pump(
      tester,
      const StudentTestsPage(),
      study: _Study(goal: const StudyGoal(dailyGoalMinutes: 30, targetNet: 65)),
    );
    expect(find.text('Hedef net takibi'), findsOneWidget);
    expect(find.text('Demo'), findsNothing);
  });
  testWidgets('profil yalnız çalışan menüleri sunar', (tester) async {
    await pump(tester, const StudentProfilePage());
    for (final text in [
      'Profili düzenle',
      'Velim',
      'Bildirim ayarları',
      'Gizlilik ve Güvenlik',
      'Aboneliğim',
      'Demo',
    ]) {
      expect(find.text(text), findsNothing, reason: text);
    }
    expect(find.text('Öğretmenlerim'), findsOneWidget);
    expect(find.text('Çıkış yap'), findsOneWidget);
  });
  testWidgets('hedefler sayfasında gelecekteki özellik kartı yok', (
    tester,
  ) async {
    await pump(tester, const StudentGoalsOverviewPage());
    expect(find.text('Aylık hedef'), findsNothing);
    expect(find.text('Üniversite hedefi'), findsNothing);
    expect(find.text('Hedefleri düzenle'), findsOneWidget);
  });
}

class _Students implements StudentRepository {
  @override
  Future<StudentProfile?> getByUser(String userId) async =>
      const StudentProfile(
        id: 'student',
        fullName: 'Gerçek Öğrenci',
        gradeLevel: '10',
        origin: 'SelfRegistered',
        subjects: [],
      );
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _Study implements StudyRepository {
  _Study({this.goal});
  final StudyGoal? goal;
  @override
  Future<StudyDashboard> getDashboard(String studentId) async => StudyDashboard(
    studentId: studentId,
    todayEffectiveMinutes: 35,
    todayGoalMinutes: 40,
    todayGoalMet: false,
    weekEffectiveMinutes: 125,
    currentStreakDays: 7,
    longestStreakDays: 8,
    recentSessions: [],
    recentAchievements: [],
    activeGoal: goal,
  );
  @override
  Future<List<TestResult>> listTests(String studentId) async => [];
  @override
  Future<List<StudySession>> listSessions(String studentId) async => [];
  @override
  Future<StudyGoal?> getGoals(String studentId) async => goal;
  @override
  Future<StudyStreak> getStreak(String studentId) async => const StudyStreak(
    currentStreakDays: 7,
    longestStreakDays: 8,
    totalStudyDays: 12,
    studiedToday: true,
    todayEffectiveMinutes: 35,
  );
  @override
  Future<List<StudyAchievement>> getAchievements(String studentId) async => [];
  @override
  Future<WeeklySummary> getWeeklySummary(String studentId) async =>
      WeeklySummary(
        weekStartDate: DateTime(2026, 11, 9),
        totalEffectiveMinutes: 125,
        totalBreakMinutes: 0,
        sessionCount: 0,
        perSubject: [],
        perDay: [],
      );
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _Assignments implements AssignmentRepository {
  _Assignments({this.fail = false});
  bool fail;
  @override
  Future<List<AssignmentItem>> listByStudent(String studentId) async {
    if (fail) throw const ApiException(message: 'Ödevler alınamadı');
    return [];
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
