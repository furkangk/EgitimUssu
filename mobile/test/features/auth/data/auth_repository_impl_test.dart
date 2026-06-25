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
}
