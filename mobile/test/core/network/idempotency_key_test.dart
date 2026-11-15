import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers.dart';

void main() {
  for (final multipart in [false, true]) {
    test(
      'same transport retry key; next operation new key multipart=$multipart',
      () async {
        final adapter = _Adapter();
        final dio = Dio(BaseOptions(baseUrl: 'https://test'))
          ..httpClientAdapter = adapter;
        final client = ApiClient(
          dio: dio,
          tokenStorage: InMemoryTokenStorage(),
        );
        addTearDown(() {
          client.dispose();
          dio.close();
        });
        Future<void> send() async {
          if (multipart) {
            await client.postMultipart(
              '/write',
              fields: {'body': 'same'},
              idempotent: true,
            );
          } else {
            await client.post(
              '/write',
              data: {'body': 'same'},
              idempotent: true,
            );
          }
        }

        await send();
        await send();
        expect(adapter.keys, hasLength(3));
        expect(
          adapter.keys[0],
          matches(
            RegExp(
              r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
            ),
          ),
        );
        expect(adapter.keys[1], adapter.keys[0]);
        expect(adapter.keys[2], isNot(adapter.keys[0]));
        expect(adapter.bodies[1], adapter.bodies[0]);
      },
    );
  }
  test(
    'manual retry after two timeouts retains key; success frees next operation',
    () async {
      final adapter = _LostReplies();
      final dio = Dio(BaseOptions(baseUrl: 'https://test'))
        ..httpClientAdapter = adapter;
      final client = ApiClient(dio: dio, tokenStorage: InMemoryTokenStorage());
      addTearDown(() {
        client.dispose();
        dio.close();
      });
      await expectLater(
        client.post('/write', data: {'amount': 200}, idempotent: true),
        throwsA(anything),
      );
      await client.post('/write', data: {'amount': 200}, idempotent: true);
      await client.post('/write', data: {'amount': 200}, idempotent: true);
      expect(adapter.keys, hasLength(4));
      expect(adapter.keys.take(3).toSet(), hasLength(1));
      expect(adapter.keys.last, isNot(adapter.keys.first));
    },
  );

  test(
    'pending writes are isolated by current account and changed body',
    () async {
      final adapter = _LostReplies();
      final dio = Dio(BaseOptions(baseUrl: 'https://test'))
        ..httpClientAdapter = adapter;
      final storage = InMemoryTokenStorage();
      await storage.writeAccessToken('account-a');
      final client = ApiClient(dio: dio, tokenStorage: storage);
      addTearDown(() {
        client.dispose();
        dio.close();
      });
      await expectLater(
        client.post('/write', data: {'amount': 1}, idempotent: true),
        throwsA(anything),
      );
      await storage.writeAccessToken('account-b');
      await client.post('/write', data: {'amount': 1}, idempotent: true);
      expect(adapter.keys.last, isNot(adapter.keys.first));
      await storage.writeAccessToken('account-a');
      await client.post('/write', data: {'amount': 2}, idempotent: true);
      expect(adapter.keys.last, isNot(adapter.keys.first));
      await client.post('/write', data: {'amount': 1}, idempotent: true);
      expect(adapter.keys.last, adapter.keys.first);
    },
  );

  test('auth refresh retains key and unmarked reads carry no key', () async {
    final adapter = _Adapter(firstStatus: 401);
    final dio = Dio(BaseOptions(baseUrl: 'https://test'))
      ..httpClientAdapter = adapter;
    final client = ApiClient(
      dio: dio,
      tokenStorage: InMemoryTokenStorage(),
      onRefreshToken: () async => 'fresh',
    );
    addTearDown(() {
      client.dispose();
      dio.close();
    });
    await client.post('/write', idempotent: true);
    await client.get('/read');
    expect(adapter.keys[0], isNotNull);
    expect(adapter.keys[1], adapter.keys[0]);
    expect(adapter.keys[2], isNull);
  });
}

class _Adapter implements HttpClientAdapter {
  _Adapter({this.firstStatus});
  final int? firstStatus;
  final keys = <Object?>[];
  final bodies = <String>[];
  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? stream,
    Future<void>? cancel,
  ) async {
    keys.add(o.headers['Idempotency-Key']);
    bodies.add(
      stream == null
          ? ''
          : String.fromCharCodes(await stream.expand((e) => e).toList()),
    );
    if (keys.length == 1 && firstStatus == null) {
      throw DioException(
        requestOptions: o,
        type: DioExceptionType.receiveTimeout,
      );
    }
    return ResponseBody.fromString(
      '{}',
      keys.length == 1 ? firstStatus! : 200,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

class _LostReplies implements HttpClientAdapter {
  final keys = <Object?>[];
  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? stream,
    Future<void>? cancel,
  ) async {
    keys.add(o.headers['Idempotency-Key']);
    if (keys.length <= 2) {
      throw DioException(
        requestOptions: o,
        type: DioExceptionType.receiveTimeout,
      );
    }
    return ResponseBody.fromString(
      '{}',
      200,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
