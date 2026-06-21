import 'dart:convert';

import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/features/scheduling/data/models/lesson_schedule_model.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';

class SchedulingRepositoryImpl implements SchedulingRepository {
  SchedulingRepositoryImpl({
    required ApiClient apiClient,
    required AppConfig config,
    required LocalCache localCache,
  }) : _apiClient = apiClient,
       _config = config,
       _localCache = localCache;

  final ApiClient _apiClient;
  final AppConfig _config;
  final LocalCache _localCache;

  @override
  Future<LessonSchedule> createLesson(LessonSchedule lessonSchedule) async {
    final model = LessonScheduleModel(
      id: lessonSchedule.id,
      teacherUserId: lessonSchedule.teacherUserId,
      studentId: lessonSchedule.studentId,
      subject: lessonSchedule.subject,
      lessonFormat: lessonSchedule.lessonFormat,
      startAtUtc: lessonSchedule.startAtUtc,
      endAtUtc: lessonSchedule.endAtUtc,
      timeZone: lessonSchedule.timeZone,
      status: lessonSchedule.status,
      recurrenceRule: lessonSchedule.recurrenceRule,
      reminderOffsetMinutes: lessonSchedule.reminderOffsetMinutes,
      locationLabel: lessonSchedule.locationLabel,
      notes: lessonSchedule.notes,
    );
    try {
      final response = await _apiClient.post(
        '/api/scheduling/lessons',
        data: model.toCreatePayload(),
      );
      return LessonScheduleModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('scheduling')) {
        return LessonScheduleModel.demo(
          teacherUserId: lessonSchedule.teacherUserId,
          studentId: lessonSchedule.studentId,
          id: DateTime.now().microsecondsSinceEpoch.toString(),
          subject: lessonSchedule.subject,
          startAtUtc: lessonSchedule.startAtUtc,
          endAtUtc: lessonSchedule.endAtUtc,
        );
      }
      rethrow;
    }
  }

  @override
  Future<LessonSchedule> getLesson(String lessonId) async {
    try {
      final response = await _apiClient.get(
        '/api/scheduling/lessons/$lessonId',
      );
      return LessonScheduleModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('scheduling')) {
        final now = DateTime.now().toUtc();
        return LessonScheduleModel.demo(
          teacherUserId: 'mock-teacher-user',
          studentId: 'student-1',
          id: lessonId,
          subject: 'Matematik',
          startAtUtc: now.add(const Duration(hours: 3)),
          endAtUtc: now.add(const Duration(hours: 4)),
        );
      }
      rethrow;
    }
  }

  @override
  Future<LessonSchedule> cancelLesson({
    required String lessonId,
    String? cancellationNote,
  }) async {
    try {
      final response = await _apiClient.post(
        '/api/scheduling/lessons/$lessonId/cancel',
        data: LessonScheduleModel(
          id: lessonId,
          teacherUserId: '',
          studentId: '',
          subject: '',
          lessonFormat: 'Online',
          startAtUtc: DateTime.now().toUtc(),
          endAtUtc: DateTime.now().toUtc(),
          timeZone: 'Europe/Istanbul',
        ).toCancelPayload(cancellationNote),
      );
      return LessonScheduleModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('scheduling')) {
        final now = DateTime.now().toUtc();
        return LessonScheduleModel(
          id: lessonId,
          teacherUserId: 'mock-teacher-user',
          studentId: 'student-1',
          subject: 'Iptal edilen ders',
          lessonFormat: 'Online',
          startAtUtc: now,
          endAtUtc: now.add(const Duration(hours: 1)),
          timeZone: 'Europe/Istanbul',
          status: 'Cancelled',
          notes: cancellationNote,
        );
      }
      rethrow;
    }
  }

  @override
  Future<List<LessonSchedule>> listTeacherLessons({
    required String teacherUserId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
  }) async {
    final start =
        (startAtUtc ?? DateTime.now().toUtc().subtract(const Duration(days: 7)))
            .toIso8601String();
    final end =
        (endAtUtc ?? DateTime.now().toUtc().add(const Duration(days: 14)))
            .toIso8601String();
    final cacheKey = 'scheduling.lessons.$teacherUserId.$start.$end';
    try {
      final response = await _apiClient.getList(
        '/api/scheduling/teachers/$teacherUserId/lessons',
        queryParameters: <String, dynamic>{
          'startAtUtc': start,
          'endAtUtc': end,
        },
      );
      await _localCache.writeString(cacheKey, jsonEncode(response));
      return response
          .whereType<Map<String, dynamic>>()
          .map(LessonScheduleModel.fromJson)
          .toList();
    } on ApiException {
      final cached = await _readCachedLessons(cacheKey);
      if (cached.isNotEmpty) {
        return cached;
      }
      if (_config.isMockFallbackEnabled('scheduling')) {
        final now = DateTime.now().toUtc();
        return <LessonSchedule>[
          LessonScheduleModel.demo(
            teacherUserId: teacherUserId,
            studentId: 'student-1',
            id: 'lesson-1',
            subject: 'Matematik',
            startAtUtc: now.add(const Duration(hours: 3)),
            endAtUtc: now.add(const Duration(hours: 4)),
          ),
          LessonScheduleModel.demo(
            teacherUserId: teacherUserId,
            studentId: 'student-2',
            id: 'lesson-2',
            subject: 'Geometri',
            startAtUtc: now.add(const Duration(days: 1, hours: 2)),
            endAtUtc: now.add(const Duration(days: 1, hours: 3)),
          ),
        ];
      }
      rethrow;
    }
  }

  Future<List<LessonSchedule>> _readCachedLessons(String cacheKey) async {
    final cached = await _localCache.readString(cacheKey);
    if (cached == null || cached.isEmpty) {
      return const <LessonSchedule>[];
    }
    final decoded = jsonDecode(cached);
    if (decoded is! List<dynamic>) {
      return const <LessonSchedule>[];
    }
    return decoded
        .whereType<Map<String, dynamic>>()
        .map(LessonScheduleModel.fromJson)
        .toList();
  }
}
