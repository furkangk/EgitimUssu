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

  /// D-02: E-posta dogrulama kodunu yeniden gonderir (kullanici yoksa ya da
  /// zaten dogrulanmissa sunucu yine 200 doner).
  Future<void> requestEmailVerification({required String email});

  /// D-02: E-postadaki kod ile hesabin e-postasini dogrular.
  Future<void> confirmEmailVerification({
    required String email,
    required String token,
  });

  Future<UserSession?> restoreSession();
  Future<UserSession> refreshSession();
  Future<void> logout();
}
