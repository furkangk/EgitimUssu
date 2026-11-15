import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class SchedulingCubit extends Cubit<SchedulingState> {
  SchedulingCubit(this._repository) : super(SchedulingState());

  final SchedulingRepository _repository;

  factory SchedulingCubit.create() =>
      SchedulingCubit(injector<SchedulingRepository>());

  Future<void> load(String teacherUserId) async {
    if (isClosed) return;
    emit(state.copyWith(isLoading: true, clearMessages: true));
    try {
      final lessons = await _repository.listTeacherLessons(
        teacherUserId: teacherUserId,
        startAtUtc: state.weekStartAtUtc,
        endAtUtc: state.weekEndAtUtc,
      );
      if (isClosed) return;
      emit(
        state.copyWith(isLoading: false, lessons: lessons, clearMessages: true),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    }
  }

  Future<void> createLesson(LessonSchedule lesson) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final created = await _repository.createLesson(lesson);
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          lessons: <LessonSchedule>[...state.lessons, created]
            ..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc)),
          successMessage: 'Ders plani olusturuldu.',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      final message = error.isConflict
          ? 'Bu saat aralığında zaten planlı bir ders var. Haftalık görünümden boş bir aralık seç.'
          : error.message;
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: message));
    }
  }

  Future<void> updateLesson(LessonSchedule lesson) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final updated = await _repository.updateLesson(lesson);
      if (isClosed) return;
      final exists = state.lessons.any((l) => l.id == updated.id);
      final merged = exists
          ? state.lessons.map((l) => l.id == updated.id ? updated : l).toList()
          : <LessonSchedule>[...state.lessons, updated];
      final next = merged..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));
      emit(
        state.copyWith(
          isSaving: false,
          lessons: next,
          successMessage: 'Ders plani guncellendi.',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      final message = error.isConflict
          ? 'Bu saat aralığında zaten planlı bir ders var. Haftalık görünümden boş bir aralık seç.'
          : error.message;
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: message));
    }
  }

  Future<void> selectWeek({
    required String teacherUserId,
    required DateTime weekStartAtUtc,
  }) async {
    if (isClosed) return;
    emit(
      state.copyWith(
        weekStartAtUtc: weekStartAtUtc,
        weekEndAtUtc: weekStartAtUtc.add(const Duration(days: 7)),
        clearMessages: true,
      ),
    );
    await load(teacherUserId);
  }

  Future<void> loadDetail(String lessonId) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final lesson = await _repository.getLesson(lessonId);
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          selectedLesson: lesson,
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    }
  }

  Future<void> loadForCalendar(String teacherUserId) async {
    if (isClosed) return;
    final now = DateTime.now().toUtc();
    final start = DateTime.utc(now.year, now.month - 2, 1);
    final end = DateTime.utc(now.year, now.month + 4, 1);
    emit(
      state.copyWith(
        isLoading: true,
        weekStartAtUtc: start,
        weekEndAtUtc: end,
        clearMessages: true,
      ),
    );
    try {
      final lessons = await _repository.listTeacherLessons(
        teacherUserId: teacherUserId,
        startAtUtc: start,
        endAtUtc: end,
      );
      if (isClosed) return;
      emit(
        state.copyWith(isLoading: false, lessons: lessons, clearMessages: true),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    }
  }

  /// P17 T1 / N-01: tekrarlı derste [scope] kullanıcıya sorulur
  /// (`showOccurrenceScopeSheet`, varsayılan "Yalnız bu ders"). Ekran girişi P17 T5'te.
  Future<void> cancelLesson({
    required LessonSchedule lesson,
    required OccurrenceScope scope,
    LessonCancellationReason reason = LessonCancellationReason.teacherCancelled,
    bool isChargeable = true,
    String? cancellationNote,
  }) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      await _repository.cancelLesson(
        lesson: lesson,
        scope: scope,
        reason: reason,
        isChargeable: isChargeable,
        cancellationNote: cancellationNote,
      );
      if (isClosed) return;
      bool affected(LessonSchedule item) =>
          item.id == lesson.id &&
          (!lesson.isRecurring ||
              scope == OccurrenceScope.all ||
              (scope == OccurrenceScope.single
                  ? item.occurrenceKeyUtc == lesson.occurrenceKeyUtc
                  : !item.occurrenceKeyUtc.isBefore(lesson.occurrenceKeyUtc)));
      final lessons = state.lessons
          .map((item) => affected(item) ? item.withStatus('Cancelled') : item)
          .toList();
      emit(
        state.copyWith(
          isSaving: false,
          lessons: lessons,
          selectedLesson: lesson.withStatus('Cancelled'),
          successMessage: 'Ders iptal edildi.',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    }
  }
}
