import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';

abstract interface class AuthRepository {
  Future<UserSession> login({
    required String email,
    required String password,
    int roleId = 2,
  });

  Future<UserSession> register({
    required String email,
    required String password,
    required String firstName,
    required String lastName,
    String? phoneNumber,
    int roleId = 2,
  });

  Future<UserSession?> restoreSession();
  Future<UserSession> refreshSession();
  Future<void> logout();
}
