import 'package:dio/dio.dart';

enum ApiErrorKind { auth, validation, conflict, network, unknown }

class ApiException implements Exception {
  const ApiException({
    required this.message,
    this.kind = ApiErrorKind.unknown,
    this.code,
    this.statusCode,
    this.validationErrors = const <String, List<String>>{},
  });

  final String message;
  final ApiErrorKind kind;
  final String? code;
  final int? statusCode;
  final Map<String, List<String>> validationErrors;

  bool get isConflict => statusCode == 409;
  bool get isValidation => kind == ApiErrorKind.validation;

  static ApiException fromDioException(DioException exception) {
    final response = exception.response;
    final data = response?.data;
    final statusCode = response?.statusCode;
    if (data is Map<String, dynamic>) {
      final code = data['code']?.toString();
      final validationErrors = _readValidationErrors(data);
      final message =
          _validationMessage(validationErrors) ??
          data['message']?.toString() ??
          data['title']?.toString() ??
          'Beklenmeyen bir API hatasi olustu.';
      return ApiException(
        message: message,
        kind: _kindFor(statusCode, validationErrors),
        code: code,
        statusCode: statusCode,
        validationErrors: validationErrors,
      );
    }

    return ApiException(
      message: switch (exception.type) {
        DioExceptionType.connectionTimeout =>
          'Sunucuya baglanti zaman asimina ugradi.',
        DioExceptionType.receiveTimeout =>
          'Sunucu yaniti zaman asimina ugradi.',
        DioExceptionType.sendTimeout => 'Istek gonderimi zaman asimina ugradi.',
        DioExceptionType.connectionError => 'Sunucuya baglanilamadi.',
        _ => 'Beklenmeyen bir API hatasi olustu.',
      },
      kind: _kindFor(statusCode, const <String, List<String>>{}),
      statusCode: statusCode,
    );
  }

  static ApiErrorKind _kindFor(
    int? statusCode,
    Map<String, List<String>> validationErrors,
  ) {
    if (statusCode == 401 || statusCode == 403) {
      return ApiErrorKind.auth;
    }
    if (statusCode == 400 || statusCode == 422 || validationErrors.isNotEmpty) {
      return ApiErrorKind.validation;
    }
    if (statusCode == 409) {
      return ApiErrorKind.conflict;
    }
    if (statusCode == null || statusCode >= 500) {
      return ApiErrorKind.network;
    }
    return ApiErrorKind.unknown;
  }

  static Map<String, List<String>> _readValidationErrors(
    Map<String, dynamic> data,
  ) {
    final errors = data['errors'];
    if (errors is! Map<String, dynamic>) {
      return const <String, List<String>>{};
    }

    return errors.map((key, value) {
      final messages = switch (value) {
        final List<dynamic> list =>
          list.map((item) => item.toString()).toList(),
        final Object object => <String>[object.toString()],
        _ => <String>[],
      };
      return MapEntry(key, messages);
    });
  }

  static String? _validationMessage(Map<String, List<String>> errors) {
    if (errors.isEmpty) {
      return null;
    }
    final flattened = errors.values.expand((messages) => messages).toList();
    if (flattened.isEmpty) {
      return 'Formdaki alanlari kontrol edip tekrar dene.';
    }
    return flattened.take(3).join('\n');
  }
}
