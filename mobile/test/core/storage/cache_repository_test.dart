import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/cache_store.dart';
import 'package:egitim_ussu_mobile/features/payments/data/repositories/payment_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/scheduling/data/repositories/scheduling_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/students/data/repositories/student_repository_impl.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers.dart';

void main() {
  const config = AppConfig(
    apiBaseUrl: 'http://localhost',
    appEnvironment: 'development',
    useMockFallback: false,
    mockFallbackFeatures: {},
  );
  for (final feature in [
    'summary',
    'records',
    'students',
    'teacherLessons',
    'studentLessons',
  ]) {
    for (final failure in ['403', '500', 'cancel', 'certificate', 'offline']) {
      test(
        '$feature: $failure ${failure == 'offline' ? 'uses marked cache' : 'must not return cache'}',
        () async {
          final local = InMemoryLocalCache();
          final store = CacheStore(local)..activate('signed-in-user');
          final adapter = _Adapter();
          final api = ApiClient(
            dio: Dio()..httpClientAdapter = adapter,
            tokenStorage: InMemoryTokenStorage(),
          );
          addTearDown(api.dispose);
          final payments = PaymentRepositoryImpl(
            apiClient: api,
            config: config,
            localCache: local,
            cacheStore: store,
          );
          final students = StudentRepositoryImpl(
            apiClient: api,
            config: config,
            localCache: local,
            cacheStore: store,
          );
          final lessons = SchedulingRepositoryImpl(
            apiClient: api,
            config: config,
            localCache: local,
            cacheStore: store,
          );
          Future<Object> call() => switch (feature) {
            'summary' => payments.getSummary('teacher'),
            'records' => payments.listTeacherRecords('teacher'),
            'students' => students.listByTeacher('teacher'),
            'teacherLessons' => lessons.listTeacherLessons(
              teacherUserId: 'teacher',
              startAtUtc: DateTime.utc(2026, 11, 15, 1),
              endAtUtc: DateTime.utc(2026, 11, 16, 1),
            ),
            _ => lessons.listStudentLessons(
              studentId: 'student',
              startAtUtc: DateTime.utc(2026, 11, 15, 1),
              endAtUtc: DateTime.utc(2026, 11, 16, 1),
            ),
          };
          await call();
          adapter.failure = failure;
          if (failure == 'offline') {
            await call();
            expect(store.isStale, isTrue);
            expect(store.staleSince.value, isNotNull);
            expect(
              (await local.keys()).every(
                (k) => k.startsWith('cache.signed-in-user.'),
              ),
              isTrue,
            );
          } else {
            await expectLater(call(), throwsA(isA<ApiException>()));
            expect(store.isStale, isFalse);
          }
        },
      );
    }
  }
  test('calendar rounds request and cache to stable UTC day bounds', () async {
    final local = InMemoryLocalCache();
    final store = CacheStore(local)..activate('alice');
    final adapter = _Adapter();
    final api = ApiClient(
      dio: Dio()..httpClientAdapter = adapter,
      tokenStorage: InMemoryTokenStorage(),
    );
    addTearDown(api.dispose);
    final repo = SchedulingRepositoryImpl(
      apiClient: api,
      config: config,
      localCache: local,
      cacheStore: store,
    );
    for (final hour in [2, 9]) {
      await repo.listTeacherLessons(
        teacherUserId: 'teacher',
        startAtUtc: DateTime.utc(2026, 11, 15, hour),
        endAtUtc: DateTime.utc(2026, 11, 16, hour),
      );
    }
    expect(await local.keys(), hasLength(1));
    expect(
      adapter.requests.first.queryParameters,
      adapter.requests.last.queryParameters,
    );
    expect(
      adapter.requests.last.queryParameters['startAtUtc'],
      '2026-11-15T00:00:00.000Z',
    );
    expect(
      adapter.requests.last.queryParameters['endAtUtc'],
      '2026-11-16T23:59:59.999999Z',
    );
  });
}

class _Adapter implements HttpClientAdapter {
  String? failure;
  final requests = <RequestOptions>[];
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    final type = switch (failure) {
      'offline' => DioExceptionType.connectionError,
      'cancel' => DioExceptionType.cancel,
      'certificate' => DioExceptionType.badCertificate,
      _ => null,
    };
    if (type != null) throw DioException(requestOptions: options, type: type);
    final code = int.tryParse(failure ?? '') ?? 200;
    final data = code != 200
        ? {'message': 'denied'}
        : options.path.endsWith('/summary')
        ? {
            'expectedTotal': 100,
            'collectedTotal': 20,
            'outstandingTotal': 80,
            'overdueTotal': 0,
            'monthlyBreakdown': [],
          }
        : options.path.endsWith('/records')
        ? [
            {
              'id': 'record',
              'teacherUserId': 'teacher',
              'studentId': 'student',
              'description': 'Fee',
              'currency': 'TRY',
              'expectedAmount': 100,
              'collectedAmount': 20,
              'outstandingAmount': 80,
              'status': 'Pending',
              'itemType': 'LessonFee',
              'isOverdue': false,
            },
          ]
        : options.path.contains('/students/profiles')
        ? [
            {
              'id': 'student',
              'fullName': 'Alice student',
              'gradeLevel': '8',
              'origin': 'TeacherManaged',
              'isActive': true,
            },
          ]
        : [
            {
              'id': 'lesson',
              'teacherUserId': 'teacher',
              'studentId': 'student',
              'subject': 'Math',
              'lessonFormat': 'Online',
              'startAtUtc': '2026-11-15T10:00:00Z',
              'endAtUtc': '2026-11-15T11:00:00Z',
              'timeZone': 'Europe/Istanbul',
              'status': 'Planned',
            },
          ];
    return ResponseBody.fromString(
      jsonEncode(data),
      code,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
