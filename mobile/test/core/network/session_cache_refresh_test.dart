import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/cache_store.dart';
import 'package:egitim_ussu_mobile/features/auth/data/repositories/auth_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/students/data/repositories/student_repository_impl.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers.dart';

const config = AppConfig(
  apiBaseUrl: 'http://localhost',
  appEnvironment: 'development',
  useMockFallback: false,
  mockFallbackFeatures: {},
);
void main() {
  test(
    'old POST 401 cannot replay its body using the next account token',
    () async {
      final tokens = InMemoryTokenStorage();
      await tokens.writeAccessToken('alice');
      final response = Completer<ResponseBody>();
      final started = Completer<void>();
      final requests = <RequestOptions>[];
      final dio = Dio()
        ..httpClientAdapter = _Adapter((o) async {
          requests.add(o);
          if (requests.length == 1) {
            started.complete();
            return response.future;
          }
          return _body({}, 200);
        });
      final api = ApiClient(
        dio: dio,
        tokenStorage: tokens,
        onRefreshToken: () async => 'must-not-refresh',
      );
      addTearDown(api.dispose);
      var unauthorized = 0;
      final subscription = api.unauthorizedEvents.listen((_) => unauthorized++);
      addTearDown(subscription.cancel);
      final posting = api.post(
        '/write',
        data: {'private': 'Alice'},
        idempotent: true,
      );
      final check = expectLater(posting, throwsA(isA<ApiException>()));
      await started.future;
      api.clearSession();
      await tokens.writeAccessToken('bob');
      response.complete(_body({}, 401));
      await check;
      expect(requests, hasLength(1));
      expect(unauthorized, 0);
    },
  );
  test('old refresh rejection cannot expire the next account', () async {
    final tokens = InMemoryTokenStorage();
    await tokens.writeAccessToken('alice');
    final refresh = Completer<String?>();
    final started = Completer<void>();
    final api = ApiClient(
      dio: Dio()..httpClientAdapter = _Adapter((_) async => _body({}, 401)),
      tokenStorage: tokens,
      onRefreshToken: () {
        started.complete();
        return refresh.future;
      },
    );
    addTearDown(api.dispose);
    var unauthorized = 0;
    final subscription = api.unauthorizedEvents.listen((_) => unauthorized++);
    addTearDown(subscription.cancel);
    final loading = api.get('/private');
    final check = expectLater(loading, throwsA(isA<ApiException>()));
    await started.future;
    api.clearSession();
    await tokens.writeAccessToken('bob');
    refresh.completeError(
      const ApiException(message: 'old refresh rejected', statusCode: 401),
    );
    await check;
    await Future<void>.delayed(Duration.zero);
    expect(unauthorized, 0);
  });
  for (final failure in ['certificate', 'cancel', 'decode']) {
    test('GET401 then refresh $failure cannot return stale cache', () async {
      final tokens = InMemoryTokenStorage();
      await tokens.writeAccessToken('alice');
      await tokens.writeRefreshToken('refresh');
      final local = InMemoryLocalCache();
      final cache = CacheStore(local)..activate('alice');
      var reject = false;
      final dio = Dio()
        ..httpClientAdapter = _Adapter(
          (_) async => reject
              ? _body({}, 401)
              : _body([
                  {
                    'id': 'student',
                    'fullName': 'Old student',
                    'gradeLevel': '8',
                    'origin': 'TeacherManaged',
                    'isActive': true,
                  },
                ], 200),
        );
      final raw = Dio()
        ..httpClientAdapter = _Adapter((o) async {
          if (failure == 'decode') {
            return _body({'expiresAtUtc': 'not-a-date'}, 200);
          }
          throw DioException(
            requestOptions: o,
            type: failure == 'cancel'
                ? DioExceptionType.cancel
                : DioExceptionType.badCertificate,
          );
        });
      late AuthRepositoryImpl auth;
      final api = ApiClient(
        dio: dio,
        tokenStorage: tokens,
        onRefreshToken: () async => (await auth.refreshSession()).accessToken,
      );
      addTearDown(api.dispose);
      auth = AuthRepositoryImpl(
        apiClient: api,
        tokenStorage: tokens,
        localCache: local,
        cacheStore: cache,
        config: config,
        refreshDio: raw,
      );
      final students = StudentRepositoryImpl(
        apiClient: api,
        config: config,
        localCache: local,
        cacheStore: cache,
      );
      await students.listByTeacher('teacher');
      reject = true;
      await expectLater(
        students.listByTeacher('teacher'),
        throwsA(isA<ApiException>()),
      );
      expect(cache.isStale, isFalse);
    });
  }
}

ResponseBody _body(Object value, int status) => ResponseBody.fromString(
  jsonEncode(value),
  status,
  headers: {
    Headers.contentTypeHeader: ['application/json'],
  },
);

class _Adapter implements HttpClientAdapter {
  _Adapter(this.respond);
  final Future<ResponseBody> Function(RequestOptions) respond;
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) => respond(options);
  @override
  void close({bool force = false}) {}
}
