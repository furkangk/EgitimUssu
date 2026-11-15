import 'dart:async';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers/fake_auth_repository.dart';

void main() {
  for (final action in ['restore', 'login', 'register']) {
    for (final error in [
      const ApiException(message: 'API failed'),
      StateError('private-token'),
    ]) {
      test('$action catches ${error.runtimeType} and leaves loading', () async {
        final repo = _DeferredAuth();
        final cubit = AuthCubit(repo);
        final pending = _run(cubit, action);
        final assertion = expectLater(pending, completes);
        repo.result.completeError(error);
        await assertion;
        expect(cubit.state.status, AuthStatus.unauthenticated);
        expect(cubit.state.session, isNull);
        if (action != 'restore') expect(cubit.state.errorMessage, isNotEmpty);
        expect(cubit.state.errorMessage, isNot(contains('private-token')));
        await cubit.close();
      });
    }
    test('late $action after logout cannot authenticate again', () async {
      final repo = _DeferredAuth();
      final cubit = AuthCubit(repo);
      final pending = _run(cubit, action);
      await cubit.logout();
      repo.result.complete(FakeAuthRepository.defaultSession());
      await pending;
      expect(cubit.state.status, AuthStatus.unauthenticated);
      expect(cubit.state.session, isNull);
      await cubit.close();
    });
  }
  test('logout completion after close does not emit', () async {
    final repo = _DeferredAuth()..deferLogout = true;
    final cubit = AuthCubit(repo);
    final pending = cubit.logout();
    await cubit.close();
    final assertion = expectLater(pending, completes);
    repo.logoutResult.complete();
    await assertion;
  });
}

Future<void> _run(AuthCubit cubit, String action) => switch (action) {
  'restore' => cubit.restoreSession(),
  'login' => cubit.login(email: 'test@example.com', password: 'secret'),
  _ => cubit.register(
    email: 'test@example.com',
    password: 'secret',
    firstName: 'A',
    lastName: 'B',
  ),
};

class _DeferredAuth extends FakeAuthRepository {
  final result = Completer<UserSession?>();
  final logoutResult = Completer<void>();
  bool deferLogout = false;
  @override
  Future<UserSession?> restoreSession() => result.future;
  @override
  Future<UserSession> login({
    required String email,
    required String password,
    int roleId = 2,
  }) async => (await result.future)!;
  @override
  Future<UserSession> register({
    required String email,
    required String password,
    required String firstName,
    required String lastName,
    String? phoneNumber,
    int roleId = 2,
  }) async => (await result.future)!;
  @override
  Future<void> logout() => deferLogout ? logoutResult.future : super.logout();
}
