import 'dart:io';
import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers.dart';

void main() {
  for (final kind in ['file', 'multipart', 'json']) {
    for (final idempotent in [false, true]) {
      test('$kind timeout and retry policy idempotent=$idempotent', () async {
        final dir = await Directory.systemTemp.createTemp('upload-timeout');
        final file = await File('${dir.path}/file.txt').writeAsString('upload');
        final adapter = _Adapter(retry: idempotent);
        final dio = Dio(
          BaseOptions(
            baseUrl: 'https://test',
            sendTimeout: const Duration(seconds: 15),
            receiveTimeout: const Duration(seconds: 30),
          ),
        )..httpClientAdapter = adapter;
        final client = ApiClient(
          dio: dio,
          tokenStorage: InMemoryTokenStorage(),
        );
        addTearDown(() async {
          client.dispose();
          dio.close();
          await dir.delete(recursive: true);
        });
        switch (kind) {
          case 'file':
            await client.postFile(
              '/upload',
              filePath: file.path,
              idempotent: idempotent,
            );
          case 'multipart':
            await client.postMultipart(
              '/upload',
              fields: {'title': 'resource'},
              filePath: file.path,
              idempotent: idempotent,
            );
          default:
            await client.post(
              '/write',
              data: {'title': 'resource'},
              idempotent: idempotent,
            );
        }
        expect(adapter.options, hasLength(idempotent ? 2 : 1));
        for (final option in adapter.options) {
          expect(
            option.sendTimeout,
            Duration(seconds: kind == 'json' ? 15 : 120),
          );
          expect(option.receiveTimeout, const Duration(seconds: 30));
        }
        if (idempotent) {
          expect(
            adapter.options.map((o) => o.headers['Idempotency-Key']).toSet(),
            hasLength(1),
          );
        }
        expect(dio.options.sendTimeout, const Duration(seconds: 15));
      });
    }
  }
}

class _Adapter implements HttpClientAdapter {
  _Adapter({required this.retry});
  final bool retry;
  final options = <RequestOptions>[];
  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? stream,
    Future<void>? cancel,
  ) async {
    options.add(o);
    if (stream != null) await stream.drain<void>();
    if (retry && options.length == 1) {
      throw DioException(requestOptions: o, type: DioExceptionType.sendTimeout);
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
