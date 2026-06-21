import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:equatable/equatable.dart';

class SchedulingState extends Equatable {
  SchedulingState({
    this.isLoading = false,
    this.isSaving = false,
    this.lessons = const <LessonSchedule>[],
    this.selectedLesson,
    DateTime? weekStartAtUtc,
    DateTime? weekEndAtUtc,
    this.errorMessage,
    this.successMessage,
  }) : weekStartAtUtc = weekStartAtUtc ?? _defaultWeekStartAtUtc,
       weekEndAtUtc = weekEndAtUtc ?? _defaultWeekEndAtUtc;

  final bool isLoading;
  final bool isSaving;
  final List<LessonSchedule> lessons;
  final LessonSchedule? selectedLesson;
  final DateTime weekStartAtUtc;
  final DateTime weekEndAtUtc;
  final String? errorMessage;
  final String? successMessage;

  SchedulingState copyWith({
    bool? isLoading,
    bool? isSaving,
    List<LessonSchedule>? lessons,
    LessonSchedule? selectedLesson,
    DateTime? weekStartAtUtc,
    DateTime? weekEndAtUtc,
    String? errorMessage,
    String? successMessage,
    bool clearError = false,
    bool clearMessages = false,
  }) {
    return SchedulingState(
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      lessons: lessons ?? this.lessons,
      selectedLesson: selectedLesson ?? this.selectedLesson,
      weekStartAtUtc: weekStartAtUtc ?? this.weekStartAtUtc,
      weekEndAtUtc: weekEndAtUtc ?? this.weekEndAtUtc,
      errorMessage: clearError || clearMessages
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
    selectedLesson,
    weekStartAtUtc,
    weekEndAtUtc,
    errorMessage,
    successMessage,
  ];

  static DateTime get _defaultWeekStartAtUtc {
    final now = DateTime.now().toUtc();
    final startOfDay = DateTime.utc(now.year, now.month, now.day);
    return startOfDay.subtract(Duration(days: now.weekday - 1));
  }

  static DateTime get _defaultWeekEndAtUtc {
    return _defaultWeekStartAtUtc.add(const Duration(days: 7));
  }
}
