import 'dart:typed_data';

import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/data/models/teacher_profile_model.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/data/repositories/teacher_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:flutter_test/flutter_test.dart';

/// P04 Task 3 (M02-2/D-05): Profil fotoğrafı yükleme/indirme sözleşmesi.
/// Kritik: fotoğraf **profil gövdesinde gönderilmez** (sunucu istemciden yazmaya izin vermiyor).
void main() {
  test('uploadProfilePhoto multipart ucuna gider ve indirme rotasini doner', () async {
    final apiClient = _RecordingApiClient(
      fileResponse: {'photoUrl': '/api/teachers/profiles/t-1/photo'},
    );
    final repository = TeacherRepositoryImpl(
      apiClient: apiClient,
      config: _config(),
    );

    final url = await repository.uploadProfilePhoto(
      userId: 't-1',
      filePath: '/tmp/foto.png',
    );

    expect(url, '/api/teachers/profiles/t-1/photo');
    expect(apiClient.postedFilePath, '/api/teachers/profiles/t-1/photo');
    expect(apiClient.postedLocalFile, '/tmp/foto.png');
  });

  test('yanit photoUrl tasimazsa rota yerel olarak turetilir', () async {
    final apiClient = _RecordingApiClient(fileResponse: const {});
    final repository = TeacherRepositoryImpl(apiClient: apiClient, config: _config());

    expect(
      await repository.uploadProfilePhoto(userId: 't-1', filePath: '/tmp/foto.png'),
      '/api/teachers/profiles/t-1/photo',
    );
  });

  test('downloadProfilePhoto baytlari yetkili ucdan alir', () async {
    final bytes = Uint8List.fromList([1, 2, 3]);
    final apiClient = _RecordingApiClient(bytesResponse: bytes);
    final repository = TeacherRepositoryImpl(apiClient: apiClient, config: _config());

    expect(await repository.downloadProfilePhoto('t-1'), bytes);
    expect(apiClient.requestedBytesPath, '/api/teachers/profiles/t-1/photo');
  });

  test('fotograf yoksa null doner', () async {
    final repository = TeacherRepositoryImpl(
      apiClient: _RecordingApiClient(),
      config: _config(),
    );

    expect(await repository.downloadProfilePhoto('t-1'), isNull);
  });

  test('profil gövdesi profilePhotoUrl tasimaz', () {
    final payload = _model().toCreatePayload();

    expect(payload.containsKey('profilePhotoUrl'), isFalse);
  });
}

AppConfig _config() => const AppConfig(
  apiBaseUrl: 'http://localhost:5296',
  appEnvironment: 'development',
  useMockFallback: false,
  mockFallbackFeatures: <String>{},
);

TeacherProfileModel _model() => TeacherProfileModel(
  id: 'p-1',
  userId: 't-1',
  fullName: 'Ayse Yilmaz',
  subject: 'Matematik',
  city: 'Istanbul',
  district: 'Kadikoy',
  lessonFormat: 'Online',
  experienceYears: 5,
  educationLevel: 'Lisans',
  hourlyRateAmount: 500,
  currency: 'TRY',
  availabilitySlots: const <TeacherAvailability>[],
);

class _RecordingApiClient implements ApiClient {
  _RecordingApiClient({this.fileResponse, this.bytesResponse});

  final Map<String, dynamic>? fileResponse;
  final Uint8List? bytesResponse;

  String? postedFilePath;
  String? postedLocalFile;
  String? requestedBytesPath;

  @override
  Future<Map<String, dynamic>> postFile(
    String path, {
    required String filePath,
    String field = 'file',
  }) async {
    postedFilePath = path;
    postedLocalFile = filePath;
    return fileResponse ?? <String, dynamic>{};
  }

  @override
  Future<Uint8List?> getBytes(String path) async {
    requestedBytesPath = path;
    return bytesResponse;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
