import 'dart:async';
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
  }) : _dio = dio {
    // İsteklere Bearer token ekle
    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          final token = await tokenStorage.readAccessToken();
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
        ),
      );
    }
  }

  final Dio _dio;
  final StreamController<void> _unauthorizedController =
      StreamController<void>.broadcast();

  Stream<void> get unauthorizedEvents => _unauthorizedController.stream;

  void _fireUnauthorized() {
    if (!_unauthorizedController.isClosed) {
      _unauthorizedController.add(null);
    }
  }

  void dispose() {
    _unauthorizedController.close();
  }

  Future<Map<String, dynamic>> post(
    String path, {
    Map<String, dynamic>? data,
  }) async {
    try {
      final response = await _dio.post<Map<String, dynamic>>(path, data: data);
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
  }) async {
    try {
      final formData = FormData.fromMap(<String, dynamic>{
        field: await MultipartFile.fromFile(filePath),
      });
      final response = await _dio.post<Map<String, dynamic>>(
        path,
        data: formData,
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
  }) async {
    try {
      final payload = <String, dynamic>{...fields};
      if (filePath != null) {
        payload[field] = await MultipartFile.fromFile(filePath);
      }
      final response = await _dio.post<Map<String, dynamic>>(
        path,
        data: FormData.fromMap(payload),
      );
      return response.data ?? <String, dynamic>{};
    } on DioException catch (error) {
      throw ApiException.fromDioException(error);
    }
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
