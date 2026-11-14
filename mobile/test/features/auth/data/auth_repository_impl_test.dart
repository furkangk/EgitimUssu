import 'dart:typed_data';

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

  // P15 T7 / N-67: Açılıştaki restoreSession ile interceptor'ın yenilemesi çakışabilir;
  // aynı anda gelen çağrılar tek bir HTTP refresh'i paylaşır (sunucu yalnız birini kazandırır).
  test('concurrent refreshSession calls share a single HTTP refresh', () async {
    final adapter = _RefreshAdapter.succeeding(
      delay: const Duration(milliseconds: 20),
    );
    final fixture = await _RepositoryFixture.create(adapter);

    final sessions = await Future.wait(
      List.generate(3, (_) => fixture.repository.refreshSession()),
    );

    expect(adapter.calls, 1);
    expect(sessions.map((session) => session.accessToken).toSet(), <String>{
      'rotated-access',
    });
    expect(await fixture.tokenStorage.readRefreshToken(), 'rotated-refresh');
  });

  // N-63: Süresi dolmak üzere olan oturum açılışta yenilenemezse (metro, uçak modu)
  // kullanıcı çıkışa atılmaz; bağlantı gelince ilk istek yeniden yeniler.
  test(
    'restoreSession keeps the session when the refresh fails on the network',
    () async {
      final fixture = await _RepositoryFixture.create(
        _RefreshAdapter.timingOut(),
      );
      await fixture.seedExpiringSession();

      final session = await fixture.repository.restoreSession();

      expect(session, isNotNull);
      expect(await fixture.tokenStorage.readAccessToken(), 'short-token');
      expect(await fixture.tokenStorage.readRefreshToken(), 'old-refresh');
      expect(await fixture.localCache.readString('user_session'), isNotNull);
    },
  );

  test(
    'restoreSession signs out when the server rejects the refresh token',
    () async {
      final fixture = await _RepositoryFixture.create(
        _RefreshAdapter.rejecting(401),
      );
      await fixture.seedExpiringSession();

      final session = await fixture.repository.restoreSession();

      expect(session, isNull);
      expect(await fixture.tokenStorage.readAccessToken(), isNull);
      expect(await fixture.localCache.readString('user_session'), isNull);
    },
  );
}

class _RepositoryFixture {
  _RepositoryFixture._(this.repository, this.tokenStorage, this.localCache);

  static Future<_RepositoryFixture> create(_RefreshAdapter adapter) async {
    final tokenStorage = InMemoryTokenStorage();
    final localCache = InMemoryLocalCache();
    await tokenStorage.writeRefreshToken('old-refresh');
    final refreshDio = Dio(BaseOptions(baseUrl: 'http://localhost'))
      ..httpClientAdapter = adapter;
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
      refreshDio: refreshDio,
    );
    return _RepositoryFixture._(repository, tokenStorage, localCache);
  }

  final AuthRepositoryImpl repository;
  final InMemoryTokenStorage tokenStorage;
  final InMemoryLocalCache localCache;

  Future<void> seedExpiringSession() async {
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
  }
}

class _RefreshAdapter implements HttpClientAdapter {
  _RefreshAdapter._({this.status = 200, this.timeout = false, this.delay});

  factory _RefreshAdapter.succeeding({Duration? delay}) =>
      _RefreshAdapter._(delay: delay);

  factory _RefreshAdapter.timingOut() => _RefreshAdapter._(timeout: true);

  factory _RefreshAdapter.rejecting(int status) =>
      _RefreshAdapter._(status: status);

  final int status;
  final bool timeout;
  final Duration? delay;
  int calls = 0;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    calls++;
    if (delay != null) {
      await Future<void>.delayed(delay!);
    }
    if (timeout) {
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.connectionTimeout,
      );
    }
    final body = status == 200
        ? '{"userId":"teacher-1","email":"teacher@example.com",'
              '"fullName":"Demo Ogretmen","roles":["Teacher"],'
              '"accessToken":"rotated-access",'
              '"expiresAtUtc":"2099-01-01T00:00:00Z",'
              '"refreshToken":"rotated-refresh"}'
        : '{"code":"identity.invalid_refresh_token"}';
    return ResponseBody.fromString(
      body,
      status,
      headers: <String, List<String>>{
        Headers.contentTypeHeader: <String>['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
