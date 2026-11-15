import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notification_center_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/pages/notifications_page.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_notification_repository.dart';

/// D-11: Bildirim merkezi ekranı — liste, okundu işareti ve zil rozetinin senkronu.
void main() {
  testWidgets('bildirimleri listeler ve okunmamis rozeti gosterir', (
    tester,
  ) async {
    final repository = FakeNotificationRepository(
      notifications: [_notification('n-1'), _notification('n-2')],
      unreadCount: 2,
    );

    await _pumpPage(tester, repository);

    expect(find.text('Yeni ödev'), findsNWidgets(2));
    expect(find.text('2 okunmamış'), findsOneWidget);
  });

  testWidgets('bildirime dokununca okundu isaretlenir ve rozet duser', (
    tester,
  ) async {
    final repository = FakeNotificationRepository(
      notifications: [_notification('n-1')],
      unreadCount: 1,
    );
    final badgeCubit = NotificationsCubit(repository)..setUnreadCount(1);

    await _pumpPage(tester, repository, badgeCubit: badgeCubit);

    await tester.tap(find.text('Yeni ödev'));
    await tester.pumpAndSettle();

    expect(repository.markedRead, ['n-1']);
    expect(badgeCubit.state.unreadCount, 0);

    await badgeCubit.close();
  });

  testWidgets('bildirim yoksa bos durum gosterilir', (tester) async {
    await _pumpPage(tester, FakeNotificationRepository());

    expect(find.text('Henüz bildirimin yok'), findsOneWidget);
  });
}

UserNotification _notification(String id) => UserNotification(
  id: id,
  userId: 'test-user',
  kind: 'AssignmentCreated',
  title: 'Yeni ödev',
  body: 'Matematik ödevi eklendi',
  // Rota yok: dokunma yalnız okundu işaretler, gezinme denemez (router yok).
  route: null,
  isRead: false,
  createdOnUtc: DateTime.now().toUtc(),
);

Future<void> _pumpPage(
  WidgetTester tester,
  FakeNotificationRepository repository, {
  NotificationsCubit? badgeCubit,
}) async {
  final authCubit = AuthCubit(
    FakeAuthRepository(session: FakeAuthRepository.defaultSession()),
  );
  await authCubit.restoreSession();

  addTearDown(authCubit.close);

  await tester.pumpWidget(
    MultiBlocProvider(
      providers: [
        BlocProvider<AuthCubit>.value(value: authCubit),
        if (badgeCubit != null)
          BlocProvider<NotificationsCubit>.value(value: badgeCubit),
      ],
      child: MaterialApp(
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        locale: const Locale('tr'),
        home: NotificationsPage(cubit: NotificationCenterCubit(repository)),
      ),
    ),
  );
  await tester.pumpAndSettle();
}
