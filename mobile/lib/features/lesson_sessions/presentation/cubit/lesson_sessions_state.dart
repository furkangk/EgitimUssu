import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:equatable/equatable.dart';

class LessonSessionsState extends Equatable {
  const LessonSessionsState({
    this.isLoading = false,
    this.isSaving = false,
    this.lessons = const <LessonSchedule>[],
    this.sessions = const <LessonSession>[],
    this.errorMessage,
    this.successMessage,
  });

  final bool isLoading;
  final bool isSaving;
  final List<LessonSchedule> lessons;
  final List<LessonSession> sessions;
  final String? errorMessage;
  final String? successMessage;

  LessonSessionsState copyWith({
    bool? isLoading,
    bool? isSaving,
    List<LessonSchedule>? lessons,
    List<LessonSession>? sessions,
    String? errorMessage,
    String? successMessage,
    bool clearMessages = false,
  }) {
    return LessonSessionsState(
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      lessons: lessons ?? this.lessons,
      sessions: sessions ?? this.sessions,
      errorMessage: clearMessages
          ? errorMessage
          : errorMessage ?? this.errorMessage,
      successMessage: clearMessages
          ? successMessage
          : successMessage ?? this.successMessage,
    );
  }

  @override
  List<Object?> get props => <Object?>[
    isLoading,
    isSaving,
    lessons,
    sessions,
    errorMessage,
    successMessage,
  ];
}
