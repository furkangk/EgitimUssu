import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/payments/data/models/payment_model.dart';
import 'package:egitim_ussu_mobile/features/payments/data/repositories/payment_repository_impl.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../helpers.dart';

/// Varsayılan geliştirme modunu (mock fallback açık, backend erişilemez) taklit eder:
/// PUT ağ hatası verir → repo yerel `model`'i döndürmeli. "Tahsil Et"in varsayılan
/// modda hiç iş yapmadığı ("hiçbir şey olmuyor") iddiasını doğrudan test eder.
void main() {
  test('updateRecord falls back to Paid model when API unreachable (mock mode)',
      () async {
    final dio = Dio(BaseOptions(baseUrl: 'http://localhost'))
      ..httpClientAdapter = _FailingAdapter();
    final apiClient = ApiClient(dio: dio, tokenStorage: InMemoryTokenStorage());
    final repo = PaymentRepositoryImpl(
      apiClient: apiClient,
      config: const AppConfig(
        apiBaseUrl: 'http://localhost',
        appEnvironment: 'development',
        useMockFallback: true,
        mockFallbackFeatures: <String>{'*'},
      ),
      localCache: InMemoryLocalCache(),
    );

    final paid = const PaymentRecordModel(
      id: 'payment-2',
      teacherUserId: 'teacher-1',
      studentId: 'student-1',
      description: 'Haziran — Fizik',
      currency: 'TRY',
      expectedAmount: 900,
      collectedAmount: 900,
      outstandingAmount: 0,
      status: 'Paid',
      itemType: 'LessonFee',
    );

    final result = await repo.updateRecord(paid);

    expect(result.status, 'Paid');
    expect(result.collectedAmount, 900);
  });
}

class _FailingAdapter implements HttpClientAdapter {
  @override
  void close({bool force = false}) {}

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<List<int>>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    throw DioException(
      requestOptions: options,
      type: DioExceptionType.connectionError,
    );
  }
}
