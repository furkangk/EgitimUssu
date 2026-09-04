import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:egitim_ussu_mobile/shared/widgets/premium_lock.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import '../helpers/fake_membership_repository.dart';

/// P09 Task 6 (D-10): Premium kilidi.
///
/// Kilit **tek** bir doğruluk kaynağından beslenir: `MembershipCubit`
/// (`GET /api/membership/me`). Ekranlar kendi başına "bu kullanıcı premium mu"
/// kararı vermez — K-09'un mobil karşılığı.
void main() {
  testWidgets('premium kullanicida icerik dogrudan gosterilir', (tester) async {
    final cubit = MembershipCubit(FakeMembershipRepository(snapshot: _premium()));
    await cubit.load();

    await _pump(tester, cubit);

    expect(find.text('Detayli analiz'), findsOneWidget);
    expect(find.byType(PremiumLockCard), findsNothing);

    await cubit.close();
  });

  testWidgets('free kullanicida kilit karti gosterilir ve icerik tiklanamaz', (
    tester,
  ) async {
    final cubit = MembershipCubit(FakeMembershipRepository());
    await cubit.load();

    await _pump(tester, cubit);

    expect(find.byType(PremiumLockCard), findsOneWidget);
    expect(find.text("Premium'a geç"), findsOneWidget);
    expect(
      tester.widget<IgnorePointer>(find.byKey(premiumLockPreviewKey)).ignoring,
      isTrue,
    );

    await cubit.close();
  });

  testWidgets('kilit kartindaki dugme paywall ekranina goturur', (tester) async {
    final cubit = MembershipCubit(FakeMembershipRepository());
    await cubit.load();

    await _pump(tester, cubit);
    await tester.tap(find.text("Premium'a geç"));
    await tester.pumpAndSettle();

    expect(find.text('PAYWALL'), findsOneWidget);

    await cubit.close();
  });

  testWidgets(
    'sunucudan gelen 402 mesaji kilit kartinda gosterilir (cubit gerekmez)',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: const Scaffold(
            body: PremiumLockCard(message: 'PDF rapor Premium üyelik gerektirir.'),
          ),
        ),
      );

      expect(find.text('PDF rapor Premium üyelik gerektirir.'), findsOneWidget);
      expect(find.text("Premium'a geç"), findsOneWidget);
    },
  );
}

Future<void> _pump(WidgetTester tester, MembershipCubit cubit) async {
  final router = GoRouter(
    initialLocation: '/',
    routes: <RouteBase>[
      GoRoute(
        path: '/',
        builder: (context, state) => const Scaffold(
          body: PremiumLock(child: Text('Detayli analiz')),
        ),
      ),
      GoRoute(
        path: '/membership/paywall',
        builder: (context, state) => const Scaffold(body: Text('PAYWALL')),
      ),
    ],
  );

  await tester.pumpWidget(
    BlocProvider<MembershipCubit>.value(
      value: cubit,
      child: MaterialApp.router(routerConfig: router),
    ),
  );
  await tester.pumpAndSettle();
  addTearDown(router.dispose);
}

MembershipSnapshot _premium() => MembershipSnapshot(
  tier: 'Premium',
  effectiveUntilUtc: DateTime.utc(2026, 10, 4),
  planCode: 'teacher-premium-monthly',
  subscriptionStatus: 'Active',
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
