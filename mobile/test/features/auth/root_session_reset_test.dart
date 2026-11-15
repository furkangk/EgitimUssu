import 'dart:async';

import 'package:egitim_ussu_mobile/app/app.dart';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/push/push_service.dart';
import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/core/storage/cache_store.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_state.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/stale_data_banner.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers.dart';
import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_membership_repository.dart';
import '../../helpers/fake_notification_repository.dart';
import '../../helpers/fake_parent_repository.dart';

const premium = MembershipSnapshot(
  tier: 'Premium',
  effectiveUntilUtc: null,
  planCode: 'premium',
  subscriptionStatus: 'Active',
  entitlement: MembershipEntitlement(
    tier: 'Premium',
    effectiveUntilUtc: null,
    maxStudents: 100,
    canSeeAds: false,
    detailedAnalytics: true,
    pdfReports: true,
    richNotifications: true,
  ),
);
void main() {
  test(
    'logout prevents a late restore from authenticating the root again',
    () async {
      final repository = _DelayedAuth();
      final auth = AuthCubit(repository);
      final restoring = auth.restoreSession();
      await auth.logout();
      repository.pending.complete(FakeAuthRepository.defaultSession());
      await restoring;
      expect(auth.state.status, AuthStatus.unauthenticated);
      expect(auth.state.session, isNull);
      await auth.close();
    },
  );
  test('reset discards in-flight notification load', () async {
    final repo = _DelayedNotifications();
    final cubit = NotificationsCubit(repo);
    final loading = cubit.load('alice');
    cubit.reset();
    repo.pending.complete([]);
    await loading;
    expect(cubit.state.status, NotificationsStatus.initial);
    expect(cubit.state.unreadCount, 0);
    await cubit.close();
  });
  for (final operation in ['load', 'trial', 'referral']) {
    test('membership reset discards late $operation response', () async {
      final repo = _DelayedMembership();
      final cubit = MembershipCubit(repo);
      final Future<dynamic> loading = switch (operation) {
        'load' => cubit.load(role: 'Teacher'),
        'trial' => cubit.startTrial('premium'),
        _ => cubit.loadReferralCode(),
      };
      cubit.reset();
      repo.pending.complete(premium);
      repo.pendingReferral.complete(
        const ReferralCode(code: 'SECRET', redemptionCount: 2, rewardMonths: 1),
      );
      await loading;
      expect(cubit.state.status, MembershipStatus.initial);
      expect(cubit.state.isPremium, isFalse);
      expect(cubit.state.referral, isNull);
      expect(cubit.state.isBusy, isFalse);
      await cubit.close();
    });
  }
  testWidgets('app logout resets populated root cubits', (tester) async {
    final cache = CacheStore(InMemoryLocalCache())..activate('test-user');
    injector.registerSingleton<CacheStore>(cache);
    await cache.write('students', 'list', []);
    await cache.load(
      feature: 'students',
      key: 'list',
      fetch: () async => throw const ApiException(
        message: 'offline',
        kind: ApiErrorKind.network,
      ),
      decode: (v) => v,
    );
    final auth = AuthCubit(
      FakeAuthRepository(
        session: FakeAuthRepository.defaultSession(roles: ['Parent']),
      ),
    );
    final app = AppRouter(authCubit: auth, enablePreviewRoutes: false);
    final badge = NotificationsCubit(FakeNotificationRepository());
    final membership = MembershipCubit(
      FakeMembershipRepository(snapshot: premium),
    );
    injector.registerSingleton<ParentRepository>(
      FakeParentRepository(children: []),
    );
    addTearDown(() async {
      await tester.pumpWidget(const SizedBox());
      app.router.dispose();
      app.dispose();
      await auth.close();
      await badge.close();
      await membership.close();
      await injector.reset();
    });
    await tester.pumpWidget(
      EgitimUssuApp(
        authCubit: auth,
        appRouter: app,
        notificationsCubit: badge,
        membershipCubit: membership,
        pushTaps: const Stream<PushTap>.empty(),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.byType(StaleDataBanner), findsOneWidget);
    badge.setUnreadCount(8);
    expect(membership.state.isPremium, isTrue);
    await auth.logout();
    await tester.pumpAndSettle();
    expect(badge.state.status, NotificationsStatus.initial);
    expect(badge.state.unreadCount, 0);
    expect(membership.state.status, MembershipStatus.initial);
    expect(membership.state.isPremium, isFalse);
  });
}

class _DelayedNotifications extends FakeNotificationRepository {
  final pending = Completer<List<LessonReminder>>();
  @override
  Future<List<LessonReminder>> listReminders({
    required String teacherUserId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
    bool activeOnly = true,
  }) => pending.future;
  @override
  Future<int> unreadNotificationCount(String userId) async => 9;
}

class _DelayedMembership extends FakeMembershipRepository {
  final pending = Completer<MembershipSnapshot>();
  final pendingReferral = Completer<ReferralCode>();
  @override
  Future<MembershipSnapshot> me() => pending.future;
  @override
  Future<MembershipSnapshot> startTrial(String planCode) => pending.future;
  @override
  Future<ReferralCode> referralCode() => pendingReferral.future;
}

class _DelayedAuth extends FakeAuthRepository {
  final pending = Completer<UserSession?>();
  @override
  Future<UserSession?> restoreSession() => pending.future;
}
