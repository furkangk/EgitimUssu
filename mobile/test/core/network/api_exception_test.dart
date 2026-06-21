import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
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
}
