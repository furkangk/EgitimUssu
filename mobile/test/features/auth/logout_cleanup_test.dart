import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/cache_store.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/features/auth/data/repositories/auth_repository_impl.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../helpers.dart';

void main() {
  test(
    'logout clears retained write bodies and keys before manual retry',
    () async {
      final tokens = InMemoryTokenStorage();
      await tokens.writeAccessToken('alice');
      final adapter = _WriteAdapter();
      final api = ApiClient(
        dio: Dio()..httpClientAdapter = adapter,
        tokenStorage: tokens,
      );
      addTearDown(api.dispose);
      await expectLater(
        api.post(
          '/write',
          data: {'private': 'old'},
          idempotent: true,
          retryIdentity: 'same',
        ),
        throwsA(isA<ApiException>()),
      );
      api.clearSession();
      adapter.fail = false;
      await api.post(
        '/write',
        data: {'private': 'new'},
        idempotent: true,
        retryIdentity: 'same',
      );
      expect(adapter.requests.last.data, {'private': 'new'});
      expect(
        adapter.requests.last.headers['Idempotency-Key'],
        isNot(adapter.requests.first.headers['Idempotency-Key']),
      );
    },
  );
  test(
    'in-flight write cannot retry with the next account after logout',
    () async {
      final tokens = InMemoryTokenStorage();
      await tokens.writeAccessToken('alice');
      final adapter = _WriteAdapter()..hold = Completer<void>();
      final api = ApiClient(
        dio: Dio()..httpClientAdapter = adapter,
        tokenStorage: tokens,
      );
      addTearDown(api.dispose);
      final posting = api.post(
        '/write',
        data: {'private': 'Alice'},
        idempotent: true,
      );
      final checked = expectLater(posting, throwsA(isA<ApiException>()));
      await adapter.started.future;
      api.clearSession();
      await tokens.writeAccessToken('bob');
      adapter.hold!.complete();
      await checked;
      expect(adapter.requests, hasLength(1));
    },
  );
  for (final fail in [false, true]) {
    test(
      'logout revokes server session and clears local data, network failure=$fail',
      () async {
        final f = await _fixture(fail: fail);
        await f.repo.logout();
        expect(
          f.adapter.requests.map((o) => o.path),
          contains('/api/identity/logout'),
        );
        final request = f.adapter.requests.firstWhere(
          (o) => o.path.endsWith('/logout'),
        );
        expect(request.data, {'refreshToken': 'old-refresh'});
        expect(request.headers['Authorization'], 'Bearer old-access');
        expect(await f.tokens.readAccessToken(), isNull);
        expect(await f.tokens.readRefreshToken(), isNull);
        expect(f.prefs.getKeys(), {'theme'});
      },
    );
  }
  test('late refresh cannot persist session after logout', () async {
    final f = await _fixture();
    f.adapter.refresh = Completer<ResponseBody>();
    final refreshing = f.repo.refreshSession();
    final checked = expectLater(refreshing, throwsA(isA<ApiException>()));
    await f.adapter.started.future;
    await f.repo.logout();
    f.adapter.refresh!.complete(
      _body({
        'userId': 'alice',
        'email': 'a@example.com',
        'fullName': 'Alice',
        'roles': ['Teacher'],
        'accessToken': 'late-access',
        'refreshToken': 'late-refresh',
        'expiresAtUtc': '2099-01-01T00:00:00Z',
      }),
    );
    await checked;
    expect(
      f.adapter.requests
          .where((r) => r.path.endsWith('/logout'))
          .map((r) => r.data['refreshToken']),
      contains('late-refresh'),
    );
    expect(await f.tokens.readAccessToken(), isNull);
    expect(await f.tokens.readRefreshToken(), isNull);
    expect(f.prefs.getKeys(), {'theme'});
  });
}

Future<
  ({
    AuthRepositoryImpl repo,
    _Adapter adapter,
    InMemoryTokenStorage tokens,
    SharedPreferences prefs,
  })
>
_fixture({bool fail = false}) async {
  SharedPreferences.setMockInitialValues({
    'theme': 'dark',
    'user_session': 'old',
    'cache.alice.students.list': 'private',
    'payments.records.alice': 'legacy',
  });
  final prefs = await SharedPreferences.getInstance();
  final local = SharedPrefsLocalCache(prefs);
  final store = CacheStore(local)..activate('alice');
  final tokens = InMemoryTokenStorage();
  await tokens.writeAccessToken('old-access');
  await tokens.writeRefreshToken('old-refresh');
  final adapter = _Adapter(fail);
  final dio = Dio()..httpClientAdapter = adapter;
  final api = ApiClient(dio: dio, tokenStorage: tokens);
  addTearDown(api.dispose);
  final raw = Dio()..httpClientAdapter = adapter;
  return (
    repo: AuthRepositoryImpl(
      apiClient: api,
      tokenStorage: tokens,
      localCache: local,
      cacheStore: store,
      config: const AppConfig(
        apiBaseUrl: 'http://localhost',
        appEnvironment: 'development',
        useMockFallback: false,
        mockFallbackFeatures: {},
      ),
      refreshDio: raw,
    ),
    adapter: adapter,
    tokens: tokens,
    prefs: prefs,
  );
}

ResponseBody _body(Object data) => ResponseBody.fromString(
  jsonEncode(data),
  200,
  headers: {
    Headers.contentTypeHeader: ['application/json'],
  },
);

class _Adapter implements HttpClientAdapter {
  _Adapter(this.fail);
  final bool fail;
  final requests = <RequestOptions>[];
  final started = Completer<void>();
  Completer<ResponseBody>? refresh;
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    if (options.path.endsWith('/refresh')) {
      started.complete();
      return refresh!.future;
    }
    if (fail) {
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.connectionError,
      );
    }
    return _body({});
  }

  @override
  void close({bool force = false}) {}
}

class _WriteAdapter implements HttpClientAdapter {
  bool fail = true;
  Completer<void>? hold;
  final started = Completer<void>();
  final requests = <RequestOptions>[];
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    if (!started.isCompleted) started.complete();
    await hold?.future;
    if (fail) {
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.connectionError,
      );
    }
    return _body({});
  }

  @override
  void close({bool force = false}) {}
}
