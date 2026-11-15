import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/core/routing/route_access.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_parent_repository.dart';

void main() {
  // Explicit expected product permissions; not derived from the implementation.
  const groups = <Set<AppRole>, List<String>>{
    {AppRole.teacher, AppRole.admin}: [
      '/dashboard',
      '/more',
      '/teacher-profile',
      '/teacher/reports',
      '/students',
      '/students/child',
      '/scheduling',
      '/lesson-sessions',
      '/lesson-sessions/detail',
      '/lesson-sessions/detail/note',
      '/lesson-notes/new',
      '/assignments',
      '/assignments/new',
      '/assignments/lesson',
      '/payments',
      '/payments/new',
      '/payments/edit',
    ],
    {AppRole.student}: [
      '/student-home',
      '/student/studies',
      '/student/performance',
      '/student/lessons',
      '/student/lessons/lesson',
      '/student/discover',
      '/student/tests',
      '/student/calendar',
      '/student/goals-overview',
      '/student/more',
      '/student/profile',
      '/student/teacher',
      '/student/assignments',
      '/student/progress',
      '/study/timer',
      '/study/test',
      '/study/catalog',
      '/study/notes',
      '/study/goals',
      '/study/history',
      '/study/achievements',
    ],
    {AppRole.parent}: [
      '/parent',
      '/parent/children',
      '/parent/notifications',
      '/parent/profile',
      '/parent/child-detail',
      '/parent/payments',
      '/parent/assignments',
      '/parent/lessons',
    ],
    {
      AppRole.teacher,
      AppRole.admin,
      AppRole.student,
      AppRole.parent,
      AppRole.unknown,
    }: [
      '/messages',
      '/messages/conversation',
      '/notifications',
      '/settings/privacy',
      '/settings/notifications',
      '/account/sessions',
      '/membership',
      '/membership/paywall',
      '/student/reports',
    ],
  };
  for (final role in AppRole.values) {
    test('$role only enters its own area and common screens', () {
      for (final entry in groups.entries) {
        for (final route in entry.value) {
          expect(
            RouteAccess.canAccess('$route?studentId=s', role),
            entry.key.contains(role),
            reason: route,
          );
        }
      }
    });
  }
  test(
    'role precedence preserves existing multi-role behavior without unknown escalation',
    () {
      expect(RouteAccess.roleFor(['Parent', 'Teacher']), AppRole.parent);
      expect(RouteAccess.roleFor(['Student', 'Teacher']), AppRole.teacher);
      expect(RouteAccess.roleFor(['Student']), AppRole.student);
      expect(RouteAccess.roleFor(['Admin']), AppRole.admin);
      expect(RouteAccess.roleFor([]), AppRole.unknown);
      expect(RouteAccess.roleFor(['FutureRole']), AppRole.unknown);
    },
  );
  test('wrong role returns home, unknown route reaches error builder', () {
    expect(RouteAccess.redirectFor('/messages', AppRole.parent), isNull);
    expect(RouteAccess.redirectFor('/students', AppRole.parent), '/parent');
    expect(
      RouteAccess.redirectFor('/payments', AppRole.student),
      '/student-home',
    );
    expect(RouteAccess.redirectFor('/parent', AppRole.teacher), '/dashboard');
    for (final path in [
      '/messages-extra',
      '/parentish',
      '/settings/missing',
      '/unknown',
    ]) {
      expect(RouteAccess.canAccess(path, AppRole.parent), isFalse);
      expect(RouteAccess.redirectFor(path, AppRole.parent), isNull);
    }
  });
  test(
    'registered dynamic templates remain protected with unusual path parameters',
    () {
      expect(
        RouteAccess.redirectFor('/students/:studentId', AppRole.parent),
        '/parent',
      );
      expect(
        RouteAccess.redirectFor('/messages/:conversationId', AppRole.parent),
        isNull,
      );
    },
  );
  test(
    'every registered non-public route has explicit permissions and vice versa',
    () async {
      final auth = AuthCubit(FakeAuthRepository());
      final app = AppRouter(authCubit: auth, enablePreviewRoutes: false);
      const public = {
        '/',
        '/login',
        '/register',
        '/role-selection',
        '/verify-email',
        '/password-reset',
        '/password-reset/confirm',
      };
      final registered = app.router.configuration.routes
          .whereType<GoRoute>()
          .map((r) => r.path)
          .toSet()
          .difference(public);
      expect(RouteAccess.routes.keys.toSet(), registered);
      app.router.dispose();
      app.dispose();
      await auth.close();
    },
  );
  testWidgets(
    'unknown location shows localized recovery screen without crashing',
    (tester) async {
      injector.registerSingleton<ParentRepository>(
        FakeParentRepository(children: []),
      );
      addTearDown(injector.reset);
      final auth = AuthCubit(
        FakeAuthRepository(
          session: FakeAuthRepository.defaultSession(roles: ['Parent']),
        ),
      );
      await auth.restoreSession();
      final app = AppRouter(
        authCubit: auth,
        initialLocation: '/missing',
        enablePreviewRoutes: false,
      );
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
      expect(find.text('Sayfa bulunamadı'), findsOneWidget);
      expect(find.text('Ana sayfaya dön'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
      app.router.dispose();
      app.dispose();
      await auth.close();
    },
  );
}
