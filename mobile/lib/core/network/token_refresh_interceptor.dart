import 'dart:async';

import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';

/// 401 yanıtlarını yakalar; oturumu yeniler ve isteği yeni token'la tekrarlar.
///
/// P15 T7 (N-63, N-67, S-1):
/// - **Tek uçuş:** Aynı anda 401 alan istekler tek bir yenilemeyi bekler. Sunucu
///   aynı refresh token'la gelen eşzamanlı isteklerden yalnız birini kazandırır
///   (N-30); her istek ayrı yenileseydi kaybedenler 401 alıp oturumu düşürürdü.
/// - **Token değiştiyse yenileme yok:** İstek eski token'la gitmiş ama o arada
///   başka bir istek yenilemişse, depodaki token'la doğrudan tekrarlanır.
/// - **Yalnız 400/401'de çıkış:** Yenileme ağ hatası, zaman aşımı, 429 ya da 5xx
///   ile düşerse oturum korunur ve istek hatası ekrana iletilir; sunucu refresh
///   token'ı açıkça reddetmedikçe kullanıcı çıkışa atılmaz.
class TokenRefreshInterceptor extends Interceptor {
  TokenRefreshInterceptor({
    required Dio dio,
    required Future<String?> Function() onRefresh,
    required Future<String?> Function() readAccessToken,
    required void Function() onUnauthorized,
  }) : _retryDio = dio.clone(interceptors: Interceptors()),
       _onRefresh = onRefresh,
       _readAccessToken = readAccessToken,
       _onUnauthorized = onUnauthorized;

  // Keep the configured adapter/transformer, but bypass the interceptor chain so
  // a second 401 cannot recurse into another refresh.
  final Dio _retryDio;
  final Future<String?> Function() _onRefresh;
  final Future<String?> Function() _readAccessToken;
  final void Function() _onUnauthorized;

  /// Sürmekte olan yenileme; bitince temizlenir.
  Future<String?>? _refreshInFlight;

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    if (err.response?.statusCode != 401) {
      return handler.next(err);
    }

    // Refresh endpoint'in kendisi 401 dönerse sonsuz döngüye girmemek için atla
    if (_isRefreshPath(err.requestOptions.path)) {
      _onUnauthorized();
      return handler.next(err);
    }

    final original = err.requestOptions;
    final String? token;
    try {
      token = await _tokenForRetry(original);
    } catch (error) {
      if (_isSessionRejected(error)) {
        _onUnauthorized();
        return handler.next(err);
      }
      return handler.next(_transientFailure(original, error));
    }
    if (token == null || token.isEmpty) {
      _onUnauthorized();
      return handler.next(err);
    }

    final payload = original.data;
    final retryOptions = original.copyWith(
      headers: <String, dynamic>{
        ...original.headers,
        'Authorization': 'Bearer $token',
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

  /// N-67: İsteğin gönderildiği token depodakinden farklıysa yenileme zaten
  /// yapılmıştır — onu kullan. Aynıysa (ya da depo boşsa) tek uçuş yenile.
  Future<String?> _tokenForRetry(RequestOptions original) async {
    final stored = await _readAccessToken();
    final sent = _bearerToken(original.headers['Authorization']);
    if (stored != null && stored.isNotEmpty && stored != sent) {
      return stored;
    }
    return _refreshOnce();
  }

  Future<String?> _refreshOnce() {
    final inFlight = _refreshInFlight;
    if (inFlight != null) {
      return inFlight;
    }
    final refresh = _onRefresh();
    _refreshInFlight = refresh;
    // Hata bekleyen her çağırana kendi `await`'inde ulaşır; bu dal yalnız temizler.
    unawaited(
      refresh.then<void>((_) {}, onError: (Object _) {}).whenComplete(() {
        if (identical(_refreshInFlight, refresh)) {
          _refreshInFlight = null;
        }
      }),
    );
    return refresh;
  }

  /// Sunucu refresh token'ı reddetti mi (400/401)? Yalnız bu durumda oturum biter.
  static bool _isSessionRejected(Object error) {
    final status = switch (error) {
      ApiException(:final statusCode) => statusCode,
      DioException(:final response) => response?.statusCode,
      _ => null,
    };
    return status == 400 || status == 401;
  }

  /// Yenileme geçici bir nedenle düştü: isteğin hatası olarak yukarı iletilir.
  /// Yanıtsız hata `ApiErrorKind.network`'e, durum kodlu olan kendi türüne eşlenir.
  static DioException _transientFailure(RequestOptions original, Object error) {
    if (error is DioException) {
      return error.copyWith(requestOptions: original);
    }
    final apiError = error is ApiException ? error : null;
    final status = apiError?.statusCode;
    return DioException(
      requestOptions: original,
      type: status == null
          ? DioExceptionType.connectionError
          : DioExceptionType.badResponse,
      response: status == null
          ? null
          : Response<dynamic>(
              requestOptions: original,
              statusCode: status,
              data: <String, dynamic>{
                'code': apiError?.code,
                'message': apiError?.message,
              },
            ),
      error: error,
      message: apiError?.message,
    );
  }

  static String? _bearerToken(Object? header) {
    const prefix = 'Bearer ';
    if (header is String && header.startsWith(prefix)) {
      return header.substring(prefix.length);
    }
    return null;
  }

  static bool _isRefreshPath(String path) => path.contains('/identity/refresh');
}
