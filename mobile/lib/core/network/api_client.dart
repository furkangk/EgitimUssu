import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/network/token_refresh_interceptor.dart';
import 'package:egitim_ussu_mobile/core/storage/token_storage.dart';

class ApiClient {
  ApiClient({
    required Dio dio,
    required TokenStorage tokenStorage,
    Future<String?> Function()? onRefreshToken,
  }) : _dio = dio,
       _tokenStorage = tokenStorage {
    // İsteklere Bearer token ekle
    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          final generation = _sessionGeneration;
          options.extra[TokenRefreshInterceptor.sessionGenerationKey] =
              generation;
          final token = await tokenStorage.readAccessToken();
          if (generation != _sessionGeneration) {
            return handler.reject(
              DioException(
                requestOptions: options,
                type: DioExceptionType.cancel,
              ),
            );
          }
          if (token != null && token.isNotEmpty) {
            options.headers['Authorization'] = 'Bearer $token';
          }
          handler.next(options);
        },
      ),
    );

    // 401'de sessizce token yenile; oturum yalnız sunucu refresh'i reddederse kapanır
    if (onRefreshToken != null) {
      _dio.interceptors.add(
        TokenRefreshInterceptor(
          dio: _dio,
          onRefresh: onRefreshToken,
          // N-67: 401 alan istek eski token'la gittiyse yenilemeden depodakiyle tekrarlanır.
          readAccessToken: tokenStorage.readAccessToken,
          onUnauthorized: _fireUnauthorized,
          sessionGeneration: () => _sessionGeneration,
        ),
      );
    }
  }

  final Dio _dio;
  final TokenStorage _tokenStorage;
  final Map<String, (String, Object?)> _pendingWrites = {};

  final StreamController<void> _unauthorizedController =
      StreamController<void>.broadcast();

  Stream<void> get unauthorizedEvents => _unauthorizedController.stream;

  void _fireUnauthorized() {
    if (!_unauthorizedController.isClosed) {
      _unauthorizedController.add(null);
    }
  }

  int _sessionGeneration = 0;

  void clearSession() {
    _sessionGeneration++;
    _pendingWrites.clear();
  }

  void dispose() {
    _pendingWrites.clear();
    _unauthorizedController.close();
  }

  Future<Map<String, dynamic>> post(
    String path, {
    Map<String, dynamic>? data,
    bool idempotent = false,
    String? idempotencyKey,
    String? retryIdentity,
  }) async {
    try {
      final response = await _post(
        path,
        data,
        idempotent,
        idempotencyKey,
        retryIdentity: retryIdentity,
      );
      return response.data ?? <String, dynamic>{};
    } on DioException catch (error) {
      throw ApiException.fromDioException(error);
    }
  }

  /// Çok parçalı (multipart) dosya yükler. [filePath] yerel dosya yolu; alan adı [field].
  Future<Map<String, dynamic>> postFile(
    String path, {
    required String filePath,
    String field = 'file',
    bool idempotent = false,
    String? idempotencyKey,
    String? retryIdentity,
  }) async {
    try {
      final formData = FormData.fromMap(<String, dynamic>{
        field: await MultipartFile.fromFile(filePath),
      });
      final response = await _post(
        path,
        formData,
        idempotent,
        idempotencyKey,
        retryIdentity:
            retryIdentity ?? '$field:${await _fileIdentity(filePath)}',
      );
      return response.data ?? <String, dynamic>{};
    } on DioException catch (error) {
      throw ApiException.fromDioException(error);
    }
  }

  /// Cok parcali (multipart) form gonderir: istege bagli bir dosya + ek metin alanlari.
  /// [postFile]'dan farki, dosyanin yaninda form alanlari da tasimasidir (P04: ders kaynagi).
  Future<Map<String, dynamic>> postMultipart(
    String path, {
    Map<String, String> fields = const <String, String>{},
    String? filePath,
    String field = 'file',
    bool idempotent = false,
    String? idempotencyKey,
    String? retryIdentity,
  }) async {
    try {
      final payload = <String, dynamic>{...fields};
      if (filePath != null) {
        payload[field] = await MultipartFile.fromFile(filePath);
      }
      final response = await _post(
        path,
        FormData.fromMap(payload),
        idempotent,
        idempotencyKey,
        retryIdentity:
            retryIdentity ??
            jsonEncode([
              _canonical(fields),
              field,
              if (filePath != null) await _fileIdentity(filePath),
            ]),
      );
      return response.data ?? <String, dynamic>{};
    } on DioException catch (error) {
      throw ApiException.fromDioException(error);
    }
  }

  /// Anahtar mantıksal işlem başına, ilk gönderimden önce üretilir.
  /// Ağ sonucu belirsizse aynı gövde/anahtarla yalnız bir kez tekrar denenir.
  /// İki deneme de sonuçsuzsa aynı kullanıcı/uç/içeriğin manuel tekrarı
  /// ilk anahtar/gövdeyi kullanır; başarı sonraki işlemi serbest bırakır.
  /// [retryIdentity] yalnız değişken gönderim zamanını dışlamak içindir.
  /// Çağıran ayrıca açık [idempotencyKey] kullanabilir.
  Future<Response<Map<String, dynamic>>> _post(
    String path,
    Object? data,
    bool idempotent,
    String? idempotencyKey, {
    String? retryIdentity,
  }) async {
    if (!idempotent && idempotencyKey == null) {
      return _dio.post<Map<String, dynamic>>(
        path,
        data: data,
        options: data is FormData
            ? Options(sendTimeout: const Duration(seconds: 120))
            : null,
      );
    }
    final generation = _sessionGeneration;
    final subject = _subject(await _tokenStorage.readAccessToken());
    final identity = jsonEncode([
      subject,
      path,
      retryIdentity ?? _canonical(data),
    ]);
    final pending = idempotencyKey == null ? _pendingWrites[identity] : null;
    final key = idempotencyKey ?? pending?.$1 ?? _newKey();
    // A manual retry uses the original body too (e.g. collection timestamp).
    final original =
        pending?.$2 ?? (data is FormData ? data : jsonDecode(jsonEncode(data)));
    final options = Options(
      headers: {'Idempotency-Key': key},
      sendTimeout: original is FormData ? const Duration(seconds: 120) : null,
    );
    Future<Response<Map<String, dynamic>>> send() {
      if (generation != _sessionGeneration) {
        throw const ApiException(
          message: 'Oturum değişti.',
          kind: ApiErrorKind.auth,
          statusCode: 401,
        );
      }
      return _dio.post<Map<String, dynamic>>(
        path,
        data: original is FormData && original.isFinalized
            ? original.clone()
            : original,
        options: options,
      );
    }

    try {
      Response<Map<String, dynamic>> response;
      try {
        response = await send();
      } on DioException catch (error) {
        if (!_networkFailure(error)) rethrow;
        response = await send();
      }
      _pendingWrites.remove(identity);
      return response;
    } on DioException catch (error) {
      final payload = error.response?.data;
      final inProgress =
          payload is Map && payload['code'] == 'shared.idempotency_in_progress';
      if (generation == _sessionGeneration &&
          idempotencyKey == null &&
          (_networkFailure(error) ||
              inProgress ||
              (error.response?.statusCode ?? 0) >= 500)) {
        _pendingWrites[identity] = (key, original);
      } else {
        _pendingWrites.remove(identity);
      }
      rethrow;
    }
  }

  static bool _networkFailure(DioException e) =>
      e.type == DioExceptionType.connectionError ||
      e.type == DioExceptionType.connectionTimeout ||
      e.type == DioExceptionType.sendTimeout ||
      e.type == DioExceptionType.receiveTimeout;

  static Object? _canonical(Object? value) {
    if (value is Map<String, dynamic>) {
      final keys = value.keys.toList()..sort();
      return {for (final key in keys) key: _canonical(value[key])};
    }
    if (value is List) return value.map(_canonical).toList();
    return value;
  }

  static String? _subject(String? token) {
    if (token == null) return null;
    try {
      final payload = jsonDecode(
        utf8.decode(base64Url.decode(base64Url.normalize(token.split('.')[1]))),
      );
      return payload['sub']?.toString() ?? token;
    } catch (_) {
      return token;
    } // Opaque development tokens still isolate accounts.
  }

  static Future<String> _fileIdentity(String path) async {
    final stat = await File(path).stat();
    return '$path:${stat.size}:${stat.modified.microsecondsSinceEpoch}';
  }

  static String _newKey() {
    final random = Random.secure();
    final bytes = List<int>.generate(16, (_) => random.nextInt(256));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    final h = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${h.substring(0, 8)}-${h.substring(8, 12)}-${h.substring(12, 16)}-${h.substring(16, 20)}-${h.substring(20)}';
  }

  /// Ikili (binary) icerik indirir — yetkili dosya uclari icin (P04: profil fotografi,
  /// odev eki). Auth interceptor'i devrede oldugu icin Authorization basligi otomatik eklenir.
  /// Icerik yoksa (404) `null` doner.
  Future<Uint8List?> getBytes(String path) async {
    try {
      final response = await _dio.get<List<int>>(
        path,
        options: Options(responseType: ResponseType.bytes),
      );
      final data = response.data;
      return data == null ? null : Uint8List.fromList(data);
    } on DioException catch (error) {
      if (error.response?.statusCode == 404) {
        return null;
      }
      throw ApiException.fromDioException(error);
    }
  }

  Future<Map<String, dynamic>> put(
    String path, {
    Map<String, dynamic>? data,
  }) async {
    try {
      final response = await _dio.put<Map<String, dynamic>>(path, data: data);
      return response.data ?? <String, dynamic>{};
    } on DioException catch (error) {
      throw ApiException.fromDioException(error);
    }
  }

  Future<Map<String, dynamic>> delete(
    String path, {
    Map<String, dynamic>? data,
  }) async {
    try {
      final response = await _dio.delete<Map<String, dynamic>>(
        path,
        data: data,
      );
      return response.data ?? <String, dynamic>{};
    } on DioException catch (error) {
      throw ApiException.fromDioException(error);
    }
  }

  Future<Map<String, dynamic>> get(
    String path, {
    Map<String, dynamic>? queryParameters,
  }) async {
    try {
      final response = await _dio.get<Map<String, dynamic>>(
        path,
        queryParameters: queryParameters,
      );
      return response.data ?? <String, dynamic>{};
    } on DioException catch (error) {
      throw ApiException.fromDioException(error);
    }
  }

  Future<List<dynamic>> getList(
    String path, {
    Map<String, dynamic>? queryParameters,
  }) async {
    try {
      final response = await _dio.get<List<dynamic>>(
        path,
        queryParameters: queryParameters,
      );
      return response.data ?? <dynamic>[];
    } on DioException catch (error) {
      throw ApiException.fromDioException(error);
    }
  }
}
