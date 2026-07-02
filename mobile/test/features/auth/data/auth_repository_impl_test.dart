import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/auth/data/models/user_session_model.dart';
import 'package:egitim_ussu_mobile/features/auth/data/repositories/auth_repository_impl.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../helpers.dart';

void main() {
  test(
    'restoreSession clears cached session when token expires soon',
    () async {
      final tokenStorage = InMemoryTokenStorage();
      final localCache = InMemoryLocalCache();
      final repository = AuthRepositoryImpl(
        apiClient: ApiClient(dio: Dio(), tokenStorage: tokenStorage),
        tokenStorage: tokenStorage,
        localCache: localCache,
        config: const AppConfig(
          apiBaseUrl: 'http://localhost',
          appEnvironment: 'development',
          useMockFallback: false,
          mockFallbackFeatures: <String>{},
        ),
        refreshDio: Dio(BaseOptions(baseUrl: 'http://localhost')),
      );
      await localCache.writeString(
        'user_session',
        UserSessionModel(
          userId: 'teacher-1',
          email: 'teacher@example.com',
          fullName: 'Demo Ogretmen',
          roles: const <String>['Teacher'],
          accessToken: 'short-token',
          expiresAtUtc: DateTime.now().toUtc().add(const Duration(seconds: 30)),
        ).toCache(),
      );
      await tokenStorage.writeAccessToken('short-token');

      final session = await repository.restoreSession();

      expect(session, isNull);
      expect(await tokenStorage.readAccessToken(), isNull);
      expect(await localCache.readString('user_session'), isNull);
    },
  );

  test('toCache does not persist access or refresh tokens (Y7)', () {
    final cache = UserSessionModel(
      userId: 'teacher-1',
      email: 'teacher@example.com',
      fullName: 'Demo Ogretmen',
      roles: const <String>['Teacher'],
      accessToken: 'secret-access-token',
      refreshToken: 'secret-refresh-token',
      expiresAtUtc: DateTime.now().toUtc().add(const Duration(days: 1)),
    ).toCache();

    expect(cache.contains('secret-access-token'), isFalse);
    expect(cache.contains('secret-refresh-token'), isFalse);
    expect(cache.contains('accessToken'), isFalse);
    expect(cache.contains('refreshToken'), isFalse);
  });

  test('restoreSession rebuilds valid session using tokens from secure storage (Y7)', () async {
    final tokenStorage = InMemoryTokenStorage();
    final localCache = InMemoryLocalCache();
    final repository = AuthRepositoryImpl(
      apiClient: ApiClient(dio: Dio(), tokenStorage: tokenStorage),
      tokenStorage: tokenStorage,
      localCache: localCache,
      config: const AppConfig(
        apiBaseUrl: 'http://localhost',
        appEnvironment: 'development',
        useMockFallback: false,
        mockFallbackFeatures: <String>{},
      ),
      refreshDio: Dio(BaseOptions(baseUrl: 'http://localhost')),
    );
    await localCache.writeString(
      'user_session',
      UserSessionModel(
        userId: 'teacher-1',
        email: 'teacher@example.com',
        fullName: 'Demo Ogretmen',
        roles: const <String>['Teacher'],
        accessToken: 'ignored-in-cache',
        expiresAtUtc: DateTime.now().toUtc().add(const Duration(days: 1)),
      ).toCache(),
    );
    await tokenStorage.writeAccessToken('secure-access-token');
    await tokenStorage.writeRefreshToken('secure-refresh-token');

    final session = await repository.restoreSession();

    expect(session, isNotNull);
    expect(session!.accessToken, 'secure-access-token');
    expect(session.refreshToken, 'secure-refresh-token');
  });
}
