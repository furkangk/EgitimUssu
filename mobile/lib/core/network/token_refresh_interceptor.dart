import 'package:dio/dio.dart';

/// 401 yanıtlarını yakalar; önce token yenilemeyi dener, başarısızsa oturum kapatır.
///
/// [QueuedInterceptorsWrapper] kullanılır: eş zamanlı birden fazla 401 geldiğinde
/// yalnızca bir yenileme isteği gönderilir, diğerleri kuyrukta bekler.
class TokenRefreshInterceptor extends QueuedInterceptorsWrapper {
  TokenRefreshInterceptor({
    required Future<String?> Function() onRefresh,
    required void Function() onUnauthorized,
  })  : _onRefresh = onRefresh,
        _onUnauthorized = onUnauthorized;

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

    try {
      final newToken = await _onRefresh();
      if (newToken == null) {
        _onUnauthorized();
        return handler.next(err);
      }

      // Orijinal isteği yeni token ile yeniden gönder
      final retryOptions = err.requestOptions
        ..headers['Authorization'] = 'Bearer $newToken';

      // Yeni bir Dio ile yeniden dene (interceptor zincirini atlar, sonsuz döngü olmaz)
      final response = await Dio().fetch<dynamic>(retryOptions);
      return handler.resolve(response);
    } catch (_) {
      _onUnauthorized();
      return handler.next(err);
    }
  }

  static bool _isRefreshPath(String path) =>
      path.contains('/identity/refresh');
}
