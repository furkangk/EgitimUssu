import 'dart:async';

import 'package:egitim_ussu_mobile/app/app.dart';
import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('app shows welcome screen while auth restores', (tester) async {
    final authCubit = AuthCubit(_DelayedAuthRepository());
    final appRouter = AppRouter(authCubit: authCubit);
    final notificationsCubit = NotificationsCubit(
      _FakeNotificationRepository(),
    );

    await tester.pumpWidget(
      EgitimUssuApp(
        authCubit: authCubit,
        appRouter: appRouter,
        notificationsCubit: notificationsCubit,
      ),
    );

    expect(find.text('EgitimUssu'), findsOneWidget);
    expect(find.text('Giris Yap'), findsOneWidget);
    expect(find.text('Kayit Ol'), findsOneWidget);

    appRouter.dispose();
    await authCubit.close();
    await notificationsCubit.close();
  });
}

class _FakeNotificationRepository implements NotificationRepository {
  @override
  Future<List<LessonReminder>> listReminders({
    required String teacherUserId,
    bool activeOnly = false,
  }) async {
    return const <LessonReminder>[];
  }
}

class _DelayedAuthRepository implements AuthRepository {
  final _completer = Completer<UserSession?>();

  @override
  Future<UserSession> login({required String email, required String password}) {
    throw UnimplementedError();
  }

  @override
  Future<void> logout() async {}

  @override
  Future<UserSession?> restoreSession() => _completer.future;

  @override
  Future<UserSession> register({
    required String email,
    required String password,
    required String firstName,
    required String lastName,
    String? phoneNumber,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<UserSession> refreshSession() {
    throw UnimplementedError();
  }
}
