import 'dart:async';

import 'package:egitim_ussu_mobile/app/app.dart';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/push/push_service.dart';
import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_membership_repository.dart';
import '../../helpers/fake_notification_repository.dart';
import '../../helpers/fake_parent_repository.dart';

void main() {
  for (final cold in [false, true]) {
    testWidgets('push maps parent route with cold start $cold', (tester) async {
      final fixture = await _pump(tester, cold: cold);
      fixture.taps.add(const PushTap(route: '/scheduling'));
      await tester.pump();
      if (cold) {
        fixture.repository.restored.complete(fixture.repository.session);
      }
      await tester.pumpAndSettle();
      expect(fixture.app.router.state.uri.path, '/parent/lessons');
      expect(tester.takeException(), isNull);
    });
  }
  testWidgets('unknown push opens notification center', (tester) async {
    final fixture = await _pump(tester);
    fixture.taps.add(const PushTap(route: '/missing'));
    await tester.pumpAndSettle();
    expect(fixture.app.router.state.uri.path, '/notifications');
    expect(find.text('Sayfa bulunamadı'), findsNothing);
  });
  testWidgets('in-app notification uses the same role mapping', (tester) async {
    final fixture = await _pump(tester, notificationRoute: '/scheduling');
    fixture.app.router.go('/notifications');
    await tester.pumpAndSettle();
    await tester.tap(find.text('Ders hatırlatması'));
    await tester.pumpAndSettle();
    expect(fixture.app.router.state.uri.path, '/parent/lessons');
  });
}

class _DelayedAuth extends FakeAuthRepository {
  _DelayedAuth({required this.cold})
    : super(session: FakeAuthRepository.defaultSession(roles: ['Parent']));
  final bool cold;
  final restored = Completer<UserSession?>();
  @override
  Future<UserSession?> restoreSession() =>
      cold ? restored.future : super.restoreSession();
}

Future<
  ({AppRouter app, StreamController<PushTap> taps, _DelayedAuth repository})
>
_pump(
  WidgetTester tester, {
  bool cold = false,
  String? notificationRoute,
}) async {
  final repository = _DelayedAuth(cold: cold);
  final auth = AuthCubit(repository);
  final app = AppRouter(authCubit: auth, enablePreviewRoutes: false);
  final taps = StreamController<PushTap>.broadcast();
  final notifications = FakeNotificationRepository(
    notifications: notificationRoute == null
        ? []
        : [
            UserNotification(
              id: 'n',
              userId: 'test-user',
              kind: 'LessonReminder',
              title: 'Ders hatırlatması',
              body: 'Ders',
              route: notificationRoute,
              isRead: false,
              createdOnUtc: DateTime.now().toUtc(),
            ),
          ],
  );
  final badge = NotificationsCubit(notifications);
  final membership = MembershipCubit(FakeMembershipRepository());
  injector.registerSingleton<ParentRepository>(
    FakeParentRepository(children: []),
  );
  injector.registerSingleton<NotificationRepository>(notifications);
  addTearDown(() async {
    await tester.pumpWidget(const SizedBox());
    await taps.close();
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
      pushTaps: taps.stream,
    ),
  );
  await tester.pumpAndSettle();
  return (app: app, taps: taps, repository: repository);
}
