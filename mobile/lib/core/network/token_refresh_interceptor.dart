import 'package:dio/dio.dart';

/// 401 yanıtlarını yakalar; önce token yenilemeyi dener, başarısızsa oturum kapatır.
///
/// [QueuedInterceptorsWrapper] kullanılır: eş zamanlı 401 yanıtlarının yenileme
/// ve tekrar işlemleri sırayla yürütülür.
class TokenRefreshInterceptor extends QueuedInterceptorsWrapper {
  TokenRefreshInterceptor({
    required Dio dio,
    required Future<String?> Function() onRefresh,
    required void Function() onUnauthorized,
  }) : _retryDio = dio.clone(interceptors: Interceptors()),
       _onRefresh = onRefresh,
       _onUnauthorized = onUnauthorized;

  // Keep the configured adapter/transformer, but bypass the queued interceptor
  // chain so a second 401 cannot recurse or wait on its own pending retry.
  final Dio _retryDio;
  final Future<String?> Function() _onRefresh;
  final void Function() _onUnauthorized;

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) async {
    if (err.response?.statusCode != 401) {
      return handler.next(err);
    }

    // Refresh endpoint'in kendisi 401 dönerse sonsuz döngüye girmemek için atla
    if (_isRefreshPath(err.requestOptions.path)) {
      _onUnauthorized();
      return handler.next(err);
    }

    final String? newToken;
    try {
      newToken = await _onRefresh();
    } catch (_) {
      _onUnauthorized();
      return handler.next(err);
    }
    if (newToken == null || newToken.isEmpty) {
      _onUnauthorized();
      return handler.next(err);
    }

    final original = err.requestOptions;
    final payload = original.data;
    final retryOptions = original.copyWith(
      headers: <String, dynamic>{
        ...original.headers,
        'Authorization': 'Bearer $newToken',
      },
      // Multipart bodies are finalized when first sent and cannot be reused.
      data: payload is FormData ? payload.clone() : payload,
    );

    try {
      final response = await _retryDio.fetch<dynamic>(retryOptions);
      return handler.resolve(response);
    } on DioException catch (retryError) {
      if (retryError.response?.statusCode == 401) {
        _onUnauthorized();
      }
      // A timeout, 5xx or validation error after a successful refresh is not an
      // expired session. Preserve the actual error for the requesting screen.
      return handler.next(retryError);
    }
  }

  static bool _isRefreshPath(String path) => path.contains('/identity/refresh');
}
