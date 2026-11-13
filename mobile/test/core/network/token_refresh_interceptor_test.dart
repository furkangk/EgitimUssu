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
