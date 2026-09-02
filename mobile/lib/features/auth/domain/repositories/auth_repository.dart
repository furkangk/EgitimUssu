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

  /// D-01: Sifre sifirlama kodu istegi. Kullanici kayitli olmasa da hata donmez
  /// (sunucu e-posta enumeration'i engellemek icin her durumda 200 doner).
  Future<void> requestPasswordReset({required String email});

  /// D-01: E-postadaki kod ile yeni sifreyi belirler.
  Future<void> confirmPasswordReset({
    required String email,
    required String token,
    required String newPassword,
  });

  Future<UserSession?> restoreSession();
  Future<UserSession> refreshSession();
  Future<void> logout();
}
