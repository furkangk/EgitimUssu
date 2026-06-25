import 'dart:async';

import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('restores cached session and authenticates user', () async {
    final cubit = AuthCubit(_RestoringAuthRepository());

    await cubit.restoreSession();

    expect(cubit.state.status, AuthStatus.authenticated);
    expect(cubit.state.session?.email, 'teacher1@example.com');

    await cubit.close();
  });

  test('expires session when api client reports unauthorized', () async {
    final unauthorizedEvents = StreamController<void>.broadcast();
    final repository = _RestoringAuthRepository();
    final cubit = AuthCubit(
      repository,
      unauthorizedEvents: unauthorizedEvents.stream,
    );
    await cubit.restoreSession();

    unauthorizedEvents.add(null);
    await Future<void>.delayed(Duration.zero);

    expect(repository.logoutCount, 1);
    expect(cubit.state.status, AuthStatus.unauthenticated);
    expect(cubit.state.errorMessage, contains('Oturumun süresi doldu'));

    await unauthorizedEvents.close();
    await cubit.close();
  });
}

class _RestoringAuthRepository implements AuthRepository {
  int logoutCount = 0;

  @override
  Future<UserSession> login({required String email, required String password}) {
    throw UnimplementedError();
  }

  @override
  Future<void> logout() async {
    logoutCount++;
  }

  @override
  Future<UserSession?> restoreSession() async {
    return UserSession(
      userId: 'teacher-id',
      email: 'teacher1@example.com',
      fullName: 'Ayse Yilmaz',
      roles: const <String>['Teacher'],
      accessToken: 'cached-token',
      expiresAtUtc: DateTime.now().toUtc().add(const Duration(days: 1)),
    );
  }

  @override
  Future<UserSession> refreshSession() {
    throw UnimplementedError();
  }

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
}
