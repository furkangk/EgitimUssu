import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';

abstract interface class AuthRepository {
  Future<UserSession> login({required String email, required String password});

  Future<UserSession> register({
    required String email,
    required String password,
    required String firstName,
    required String lastName,
    String? phoneNumber,
  });

  Future<UserSession?> restoreSession();
  Future<UserSession> refreshSession();
  Future<void> logout();
}
