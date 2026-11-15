import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_messaging_repository.dart';
import '../../helpers/fake_notification_repository.dart';
import '../../helpers/fake_parent_repository.dart';

void main() {
  setUp(() {
    injector.registerSingleton<ParentRepository>(
      FakeParentRepository(
        children: [
          ChildLink(
            id: 'link',
            parentUserId: 'test-user',
            studentId: 'child',
            status: 'Approved',
            isPrimaryContact: true,
            requestedOnUtc: DateTime.utc(2026),
            childDisplayName: 'Ada',
          ),
        ],
      ),
    );
    injector.registerSingleton<MessagingRepository>(FakeMessagingRepository());
    injector.registerSingleton<NotificationRepository>(
      FakeNotificationRepository(),
    );
  });
  tearDown(injector.reset);

  for (final entry in {
    'payments': 'Ödeme özeti',
    'assignments': 'Ödev özeti',
    'lessons': 'Ders özeti',
  }.entries) {
    testWidgets(
      'parent ${entry.key} route opens the matching real child summary',
      (tester) async {
        await pumpParent(tester, '/parent/${entry.key}');
        expect(find.text(entry.value), findsOneWidget);
        expect(find.text('Ada'), findsOneWidget);
        expect(find.text('Haftalık çalışma'), findsNothing);
        if (entry.key == 'payments') {
          expect(find.text('500 TRY'), findsOneWidget);
        }
        if (entry.key == 'assignments') expect(find.text('5'), findsOneWidget);
        if (entry.key == 'lessons') expect(find.text('6'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
    testWidgets('parent ${entry.key} returns home through bottom navigation', (
      tester,
    ) async {
      final app = await pumpParent(tester, '/parent/${entry.key}');
      await tester.tap(find.text('Ana Sayfa'));
      await tester.pumpAndSettle();
      expect(app.router.state.uri.path, '/parent');
      expect(find.text('Haftalık çalışma'), findsOneWidget);
    });
  }
  testWidgets(
    'parent home exposes working messages and notification center entries',
    (tester) async {
      final app = await pumpParent(tester, '/parent');
      await tester.tap(find.byTooltip('Mesajlar'));
      await tester.pumpAndSettle();
      expect(app.router.state.uri.path, '/messages');
      app.router.go('/parent');
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('Bildirimler'));
      await tester.pumpAndSettle();
      expect(app.router.state.uri.path, '/notifications');
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets('wrong-role route redirects parent to parent home', (
    tester,
  ) async {
    final app = await pumpParent(tester, '/students');
    expect(app.router.state.uri.path, '/parent');
    expect(find.text('Merhaba, Test'), findsOneWidget);
  });
  testWidgets(
    'parent summary with no approved child offers linking instead of data',
    (tester) async {
      await injector.unregister<ParentRepository>();
      injector.registerSingleton<ParentRepository>(
        FakeParentRepository(children: []),
      );
      await pumpParent(tester, '/parent/payments');
      expect(find.text('Henüz onaylı çocuk yok'), findsOneWidget);
      expect(find.text('500 TRY'), findsNothing);
    },
  );
}

Future<AppRouter> pumpParent(WidgetTester tester, String location) async {
  final auth = AuthCubit(
    FakeAuthRepository(
      session: FakeAuthRepository.defaultSession(roles: ['Parent']),
    ),
  );
  await auth.restoreSession();
  final app = AppRouter(
    authCubit: auth,
    initialLocation: location,
    enablePreviewRoutes: false,
  );
  addTearDown(() async {
    app.router.dispose();
    app.dispose();
    await auth.close();
  });
  await tester.pumpWidget(
    BlocProvider.value(
      value: auth,
      child: MaterialApp.router(
        locale: const Locale('tr'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        routerConfig: app.router,
      ),
    ),
  );
  await tester.pumpAndSettle();
  return app;
}
