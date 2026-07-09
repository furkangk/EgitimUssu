import 'dart:convert';

import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/features/scheduling/data/models/lesson_schedule_model.dart';
import 'package:egitim_ussu_mobile/features/scheduling/data/models/study_schedule_models.dart';
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
      meetingUrl: lessonSchedule.meetingUrl,
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
  Future<LessonSchedule> updateLesson(LessonSchedule lessonSchedule) async {
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
      meetingUrl: lessonSchedule.meetingUrl,
      notes: lessonSchedule.notes,
    );
    try {
      final response = await _apiClient.put(
        '/api/scheduling/lessons/${lessonSchedule.id}',
        data: model.toUpdatePayload(),
      );
      return LessonScheduleModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('scheduling')) {
        return model;
      }
      rethrow;
    }
  }

  @override
  Future<LessonSchedule> getLesson(String lessonId) async {
    if (_config.isMockFallbackEnabled('scheduling')) {
      final now = DateTime.now().toUtc();
      return _mockLessons('mock-teacher-user', now).firstWhere(
        (l) => l.id == lessonId,
        orElse: () => LessonScheduleModel.demo(
          teacherUserId: 'mock-teacher-user',
          studentId: 'student-1',
          id: lessonId,
          subject: 'Matematik',
          startAtUtc: now.add(const Duration(hours: 3)),
          endAtUtc: now.add(const Duration(hours: 4)),
        ),
      );
    }
    try {
      final response = await _apiClient.get(
        '/api/scheduling/lessons/$lessonId',
      );
      return LessonScheduleModel.fromJson(response);
    } on ApiException {
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
  Future<LessonSchedule> completeLesson({required String lessonId}) async {
    try {
      final response = await _apiClient.post(
        '/api/scheduling/lessons/$lessonId/complete',
        data: <String, dynamic>{},
      );
      return LessonScheduleModel.fromJson(response);
    } on ApiException {
      if (_config.isMockFallbackEnabled('scheduling')) {
        final now = DateTime.now().toUtc();
        return LessonScheduleModel(
          id: lessonId,
          teacherUserId: 'mock-teacher-user',
          studentId: 'student-1',
          subject: 'Tamamlanan ders',
          lessonFormat: 'Online',
          startAtUtc: now,
          endAtUtc: now.add(const Duration(hours: 1)),
          timeZone: 'Europe/Istanbul',
          status: 'Completed',
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
    if (_config.isMockFallbackEnabled('scheduling')) {
      return _mockLessons(teacherUserId, DateTime.now().toUtc());
    }
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
      if (cached.isNotEmpty) return cached;
      rethrow;
    }
  }

  @override
  Future<List<LessonSchedule>> listStudentLessons({
    required String studentId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
  }) async {
    if (_config.isMockFallbackEnabled('scheduling')) {
      return _mockStudentLessons(DateTime.now().toUtc());
    }
    final start =
        (startAtUtc ?? DateTime.now().toUtc().subtract(const Duration(days: 30)))
            .toIso8601String();
    final end =
        (endAtUtc ?? DateTime.now().toUtc().add(const Duration(days: 30)))
            .toIso8601String();
    final cacheKey = 'scheduling.studentLessons.$studentId.$start.$end';
    try {
      final response = await _apiClient.getList(
        '/api/scheduling/students/$studentId/lessons',
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
      if (cached.isNotEmpty) return cached;
      rethrow;
    }
  }

  @override
  Future<List<CalendarOccurrence>> getStudentCalendar({
    required String studentId,
    required DateTime startAtUtc,
    required DateTime endAtUtc,
  }) async {
    if (_config.isMockFallbackEnabled('scheduling')) {
      return _mockCalendar(DateTime.now().toUtc());
    }
    try {
      final response = await _apiClient.getList(
        '/api/scheduling/students/$studentId/calendar',
        queryParameters: <String, dynamic>{
          'startAtUtc': startAtUtc.toIso8601String(),
          'endAtUtc': endAtUtc.toIso8601String(),
        },
      );
      return response
          .whereType<Map<String, dynamic>>()
          .map(CalendarOccurrenceModel.fromJson)
          .toList();
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<StudyScheduleEntry> createStudyEntry({
    required String studentId,
    required StudyScheduleEntry entry,
  }) async {
    final response = await _apiClient.post(
      '/api/scheduling/students/$studentId/study-entries',
      data: StudyScheduleEntryModel.toPayload(entry),
    );
    return StudyScheduleEntryModel.fromJson(response);
  }

  @override
  Future<StudyScheduleEntry> updateStudyEntry(StudyScheduleEntry entry) async {
    final response = await _apiClient.put(
      '/api/scheduling/study-entries/${entry.id}',
      data: StudyScheduleEntryModel.toPayload(entry),
    );
    return StudyScheduleEntryModel.fromJson(response);
  }

  @override
  Future<void> deleteStudyEntry(String entryId) async {
    await _apiClient.delete('/api/scheduling/study-entries/$entryId');
  }

  List<CalendarOccurrence> _mockCalendar(DateTime now) {
    final today = DateTime.utc(now.year, now.month, now.day);
    return <CalendarOccurrence>[
      CalendarOccurrenceModel(
        entryId: 'mock-lesson-1',
        source: 'Teacher',
        subject: 'Fizik',
        startAtUtc: today.add(const Duration(days: 1, hours: 20)),
        endAtUtc: today.add(const Duration(days: 1, hours: 21, minutes: 30)),
        lessonFormat: 'Online',
        locationLabel: 'Zoom',
        isEditable: false,
      ),
      CalendarOccurrenceModel(
        entryId: 'mock-self-1',
        source: 'Self',
        subject: 'Matematik',
        topic: 'Türev',
        startAtUtc: today.add(const Duration(hours: 15)),
        endAtUtc: today.add(const Duration(hours: 16)),
        colorHex: '#20A4A9',
        recurrenceRule: 'FREQ=WEEKLY;BYDAY=MO',
        isEditable: true,
      ),
    ];
  }

  List<LessonSchedule> _mockLessons(String teacherUserId, DateTime now) {
    final today = DateTime.utc(now.year, now.month, now.day);
    return [
      // Bugün — 3 ders
      LessonScheduleModel.demo(
        teacherUserId: teacherUserId,
        studentId: 'student-1',
        id: 'lesson-1',
        subject: 'Matematik',
        startAtUtc: today.add(const Duration(hours: 12, minutes: 30)),
        endAtUtc: today.add(const Duration(hours: 13, minutes: 30)),
      ),
      LessonScheduleModel(
        id: 'lesson-2',
        teacherUserId: teacherUserId,
        studentId: 'student-2',
        subject: 'Fizik',
        lessonFormat: 'InPerson',
        startAtUtc: today.add(const Duration(hours: 15)),
        endAtUtc: today.add(const Duration(hours: 16, minutes: 30)),
        timeZone: 'Europe/Istanbul',
        locationLabel: 'Çalışma odası',
        status: 'Planned',
      ),
      LessonScheduleModel.demo(
        teacherUserId: teacherUserId,
        studentId: 'student-4',
        id: 'lesson-3',
        subject: 'Türkçe',
        startAtUtc: today.add(const Duration(hours: 18)),
        endAtUtc: today.add(const Duration(hours: 19)),
      ),
      // Yaklaşan — 4 ders
      LessonScheduleModel.demo(
        teacherUserId: teacherUserId,
        studentId: 'student-3',
        id: 'lesson-4',
        subject: 'Biyoloji',
        startAtUtc: today.add(const Duration(days: 1, hours: 10)),
        endAtUtc: today.add(const Duration(days: 1, hours: 11)),
      ),
      LessonScheduleModel.demo(
        teacherUserId: teacherUserId,
        studentId: 'student-5',
        id: 'lesson-5',
        subject: 'TYT Sayısal',
        startAtUtc: today.add(const Duration(days: 2, hours: 14)),
        endAtUtc: today.add(const Duration(days: 2, hours: 16)),
      ),
      LessonScheduleModel.demo(
        teacherUserId: teacherUserId,
        studentId: 'student-1',
        id: 'lesson-6',
        subject: 'Geometri',
        startAtUtc: today.add(const Duration(days: 4, hours: 11)),
        endAtUtc: today.add(const Duration(days: 4, hours: 12)),
      ),
      LessonScheduleModel.demo(
        teacherUserId: teacherUserId,
        studentId: 'student-3',
        id: 'lesson-7',
        subject: 'Kimya',
        startAtUtc: today.add(const Duration(days: 5, hours: 16)),
        endAtUtc: today.add(const Duration(days: 5, hours: 17)),
      ),
      // Geçmiş — 2 ders
      LessonScheduleModel.demo(
        teacherUserId: teacherUserId,
        studentId: 'student-2',
        id: 'lesson-8',
        subject: 'Matematik',
        startAtUtc: today.subtract(const Duration(hours: 15)),
        endAtUtc: today.subtract(const Duration(hours: 14)),
      ),
      LessonScheduleModel(
        id: 'lesson-9',
        teacherUserId: teacherUserId,
        studentId: 'student-5',
        subject: 'Fizik',
        lessonFormat: 'Online',
        startAtUtc: today.subtract(const Duration(hours: 34)),
        endAtUtc: today.subtract(const Duration(hours: 33)),
        timeZone: 'Europe/Istanbul',
        status: 'Completed',
      ),
    ];
  }

  /// Öğrenci görünümü için mock dersler — **iki** öğretmene dağıtılmış
  /// (`mock-teacher-1`: sayısal, `mock-teacher-2`: fen) ki "Öğretmenlerim" ekranında
  /// iki farklı öğretmen görünsün. `teacher_profile` mock fallback'ı bu id'leri farklı
  /// demo öğretmenlere eşler (`TeacherProfileModel.demo`).
  List<LessonSchedule> _mockStudentLessons(DateTime now) {
    const Map<String, String> teacherBySubject = <String, String>{
      'Matematik': 'mock-teacher-1',
      'Geometri': 'mock-teacher-1',
      'TYT Sayısal': 'mock-teacher-1',
      'Türkçe': 'mock-teacher-1',
      'Fizik': 'mock-teacher-2',
      'Kimya': 'mock-teacher-2',
      'Biyoloji': 'mock-teacher-2',
    };
    return _mockLessons('mock-teacher-1', now).map((LessonSchedule l) {
      return LessonScheduleModel(
        id: l.id,
        teacherUserId: teacherBySubject[l.subject] ?? 'mock-teacher-1',
        studentId: l.studentId,
        subject: l.subject,
        lessonFormat: l.lessonFormat,
        startAtUtc: l.startAtUtc,
        endAtUtc: l.endAtUtc,
        timeZone: l.timeZone,
        status: l.status,
        recurrenceRule: l.recurrenceRule,
        reminderOffsetMinutes: l.reminderOffsetMinutes,
        locationLabel: l.locationLabel,
        meetingUrl: l.meetingUrl,
        notes: l.notes,
      );
    }).toList();
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
