import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers.dart';

void main() {
  for (final status in <int>[200, 500, 401]) {
    test(
      'refreshed retry preserves configured transport and status $status',
      () async {
        final fixture = _Fixture(retryStatus: status);
        addTearDown(fixture.dispose);

        if (status == 200) {
          expect(await fixture.client.get('/resource'), <String, dynamic>{
            'ok': true,
          });
        } else {
          await expectLater(
            fixture.client.get('/resource'),
            throwsA(
              isA<ApiException>().having((e) => e.statusCode, 'status', status),
            ),
          );
        }
        await Future<void>.delayed(Duration.zero);
        expect(fixture.adapter.requests, hasLength(2));
        expect(
          fixture.adapter.requests.last.headers['Authorization'],
          'Bearer fresh',
        );
        expect(fixture.refreshCount, 1);
        expect(fixture.unauthorizedCount, status == 401 ? 1 : 0);
      },
    );
  }

  test(
    'a retry timeout does not expire the successfully refreshed session',
    () async {
      final fixture = _Fixture(timeoutOnRetry: true);
      addTearDown(fixture.dispose);
      await expectLater(
        fixture.client.get('/resource'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.kind,
            'kind',
            ApiErrorKind.network,
          ),
        ),
      );
      await Future<void>.delayed(Duration.zero);
      expect(fixture.adapter.requests, hasLength(2));
      expect(fixture.unauthorizedCount, 0);
    },
  );

  // P15 T7 / N-67: Dashboard'ın paralel çağrıları birlikte 401 aldığında yalnız biri
  // yeniler; diğerleri aynı sonucu bekler. Eskiden her biri ayrı rotasyon yapıyordu ve
  // sunucu tek kazanana indirgendiğinde (N-30) kaybedenler çıkışa düşüyordu (S-1).
  test('five parallel 401s share a single refresh', () async {
    final fixture = _TokenFixture(
      onRefresh: (storage) async {
        await Future<void>.delayed(const Duration(milliseconds: 20));
        await storage.writeAccessToken('fresh');
        return 'fresh';
      },
    );
    addTearDown(fixture.dispose);

    final results = await Future.wait(
      List.generate(5, (index) => fixture.client.get('/resource/$index')),
    );

    expect(results, everyElement(<String, dynamic>{'ok': true}));
    expect(fixture.refreshCount, 1);
    expect(fixture.unauthorizedCount, 0);
    expect(
      fixture.adapter.requests
          .where((request) => request.headers['Authorization'] == 'Bearer fresh'),
      hasLength(5),
    );
  });

  // N-67: İstek eski token'la gitmiş ama o arada başka bir istek yenilemiş — depodaki
  // token farklıysa yeniden yenilemeden onunla tekrar denenir.
  test(
    'a 401 for an outdated token is replayed with the stored token without refreshing',
    () async {
      final fixture = _TokenFixture(
        onRefresh: (storage) async => 'unexpected',
        beforeRejecting: (storage) => storage.writeAccessToken('fresh'),
      );
      addTearDown(fixture.dispose);

      expect(await fixture.client.get('/resource'), <String, dynamic>{
        'ok': true,
      });
      expect(fixture.refreshCount, 0);
      expect(fixture.unauthorizedCount, 0);
      expect(
        fixture.adapter.requests.last.headers['Authorization'],
        'Bearer fresh',
      );
    },
  );

  // N-63: Metroda açılan uygulama — refresh ağ hatasıyla düşer. Oturum silinmez; ekran
  // bir ağ hatası görür ve bağlantı gelince bir sonraki istek yeniden dener.
  for (final failure in <ApiException>[
    const ApiException(message: 'zaman asimi', kind: ApiErrorKind.network),
    const ApiException(
      message: 'sunucu',
      kind: ApiErrorKind.network,
      statusCode: 500,
    ),
    const ApiException(message: 'kota', statusCode: 429),
  ]) {
    test(
      'refresh failure ${failure.statusCode ?? 'network'} keeps the session',
      () async {
        final fixture = _TokenFixture(onRefresh: (_) async => throw failure);
        addTearDown(fixture.dispose);

        await expectLater(
          fixture.client.get('/resource'),
          throwsA(
            isA<ApiException>().having(
              (e) => e.kind,
              'kind',
              isNot(ApiErrorKind.auth),
            ),
          ),
        );
        expect(fixture.unauthorizedCount, 0);
        expect(await fixture.storage.readAccessToken(), 'stale');
      },
    );
  }

  test('refresh timeout surfaces as a network error', () async {
    final fixture = _TokenFixture(
      onRefresh: (_) async => throw const ApiException(
        message: 'zaman asimi',
        kind: ApiErrorKind.network,
      ),
    );
    addTearDown(fixture.dispose);

    await expectLater(
      fixture.client.get('/resource'),
      throwsA(
        isA<ApiException>().having((e) => e.kind, 'kind', ApiErrorKind.network),
      ),
    );
  });

  // Yalnız sunucu refresh token'ı reddederse (400/401) oturum biter.
  for (final status in <int>[400, 401]) {
    test('refresh rejected with $status expires the session', () async {
      final fixture = _TokenFixture(
        onRefresh: (_) async =>
            throw ApiException(message: 'reddedildi', statusCode: status),
      );
      addTearDown(fixture.dispose);

      await expectLater(
        fixture.client.get('/resource'),
        throwsA(isA<ApiException>()),
      );
      expect(fixture.unauthorizedCount, 1);
    });
  }

  test(
    'multipart request can be replayed after refreshing its session',
    () async {
      final fixture = _Fixture();
      addTearDown(fixture.dispose);
      expect(
        await fixture.client.postMultipart(
          '/resource',
          fields: <String, String>{'title': 'Ders'},
        ),
        <String, dynamic>{'ok': true},
      );
      expect(fixture.adapter.requests, hasLength(2));
      expect(fixture.adapter.bodies.last, contains('Ders'));
      expect(fixture.unauthorizedCount, 0);
    },
  );
}

class _Fixture {
  _Fixture({int retryStatus = 200, bool timeoutOnRetry = false}) {
    adapter = _Adapter(retryStatus, timeoutOnRetry);
    dio = Dio(BaseOptions(baseUrl: 'https://api.test'))
      ..httpClientAdapter = adapter;
    client = ApiClient(
      dio: dio,
      tokenStorage: InMemoryTokenStorage(),
      onRefreshToken: () async {
        refreshCount++;
        return 'fresh';
      },
    );
    client.unauthorizedEvents.listen((_) => unauthorizedCount++);
  }

  late final _Adapter adapter;
  late final Dio dio;
  late final ApiClient client;
  int refreshCount = 0;
  int unauthorizedCount = 0;

  void dispose() {
    client.dispose();
    dio.close();
  }
}

/// Sunucuyu token'a göre taklit eder: 'Bearer fresh' → 200, diğer her şey → 401.
class _TokenFixture {
  _TokenFixture({
    required Future<String?> Function(InMemoryTokenStorage storage) onRefresh,
    Future<void> Function(InMemoryTokenStorage storage)? beforeRejecting,
  }) {
    storage = InMemoryTokenStorage();
    adapter = _TokenAdapter(storage, beforeRejecting);
    dio = Dio(BaseOptions(baseUrl: 'https://api.test'))
      ..httpClientAdapter = adapter;
    client = ApiClient(
      dio: dio,
      tokenStorage: storage,
      onRefreshToken: () {
        refreshCount++;
        return onRefresh(storage);
      },
    );
    client.unauthorizedEvents.listen((_) => unauthorizedCount++);
  }

  late final InMemoryTokenStorage storage;
  late final _TokenAdapter adapter;
  late final Dio dio;
  late final ApiClient client;
  int refreshCount = 0;
  int unauthorizedCount = 0;

  void dispose() {
    client.dispose();
    dio.close();
  }
}

class _TokenAdapter implements HttpClientAdapter {
  _TokenAdapter(this.storage, this.beforeRejecting) {
    storage.writeAccessToken('stale');
  }

  final InMemoryTokenStorage storage;
  final Future<void> Function(InMemoryTokenStorage storage)? beforeRejecting;
  final requests = <RequestOptions>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    final accepted = options.headers['Authorization'] == 'Bearer fresh';
    if (!accepted && beforeRejecting != null) {
      await beforeRejecting!(storage);
    }
    return ResponseBody.fromString(
      accepted ? '{"ok":true}' : '{"code":"shared.unauthorized"}',
      accepted ? 200 : 401,
      headers: <String, List<String>>{
        Headers.contentTypeHeader: <String>['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

class _Adapter implements HttpClientAdapter {
  _Adapter(this.retryStatus, this.timeoutOnRetry);

  final int retryStatus;
  final bool timeoutOnRetry;
  final requests = <RequestOptions>[];
  final bodies = <String>[];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    if (requestStream != null) {
      final bytes = await requestStream.expand((chunk) => chunk).toList();
      bodies.add(String.fromCharCodes(bytes));
    }
    if (requests.length > 1 && timeoutOnRetry) {
      throw DioException(
        requestOptions: options,
        type: DioExceptionType.receiveTimeout,
      );
    }
    return ResponseBody.fromString(
      '{"ok":true}',
      requests.length == 1 ? 401 : retryStatus,
      headers: <String, List<String>>{
        Headers.contentTypeHeader: <String>['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
