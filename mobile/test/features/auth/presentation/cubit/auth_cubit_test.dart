import 'dart:async';

import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../../helpers/fake_auth_repository.dart';

FakeAuthRepository _restoringRepository() {
  return FakeAuthRepository(
    session: FakeAuthRepository.defaultSession(
      userId: 'teacher-id',
      email: 'teacher1@example.com',
      fullName: 'Ayse Yilmaz',
    ),
  );
}

void main() {
  test('restores cached session and authenticates user', () async {
    final cubit = AuthCubit(_restoringRepository());

    await cubit.restoreSession();

    expect(cubit.state.status, AuthStatus.authenticated);
    expect(cubit.state.session?.email, 'teacher1@example.com');

    await cubit.close();
  });

  test('expires session when api client reports unauthorized', () async {
    final unauthorizedEvents = StreamController<void>.broadcast();
    final repository = _restoringRepository();
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
