import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/data/repositories/teacher_repository_impl.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  const missing = ApiException(
    message: 'Profil bulunamadı.',
    statusCode: 404,
    code: 'teachers.profile_not_found',
  );

  for (final environment in ['development', 'production']) {
    test('$environment: mock kapalı 404 hatası demo yerine korunur', () async {
      final api = _ProfileApi(error: missing);
      final repository = TeacherRepositoryImpl(
        apiClient: api,
        config: _config(environment: environment),
      );
      await expectLater(
        repository.getProfile('teacher-user'),
        throwsA(same(missing)),
      );
      expect(api.requestedPath, '/api/teachers/profiles/teacher-user');
    });
  }

  test('yalnız açık development mock izni demo profile düşer', () async {
    final repository = TeacherRepositoryImpl(
      apiClient: _ProfileApi(error: missing),
      config: _config(mock: true),
    );
    final profile = await repository.getProfile('teacher-user');
    expect(profile.id, startsWith('demo-teacher-'));
    expect(profile.userId, 'teacher-user');
  });

  test(
    'production ortamı açık mock bayrağında da 404 hatasını korur',
    () async {
      final repository = TeacherRepositoryImpl(
        apiClient: _ProfileApi(error: missing),
        config: _config(mock: true, environment: 'production'),
      );
      await expectLater(
        repository.getProfile('teacher-user'),
        throwsA(same(missing)),
      );
    },
  );

  test('başarılı gerçek profil mock kapalıyken aynen ayrıştırılır', () async {
    final api = _ProfileApi(
      profile: {
        'id': 'real-profile',
        'userId': 'teacher-user',
        'fullName': 'Gerçek Öğretmen',
        'subject': 'Matematik',
        'lessonFormat': 'Online',
        'experienceYears': 6,
        'hourlyRateAmount': 750,
        'currency': 'TRY',
        'availabilitySlots': <dynamic>[],
      },
    );
    final repository = TeacherRepositoryImpl(apiClient: api, config: _config());
    final profile = await repository.getProfile('teacher-user');
    expect(profile.id, 'real-profile');
    expect(profile.fullName, 'Gerçek Öğretmen');
    expect(profile.hourlyRateAmount, 750);
    expect(profile.availabilitySlots, isEmpty);
  });
}

AppConfig _config({bool mock = false, String environment = 'development'}) =>
    AppConfig(
      apiBaseUrl: 'http://localhost',
      appEnvironment: environment,
      useMockFallback: mock,
      mockFallbackFeatures: {'teacher_profile'},
    );

class _ProfileApi implements ApiClient {
  _ProfileApi({this.error, this.profile = const {}});
  final ApiException? error;
  final Map<String, dynamic> profile;
  String? requestedPath;

  @override
  Future<Map<String, dynamic>> get(
    String path, {
    Map<String, dynamic>? queryParameters,
  }) async {
    requestedPath = path;
    if (error != null) throw error!;
    return profile;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
