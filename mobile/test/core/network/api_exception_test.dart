import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('maps conflict payload into ApiException', () {
    final exception = DioException(
      requestOptions: RequestOptions(path: '/api/scheduling/lessons'),
      response: Response<dynamic>(
        requestOptions: RequestOptions(path: '/api/scheduling/lessons'),
        statusCode: 409,
        data: <String, dynamic>{
          'code': 'scheduling.lesson_conflict',
          'message': 'Ders saati cakisiyor.',
        },
      ),
    );

    final apiException = ApiException.fromDioException(exception);

    expect(apiException.statusCode, 409);
    expect(apiException.code, 'scheduling.lesson_conflict');
    expect(apiException.message, 'Ders saati cakisiyor.');
    expect(apiException.isConflict, isTrue);
  });

  test('maps validation problem details into the shared validation kind', () {
    final exception = DioException(
      requestOptions: RequestOptions(path: '/api/students/profiles'),
      response: Response<dynamic>(
        requestOptions: RequestOptions(path: '/api/students/profiles'),
        statusCode: 400,
        data: <String, dynamic>{
          'title': 'Validation failed',
          'errors': <String, dynamic>{
            'fullName': <String>['Ogrenci adi zorunlu.'],
            'gradeLevel': <String>['Sinif bilgisi zorunlu.'],
          },
        },
      ),
    );

    final apiException = ApiException.fromDioException(exception);

    expect(apiException.kind, ApiErrorKind.validation);
    expect(apiException.isValidation, isTrue);
    expect(apiException.validationErrors['fullName'], <String>[
      'Ogrenci adi zorunlu.',
    ]);
    expect(apiException.message, contains('Ogrenci adi zorunlu.'));
    expect(apiException.message, contains('Sinif bilgisi zorunlu.'));
  });

  for (final entry in <DioExceptionType, String>{
    DioExceptionType.receiveTimeout: 'Sunucu yanıtı zaman aşımına uğradı.',
    DioExceptionType.sendTimeout: 'İstek gönderimi zaman aşımına uğradı.',
    DioExceptionType.connectionError: 'Sunucuya bağlanılamadı.',
    DioExceptionType.unknown: 'Beklenmeyen bir API hatası oluştu.',
  }.entries) {
    test('${entry.key} Türkçe fallback doğru yazılır', () {
      final error = ApiException.fromDioException(
        DioException(requestOptions: RequestOptions(), type: entry.key),
        locale: const Locale('tr'),
      );
      expect(error.message, entry.value);
    });
  }
  test('istemci fallback mesajı seçilen dile uyar', () {
    final error = ApiException.fromDioException(
      DioException(
        requestOptions: RequestOptions(),
        type: DioExceptionType.connectionError,
      ),
      locale: const Locale('en'),
    );
    expect(error.message, 'Unable to connect to the server.');
  });
  test('sunucu ve doğrulama mesajları değiştirilmez', () {
    final options = RequestOptions();
    for (final data in <Map<String, dynamic>>[
      {'message': 'Sunucu açıklaması'},
      {
        'errors': {
          'name': ['Alan gerekli'],
        },
      },
    ]) {
      final error = ApiException.fromDioException(
        DioException(
          requestOptions: options,
          response: Response<dynamic>(
            requestOptions: options,
            statusCode: 422,
            data: data,
          ),
        ),
        locale: const Locale('en'),
      );
      expect(
        error.message,
        data.containsKey('message') ? 'Sunucu açıklaması' : 'Alan gerekli',
      );
    }
  });
}
