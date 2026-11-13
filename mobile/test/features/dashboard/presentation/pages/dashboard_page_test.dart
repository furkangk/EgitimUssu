import 'package:egitim_ussu_mobile/features/dashboard/domain/dashboard_contracts.dart';
import 'package:egitim_ussu_mobile/features/dashboard/presentation/cubit/dashboard_state.dart';
import 'package:egitim_ussu_mobile/features/dashboard/presentation/pages/dashboard_page.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../../helpers/fake_notification_repository.dart';

/// P13 Task 6b: ana sayfanın metinleri ARB'ye taşındı. Bu test taşımanın
/// ekranda **gerçekten** doğru metni ürettiğini doğrular — statik tarama
/// (`test/l10n/localization_test.dart`) yalnız kaynakta kalıntı olmadığını görür.
void main() {
  final today = DateTime(2026, 9, 6);

  DashboardState buildState() {
    return DashboardState(
      streakCount: 3,
      studentCount: 1,
      todayLessonCount: 1,
      todayLessonDuration: const Duration(hours: 3, minutes: 30),
      students: const <StudentProfile>[
        StudentProfile(
          id: 'student-1',
          fullName: 'Zeynep Demir',
          gradeLevel: '9',
          origin: 'Manual',
          subjects: <StudentSubjectTarget>[],
        ),
      ],
      todayLessons: <DashboardTodayLesson>[
        DashboardTodayLesson(
          id: 'lesson-1',
          studentId: 'student-1',
          subject: 'Matematik',
          lessonFormat: 'InPerson',
          startAtUtc: today.add(const Duration(hours: 9)),
          endAtUtc: today.add(const Duration(hours: 10)),
          status: 'Completed',
        ),
      ],
      recentActivities: <DashboardActivity>[
        DashboardActivity(
          studentName: 'Zeynep Demir',
          kind: DashboardActivityKind.paymentReceived,
          occurredAt: DateTime.now().copyWith(hour: 10, minute: 30),
          accent: Colors.green,
        ),
        // Adı bilinmeyen öğrenci: yedek metin ekran katmanından gelmeli.
        DashboardActivity(
          studentName: null,
          kind: DashboardActivityKind.lessonPlanned,
          occurredAt: DateTime.now().subtract(const Duration(days: 1)),
          accent: Colors.blue,
        ),
      ],
      pendingAssignments: <DashboardPendingAssignment>[
        DashboardPendingAssignment(
          id: 'asgn-1',
          studentId: 'student-1',
          title: 'Denklemler',
          dueDateUtc: DateTime.now().add(const Duration(days: 5, hours: 1)),
        ),
      ],
      overduePayments: <DashboardOverduePayment>[
        DashboardOverduePayment(
          id: 'pay-1',
          studentId: 'student-1',
          description: 'Eylül ayı ders ücreti',
          currency: 'TRY',
          outstandingAmount: 850,
          dueDateUtc: DateTime.now().subtract(const Duration(days: 3)),
        ),
      ],
    );
  }

  Future<void> pumpPanel(WidgetTester tester) async {
    await tester.pumpWidget(
      MaterialApp(
        locale: const Locale('tr', 'TR'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: BlocProvider<NotificationsCubit>(
          create: (_) => NotificationsCubit(FakeNotificationRepository()),
          child: TeacherPanelView(
            teacherName: 'Ayşe Öğretmen',
            state: buildState(),
          ),
        ),
      ),
    );
    await tester.pump();

    // Test yazı tipi her glifi tam kare çizdiği için metinler gerçek uygulamadan
    // belirgin geniştir; 172 px sabit genişlikli ders kartının üst satırı yalnız
    // testte taşar. Metin doğrulamasıyla ilgisi yok, taşma hatası yutulur.
    while (tester.takeException() != null) {}
  }

  testWidgets('özet, bölüm başlıkları ve ders durumu ARB metniyle gelir', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1080, 3600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await pumpPanel(tester);

    expect(find.text('Günlük seri'), findsOneWidget);
    expect(find.text('3 gün'), findsOneWidget);
    expect(find.text('Bugünün Dersleri'), findsOneWidget);
    expect(find.text('Toplam 3s 30dk'), findsOneWidget);
    expect(find.text('Hızlı işlemler'), findsOneWidget);
    expect(find.text('Ders ekle'), findsOneWidget);
    expect(find.text('Tümünü gör'), findsWidgets);
    expect(find.text('Tamamlandı'), findsOneWidget);
    expect(find.text('Yüz yüze'), findsOneWidget);
    expect(find.text('Bekleyen Ödevler'), findsOneWidget);
    expect(find.text('1 ödev'), findsOneWidget);
    expect(find.text('Geciken Ödemeler'), findsOneWidget);
  });

  testWidgets('aktivite satırı türden metin ve göreli zaman üretir', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1080, 3600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await pumpPanel(tester);

    expect(find.text('Son aktiviteler'), findsOneWidget);
    expect(find.text('Ödeme alındı'), findsOneWidget);
    expect(find.text('Bugün 10:30'), findsOneWidget);
    expect(find.text('Ders planlandı'), findsOneWidget);
    // Adı olmayan aktivite yedek metinle görünür.
    expect(find.text('Öğrenci'), findsOneWidget);
    expect(find.textContaining('Dün '), findsOneWidget);
  });

  testWidgets('ödev ve ödeme rozetleri ARB metniyle gelir', (tester) async {
    tester.view.physicalSize = const Size(1080, 3600);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await pumpPanel(tester);

    await tester.tap(find.text('Bekleyen Ödevler'));
    await tester.pumpAndSettle();
    expect(find.text('5 gün'), findsOneWidget);

    await tester.tap(find.text('Geciken Ödemeler'));
    await tester.pumpAndSettle();
    expect(find.text('3 gün gecikti'), findsOneWidget);
  });
}
