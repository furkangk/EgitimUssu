import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';
import 'package:dio/dio.dart';
import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/assignments/data/repositories/assignment_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/messaging/data/repositories/messaging_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/scheduling/data/repositories/scheduling_repository_impl.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers.dart';

void main() {
  test(
    'submission, message and lesson repositories carry distinct write keys',
    () async {
      final adapter = _Adapter();
      final dio = Dio(BaseOptions(baseUrl: 'https://test'))
        ..httpClientAdapter = adapter;
      final api = ApiClient(dio: dio, tokenStorage: InMemoryTokenStorage());
      addTearDown(() {
        api.dispose();
        dio.close();
      });
      const config = AppConfig(
        apiBaseUrl: 'https://test',
        appEnvironment: 'test',
        useMockFallback: false,
        mockFallbackFeatures: {},
      );
      final dir = await Directory.systemTemp.createTemp(
        'egitimussu-idempotency-',
      );
      addTearDown(() => dir.delete(recursive: true));
      final file = await File(
        '${dir.path}/assignment.txt',
      ).writeAsString('Original submission');
      await AssignmentRepositoryImpl(
        apiClient: api,
        config: config,
      ).submitWork('a', file.path);
      await MessagingRepositoryImpl(
        apiClient: api,
      ).send(conversationId: 'c', body: 'Hello');
      await SchedulingRepositoryImpl(
        apiClient: api,
        config: config,
        localCache: InMemoryLocalCache(),
      ).createLesson(
        LessonSchedule(
          id: '',
          teacherUserId: 't',
          studentId: 's',
          subject: 'Math',
          lessonFormat: 'Online',
          startAtUtc: DateTime.utc(2026, 11, 20, 12),
          endAtUtc: DateTime.utc(2026, 11, 20, 13),
          timeZone: 'Europe/Istanbul',
        ),
      );
      expect(adapter.requests.map((o) => o.path), [
        '/api/assignments/a/submission',
        '/api/messaging/conversations/c/messages',
        '/api/scheduling/lessons',
      ]);
      final keys = adapter.requests
          .map((o) => o.headers['Idempotency-Key'])
          .toList();
      expect(keys, everyElement(isNotEmpty));
      expect(keys.toSet(), hasLength(3));
      expect(adapter.submission, contains('Original submission'));
    },
  );
}

class _Adapter implements HttpClientAdapter {
  final requests = <RequestOptions>[];
  String submission = '';
  @override
  Future<ResponseBody> fetch(
    RequestOptions o,
    Stream<Uint8List>? stream,
    Future<void>? cancel,
  ) async {
    requests.add(o);
    if (stream != null) {
      final body = String.fromCharCodes(await stream.expand((e) => e).toList());
      if (o.path.endsWith('/submission')) submission = body;
    }
    return ResponseBody.fromString(
      o.path == '/api/scheduling/lessons' ? jsonEncode(o.data) : '{}',
      200,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}
