import 'dart:async';

import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/token_storage.dart';

class ApiClient {
  ApiClient({required Dio dio, required TokenStorage tokenStorage})
    : _dio = dio,
      _tokenStorage = tokenStorage {
    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          final token = await _tokenStorage.readAccessToken();
          if (token != null && token.isNotEmpty) {
            options.headers['Authorization'] = 'Bearer $token';
          }
          handler.next(options);
        },
      ),
    );
  }

  final Dio _dio;
  final TokenStorage _tokenStorage;
  final StreamController<void> _unauthorizedController =
      StreamController<void>.broadcast();

  Stream<void> get unauthorizedEvents => _unauthorizedController.stream;

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
      _notifyUnauthorized(error);
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
      _notifyUnauthorized(error);
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
      _notifyUnauthorized(error);
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
      _notifyUnauthorized(error);
      throw ApiException.fromDioException(error);
    }
  }

  void _notifyUnauthorized(DioException error) {
    if (error.response?.statusCode == 401 &&
        !_unauthorizedController.isClosed) {
      _unauthorizedController.add(null);
    }
  }
}
