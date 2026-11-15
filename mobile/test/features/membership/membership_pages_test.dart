import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/pages/membership_page.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/pages/paywall_page.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/widgets/membership_widgets.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_membership_repository.dart';

/// P09 Task 6 (D-10/D-15): Üyelik ekranı + paywall.
///
/// Sözleşme kontrolleri: iptal düğmesi yalnız yürüyen abonelikte çıkar, satın alma
/// düğmesi (P09 Task 4 gelene kadar) **pasif** kalır ve deneme düğmesi gerçekten
/// `POST /subscriptions/start-trial` çağırır.
void main() {
  group('MembershipPage', () {
    testWidgets('free kullanicida premium CTA ve referans kodu gosterilir', (
      tester,
    ) async {
      final repository = FakeMembershipRepository(
        referral: const ReferralCode(
          code: 'K7MNPQ',
          redemptionCount: 3,
          rewardMonths: 1,
        ),
      );

      await _pump(tester, repository, const MembershipPage());

      expect(find.text('Ücretsiz üyelik'), findsOneWidget);
      expect(find.text("Premium'a geç"), findsOneWidget);
      expect(find.text('K7MNPQ'), findsOneWidget);
      expect(find.text('Aboneliği iptal et'), findsNothing);
    });

    testWidgets(
      'yuruyen abonelikte iptal dugmesi cikar ve onaydan sonra calisir',
      (tester) async {
        final repository = FakeMembershipRepository(
          snapshot: _premium(),
          afterCancel: _premium(status: 'Cancelled'),
        );

        await _pump(tester, repository, const MembershipPage());

        expect(find.text('Premium üyelik aktif'), findsOneWidget);

        await tester.tap(find.text('Aboneliği iptal et'));
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(FilledButton, 'İptal et'));
        await tester.pumpAndSettle();

        expect(repository.cancelCalls, 1);
        expect(find.text('Abonelik iptal edildi'), findsOneWidget);
      },
    );

    testWidgets('iptal edilmis abonelikte iptal dugmesi tekrar gosterilmez', (
      tester,
    ) async {
      final repository = FakeMembershipRepository(
        snapshot: _premium(status: 'Cancelled'),
      );

      await _pump(tester, repository, const MembershipPage());

      expect(find.text('Abonelik iptal edildi'), findsOneWidget);
      expect(find.text('Aboneliği iptal et'), findsNothing);
    });

    testWidgets('referans kodu girisi sunucuya buyuk harfle gider', (
      tester,
    ) async {
      final repository = FakeMembershipRepository(
        afterRedeem: _premium(status: 'Active'),
      );

      await _pump(tester, repository, const MembershipPage());

      await tester.enterText(find.byType(TextField), 'k7mnpq');
      await tester.tap(find.widgetWithText(FilledButton, 'Kodu kullan'));
      await tester.pumpAndSettle();

      expect(repository.redeemCalls.single, 'K7MNPQ');
    });
  });

  group('PaywallPage', () {
    testWidgets('İngilizce paywall rol/hak ve plan etiketlerini çevirir', (
      tester,
    ) async {
      await _pump(
        tester,
        FakeMembershipRepository(plans: [_monthly()]),
        const PaywallPage(),
        locale: const Locale('en'),
      );
      expect(find.text('Students you can link'), findsOneWidget);
      expect(find.text('PDF student report'), findsOneWidget);
      expect(find.text('5 students'), findsOneWidget);
      expect(find.text('Monthly Premium'), findsOneWidget);
    });

    testWidgets('rol plani listelenir, deneme rozeti ve satin alma pasif', (
      tester,
    ) async {
      final repository = FakeMembershipRepository(
        plans: <MembershipPlan>[_monthly(), _studentPlan()],
      );

      await _pump(tester, repository, const PaywallPage());

      // Oturum rolü Teacher; öğrenci planı listeye girmemeli.
      expect(find.text('Aylık Premium'), findsOneWidget);
      // "İlk ay ücretsiz" hem plan rozetinde hem kampanya bölümünde geçer;
      // burada kastedilen plan kartındaki rozettir.
      expect(
        find.descendant(
          of: find.byType(PlanCard),
          matching: find.text('İlk ay ücretsiz'),
        ),
        findsOneWidget,
      );
      expect(find.text('249 TRY / aylık'), findsOneWidget);

      expect(find.text('Satın al (yakında)'), findsNothing);
    });

    testWidgets('deneme dugmesi gercekten start-trial cagirir', (tester) async {
      final repository = FakeMembershipRepository(
        plans: <MembershipPlan>[_monthly()],
        afterTrial: _premium(status: 'Trialing'),
      );

      await _pump(tester, repository, const PaywallPage());
      await tester.tap(find.text('30 gün ücretsiz dene'));
      await tester.pumpAndSettle();

      expect(repository.trialCalls.single, 'teacher-premium-monthly');
      expect(find.text('Premium üyeliğin aktif'), findsOneWidget);
    });

    testWidgets('premium kullaniciya deneme dugmesi gosterilmez', (
      tester,
    ) async {
      final repository = FakeMembershipRepository(
        snapshot: _premium(),
        plans: <MembershipPlan>[_monthly()],
      );

      await _pump(tester, repository, const PaywallPage());

      expect(find.text('30 gün ücretsiz dene'), findsNothing);
      expect(find.text('Premium üyeliğin aktif'), findsOneWidget);
    });

    testWidgets('karsilastirma tablosu yalnizca uygulanan farklari listeler', (
      tester,
    ) async {
      final repository = FakeMembershipRepository(
        plans: <MembershipPlan>[_monthly()],
      );

      await _pump(tester, repository, const PaywallPage());

      expect(find.text('Bağlanabilecek öğrenci'), findsOneWidget);
      expect(find.text('PDF öğrenci raporu'), findsOneWidget);
      // Kapısı olmayan vaatler tabloda değil, "yakında" bölümünde durur.
      expect(find.text('Reklamsız kullanım'), findsNothing);
    });
  });
}

Future<void> _pump(
  WidgetTester tester,
  FakeMembershipRepository repository,
  Widget page, {
  Locale locale = const Locale('tr', 'TR'),
}) async {
  // Ekranlar uzun listeler; varsayılan 800x600 tuvalde alt bölümler hiç
  // kurulmaz ve düğmeler dokunulamaz kalırdı. Tuval bir telefon boyuna açılır.
  tester.view.physicalSize = const Size(1000, 2400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);

  final authCubit = AuthCubit(FakeAuthRepository());
  final membershipCubit = MembershipCubit(repository);
  await membershipCubit.load(role: 'Teacher');

  await tester.pumpWidget(
    MultiBlocProvider(
      providers: <BlocProvider<dynamic>>[
        BlocProvider<AuthCubit>.value(value: authCubit),
        BlocProvider<MembershipCubit>.value(value: membershipCubit),
      ],
      // `DateFormat(..., 'tr_TR')` tarih sembollerini `GlobalMaterialLocalizations`
      // yüklerken hazırlar; test uygulaması bu yüzden gerçek `app.dart` kurulumunu
      // birebir taklit eder (aksi hâlde `LocaleDataException` atar).
      child: MaterialApp(
        locale: locale,
        supportedLocales: AppLocalizations.supportedLocales,
        localizationsDelegates: const <LocalizationsDelegate<dynamic>>[
          AppLocalizations.delegate,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        home: page,
      ),
    ),
  );
  await tester.pumpAndSettle();

  addTearDown(() async {
    await membershipCubit.close();
    await authCubit.close();
  });
}

MembershipSnapshot _premium({String status = 'Active'}) => MembershipSnapshot(
  tier: 'Premium',
  effectiveUntilUtc: DateTime.utc(2026, 10, 4),
  planCode: 'teacher-premium-monthly',
  subscriptionStatus: status,
  entitlement: const MembershipEntitlement(
    tier: 'Premium',
    effectiveUntilUtc: null,
    maxStudents: 2147483647,
    canSeeAds: false,
    detailedAnalytics: true,
    pdfReports: true,
    richNotifications: true,
  ),
);

MembershipPlan _monthly() => const MembershipPlan(
  id: 'plan-1',
  code: 'teacher-premium-monthly',
  role: 'Teacher',
  tier: 'Premium',
  period: 'Monthly',
  price: 249,
  currency: 'TRY',
  trialDays: 30,
  isActive: true,
);

MembershipPlan _studentPlan() => const MembershipPlan(
  id: 'plan-3',
  code: 'student-premium-monthly',
  role: 'Student',
  tier: 'Premium',
  period: 'Monthly',
  price: 99,
  currency: 'TRY',
  trialDays: 30,
  isActive: true,
);
