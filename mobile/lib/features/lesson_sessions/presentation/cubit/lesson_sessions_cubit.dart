import 'dart:convert';

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/storage/local_cache.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/data/models/lesson_session_model.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/cubit/lesson_sessions_state.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class LessonSessionsCubit extends Cubit<LessonSessionsState> {
  LessonSessionsCubit({
    required LessonSessionRepository lessonSessionRepository,
    required SchedulingRepository schedulingRepository,
    required LocalCache localCache,
  }) : _lessonSessionRepository = lessonSessionRepository,
       _schedulingRepository = schedulingRepository,
       _localCache = localCache,
       super(const LessonSessionsState());

  final LessonSessionRepository _lessonSessionRepository;
  final SchedulingRepository _schedulingRepository;
  final LocalCache _localCache;

  factory LessonSessionsCubit.create() {
    return LessonSessionsCubit(
      lessonSessionRepository: injector<LessonSessionRepository>(),
      schedulingRepository: injector<SchedulingRepository>(),
      localCache: injector<LocalCache>(),
    );
  }

  Future<void> load(String teacherUserId) async {
    if (isClosed) return;
    emit(state.copyWith(isLoading: true, clearMessages: true));
    try {
      final lessons = await _schedulingRepository.listTeacherLessons(
        teacherUserId: teacherUserId,
      );
      if (isClosed) return;
      final cachedSessions = await _readCachedSessions(teacherUserId);
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          lessons: lessons,
          sessions: cachedSessions,
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      final cachedSessions = await _readCachedSessions(teacherUserId);
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          sessions: cachedSessions,
          errorMessage: error.message,
        ),
      );
    }
  }

  Future<void> startFromLesson(LessonSchedule lesson) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final created = await _lessonSessionRepository.createSession(
        LessonSession(
          id: '',
          lessonScheduleId: lesson.id,
          teacherUserId: lesson.teacherUserId,
          studentId: lesson.studentId,
          subject: lesson.subject,
          status: 'Planned',
          plannedStartAtUtc: lesson.startAtUtc,
          topicTitle: lesson.subject,
        ),
      );
      if (isClosed) return;
      final sessions = <LessonSession>[created, ...state.sessions];
      emit(
        state.copyWith(
          isSaving: false,
          sessions: sessions,
          successMessage: 'Ders oturumu acildi.',
          clearMessages: true,
        ),
      );
      await _writeCachedSessions(lesson.teacherUserId, sessions);
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    }
  }

  Future<void> completeSession({
    required LessonSession session,
    required DateTime actualStartAtUtc,
    required DateTime actualEndAtUtc,
    required String attendanceStatus,
    required String topicTitle,
    required String coveredContent,
    required String teacherNotes,
  }) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final completed = await _lessonSessionRepository.completeSession(
        LessonSession(
          id: session.id,
          lessonScheduleId: session.lessonScheduleId,
          teacherUserId: session.teacherUserId,
          studentId: session.studentId,
          subject: session.subject,
          status: session.status,
          plannedStartAtUtc: session.plannedStartAtUtc,
          actualStartAtUtc: actualStartAtUtc,
          actualEndAtUtc: actualEndAtUtc,
          attendanceStatus: attendanceStatus,
          topicTitle: topicTitle,
          coveredContent: coveredContent,
          teacherNotes: teacherNotes,
        ),
      );
      if (isClosed) return;
      final sessions = state.sessions
          .map((item) => item.id == completed.id ? completed : item)
          .toList();
      emit(
        state.copyWith(
          isSaving: false,
          sessions: sessions,
          successMessage: 'Ders oturumu tamamlandi.',
          clearMessages: true,
        ),
      );
      await _writeCachedSessions(session.teacherUserId, sessions);
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    }
  }

  Future<List<LessonSession>> _readCachedSessions(String teacherUserId) async {
    final cached = await _localCache.readString(_cacheKey(teacherUserId));
    if (cached == null || cached.isEmpty) {
      return const <LessonSession>[];
    }
    final decoded = jsonDecode(cached);
    if (decoded is! List<dynamic>) {
      return const <LessonSession>[];
    }
    return decoded
        .whereType<Map<String, dynamic>>()
        .map(LessonSessionModel.fromJson)
        .toList();
  }

  Future<void> _writeCachedSessions(
    String teacherUserId,
    List<LessonSession> sessions,
  ) async {
    final payload = sessions
        .map(
          (session) => LessonSessionModel(
            id: session.id,
            lessonScheduleId: session.lessonScheduleId,
            teacherUserId: session.teacherUserId,
            studentId: session.studentId,
            subject: session.subject,
            status: session.status,
            topicTitle: session.topicTitle,
            coveredContent: session.coveredContent,
            teacherNotes: session.teacherNotes,
            actualStartAtUtc: session.actualStartAtUtc,
            actualEndAtUtc: session.actualEndAtUtc,
            plannedStartAtUtc: session.plannedStartAtUtc,
            durationMinutes: session.durationMinutes,
            attendanceStatus: session.attendanceStatus,
          ).toJson(),
        )
        .toList();
    await _localCache.writeString(
      _cacheKey(teacherUserId),
      jsonEncode(payload),
    );
  }

  String _cacheKey(String teacherUserId) => 'lessonSessions.$teacherUserId';
}
