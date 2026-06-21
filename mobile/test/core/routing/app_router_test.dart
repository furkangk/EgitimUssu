import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('redirects unauthenticated user to welcome page', (tester) async {
    final cubit = AuthCubit(_FakeAuthRepository());
    await cubit.restoreSession();
    final router = AppRouter(authCubit: cubit, initialLocation: '/dashboard');

    await tester.pumpWidget(
      BlocProvider<AuthCubit>.value(
        value: cubit,
        child: MaterialApp.router(routerConfig: router.router),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('EgitimUssu'), findsOneWidget);
    expect(
      find.text('Ozel ders sureclerinizi tek bir yerde yonetin.'),
      findsOneWidget,
    );
    expect(find.text('Kayit Ol'), findsOneWidget);

    router.dispose();
    await cubit.close();
  });
}

class _FakeAuthRepository implements AuthRepository {
  @override
  Future<UserSession> login({required String email, required String password}) {
    throw UnimplementedError();
  }

  @override
  Future<void> logout() async {}

  @override
  Future<UserSession?> restoreSession() async => null;

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
