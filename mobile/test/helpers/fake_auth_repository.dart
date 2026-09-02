import 'dart:async';

import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';

/// Tum auth testlerinin ortak sahtesi. Gercek [AuthRepository] imzasini birebir uygular;
/// imza degisirse yalniz burasi guncellenir (her test dosyasinda ayri sahte tutulmaz).
///
/// Davranis ayari:
/// - [session]: `restoreSession` / `login` / `register` bu oturumu dondurur (null ise
///   `restoreSession` null doner, login/register [defaultSession] doner).
/// - [hangOnRestore]: `restoreSession` hicbir zaman tamamlanmaz (uygulama "restoring"
///   durumunda kalir; acilis ekrani testleri icin).
class FakeAuthRepository implements AuthRepository {
  FakeAuthRepository({this.session, this.hangOnRestore = false});

  final UserSession? session;
  final bool hangOnRestore;

  final Completer<UserSession?> _pendingRestore = Completer<UserSession?>();

  int loginCallCount = 0;
  int registerCallCount = 0;
  int refreshCallCount = 0;
  int logoutCount = 0;

  /// Testlerin ozellestirebilecegi varsayilan oturum.
  static UserSession defaultSession({
    String userId = 'test-user',
    String email = 'test@example.com',
    String fullName = 'Test Kullanici',
    List<String> roles = const <String>['Teacher'],
  }) {
    return UserSession(
      userId: userId,
      email: email,
      fullName: fullName,
      roles: roles,
      accessToken: 'access-token',
      refreshToken: 'refresh-token',
      expiresAtUtc: DateTime.now().toUtc().add(const Duration(days: 1)),
    );
  }

  UserSession get _session => session ?? defaultSession();

  @override
  Future<UserSession> login({
    required String email,
    required String password,
    int roleId = 2,
  }) async {
    loginCallCount++;
    return _session;
  }

  @override
  Future<UserSession> register({
    required String email,
    required String password,
    required String firstName,
    required String lastName,
    String? phoneNumber,
    int roleId = 2,
  }) async {
    registerCallCount++;
    return _session;
  }

  @override
  Future<UserSession?> restoreSession() {
    if (hangOnRestore) {
      return _pendingRestore.future;
    }
    return Future<UserSession?>.value(session);
  }

  @override
  Future<UserSession> refreshSession() async {
    refreshCallCount++;
    return _session;
  }

  @override
  Future<void> logout() async {
    logoutCount++;
  }
}
