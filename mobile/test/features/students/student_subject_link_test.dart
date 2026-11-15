import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/students/data/models/student_model.dart';
import 'package:egitim_ussu_mobile/features/students/data/repositories/student_repository_impl.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers.dart';

/// P06 Task 9 (M03-1/M03-2): Öğrenci branş ekle/sil + veli bağlama sözleşmesi.
/// Kritik: branş silme **kimlikle** yapılır — bu yüzden sunucu yanıtındaki `id` okunmalıdır.
void main() {
  test('brans yanitindaki id okunur', () {
    final subject = StudentSubjectTargetModel.fromJson(<String, dynamic>{
      'id': 'sub-1',
      'subject': 'Matematik',
      'targetLevel': 'LGS',
    });

    expect(subject.id, 'sub-1');
    expect(subject.subject, 'Matematik');
  });

  test('profil govdesi branslari kimlikleriyle birlikte cozer', () {
    final profile = StudentProfileModel.fromJson(<String, dynamic>{
      'id': 'st-1',
      'fullName': 'Ali Veli',
      'gradeLevel': '9',
      'subjects': <dynamic>[
        <String, dynamic>{
          'id': 'sub-1',
          'subject': 'Matematik',
          'targetLevel': 'LGS',
        },
      ],
    });

    expect(profile.subjects.single.id, 'sub-1');
  });

  test('addSubject dogru uca gider ve eklenen bransi doner', () async {
    final apiClient = _FakeApiClient();
    final repository = _repository(apiClient);

    final added = await repository.addSubject(
      studentId: 'st-1',
      subject: 'Fizik',
      targetLevel: 'TYT',
    );

    expect(apiClient.lastPath, '/api/students/profiles/st-1/subjects');
    expect(apiClient.lastData, <String, dynamic>{
      'subject': 'Fizik',
      'targetLevel': 'TYT',
    });
    expect(added.id, 'sub-9');
    expect(added.subject, 'Fizik');
  });

  test('removeSubject brans kimligiyle DELETE eder', () async {
    final apiClient = _FakeApiClient();
    final repository = _repository(apiClient);

    await repository.removeSubject(studentId: 'st-1', subjectId: 'sub-9');

    expect(
      apiClient.lastDeletePath,
      '/api/students/profiles/st-1/subjects/sub-9',
    );
  });

  test('linkParent veli kimligini govdede gonderir', () async {
    final apiClient = _FakeApiClient();
    final repository = _repository(apiClient);

    await repository.linkParent(studentId: 'st-1', parentUserId: 'p-1');

    expect(apiClient.lastPath, '/api/students/profiles/st-1/link-parent');
    expect(apiClient.lastData, <String, dynamic>{'parentUserId': 'p-1'});
  });
}

StudentRepositoryImpl _repository(ApiClient apiClient) => StudentRepositoryImpl(
  apiClient: apiClient,
  config: _config,
  localCache: InMemoryLocalCache(),
);

const _config = AppConfig(
  apiBaseUrl: 'http://localhost',
  appEnvironment: 'test',
  useMockFallback: false,
  mockFallbackFeatures: <String>{},
);

class _FakeApiClient extends ApiClient {
  _FakeApiClient() : super(dio: Dio(), tokenStorage: InMemoryTokenStorage());

  String? lastPath;
  Map<String, dynamic>? lastData;
  String? lastDeletePath;

  @override
  Future<Map<String, dynamic>> post(
    String path, {
    Map<String, dynamic>? data,
    bool idempotent = false,
    String? idempotencyKey,
    String? retryIdentity,
  }) async {
    lastPath = path;
    lastData = data;
    if (path.endsWith('/link-parent')) {
      return <String, dynamic>{
        'studentId': 'st-1',
        'parentUserId': data?['parentUserId'],
        'status': 'Pending',
      };
    }
    return <String, dynamic>{
      'id': 'sub-9',
      'subject': data?['subject'],
      'targetLevel': data?['targetLevel'],
    };
  }

  @override
  Future<Map<String, dynamic>> delete(
    String path, {
    Map<String, dynamic>? data,
  }) async {
    lastDeletePath = path;
    return <String, dynamic>{};
  }
}
