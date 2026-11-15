import 'dart:convert';
import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/payments/data/models/payment_model.dart';
import 'package:egitim_ussu_mobile/features/payments/data/repositories/payment_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_cubit.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers.dart';

void main() {
  test(
    'stale screen sends delta to collections; server owns resulting totals',
    () async {
      final adapter = _Adapter();
      final dio = Dio(BaseOptions(baseUrl: 'https://test'))
        ..httpClientAdapter = adapter;
      final api = ApiClient(dio: dio, tokenStorage: InMemoryTokenStorage());
      addTearDown(() {
        api.dispose();
        dio.close();
      });
      final repo = PaymentRepositoryImpl(
        apiClient: api,
        config: const AppConfig(
          apiBaseUrl: 'https://test',
          appEnvironment: 'test',
          useMockFallback: false,
          mockFallbackFeatures: {},
        ),
        localCache: InMemoryLocalCache(),
      );
      final cubit = PaymentsCubit(repo);
      addTearDown(cubit.close);
      final stale = PaymentRecordModel.fromJson({
        'id': 'p',
        'currency': 'USD',
        'expectedAmount': 500,
        'collectedAmount': 0,
        'outstandingAmount': 500,
      });
      await cubit.load('teacher');
      await cubit.collect(stale, 20000);
      expect(cubit.state.errorMessage, isNull);
      final request = adapter.requests.singleWhere((o) => o.method == 'POST');
      expect(request.path, '/api/payments/records/p/collections');
      expect(request.method, 'POST');
      expect(request.data['amount'], 200);
      expect(request.data['method'], 1);
      expect(request.data, isNot(contains('collectedAmount')));
      expect(request.data, isNot(contains('status')));
      expect(request.headers['Idempotency-Key'], isNotEmpty);
      // Server already had 100; the stale screen must accept its 300 total.
      expect(cubit.state.records.single.collectedAmount, 30000);
      expect(cubit.state.records.single.outstandingAmount, 20000);
      expect(cubit.state.successMessage, isNotNull);
      await cubit.cancel(stale);
      expect(
        adapter.requests.where((o) => o.method == 'POST').last.path,
        '/api/payments/records/p/cancel',
      );
    },
  );
  test('manual collection retry keeps its first timestamp and key', () async {
    final adapter = _Adapter(lostReplies: 2);
    final dio = Dio(BaseOptions(baseUrl: 'https://test'))
      ..httpClientAdapter = adapter;
    final api = ApiClient(dio: dio, tokenStorage: InMemoryTokenStorage());
    addTearDown(() {
      api.dispose();
      dio.close();
    });
    final repo = PaymentRepositoryImpl(
      apiClient: api,
      config: const AppConfig(
        apiBaseUrl: 'https://test',
        appEnvironment: 'test',
        useMockFallback: false,
        mockFallbackFeatures: {},
      ),
      localCache: InMemoryLocalCache(),
    );
    final first = DateTime.utc(2026, 11, 14, 12);
    await expectLater(
      repo.addCollection('p', 20000, occurredOnUtc: first),
      throwsA(anything),
    );
    await repo.addCollection(
      'p',
      20000,
      occurredOnUtc: first.add(const Duration(minutes: 1)),
    );
    expect(
      adapter.requests.map((o) => o.headers['Idempotency-Key']).toSet(),
      hasLength(1),
    );
    expect(
      adapter.requests.last.data['occurredOnUtc'],
      first.toIso8601String(),
    );
    expect(adapter.requests.last.data['amount'], 200);
  });
}

class _Adapter implements HttpClientAdapter {
  _Adapter({this.lostReplies = 0});
  final int lostReplies;
  final requests = <RequestOptions>[];
  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? stream,
    Future<void>? cancel,
  ) async {
    requests.add(o);
    if (requests.length <= lostReplies) {
      throw DioException(
        requestOptions: o,
        type: DioExceptionType.receiveTimeout,
      );
    }
    final record = {
      'id': 'p',
      'currency': 'USD',
      'expectedAmount': 500,
      'collectedAmount': 300,
      'outstandingAmount': 200,
      'status': 'PartiallyPaid',
    };
    final response = o.method != 'GET'
        ? record
        : o.path.endsWith('/summary')
        ? {'currencySummaries': [], 'totalRecords': 1}
        : {
            'items': [record],
            'totalCount': 1,
          };
    return ResponseBody.fromString(
      jsonEncode(response),
      200,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
