import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:equatable/equatable.dart';

class StudyTimerState extends Equatable {
  const StudyTimerState({
    this.session,
    this.elapsedSeconds = 0,
    this.isBusy = false,
    this.completedSummary,
    this.errorMessage,
  });

  final StudySession? session;
  final int elapsedSeconds;
  final bool isBusy;

  /// Seans tamamlandığında dolan özet; UI bunu gösterip temizler.
  final StudySession? completedSummary;
  final String? errorMessage;

  bool get hasActive => session?.isActive ?? false;
  bool get isRunning => session?.isRunning ?? false;

  StudyTimerState copyWith({
    StudySession? session,
    bool clearSession = false,
    int? elapsedSeconds,
    bool? isBusy,
    StudySession? completedSummary,
    bool clearSummary = false,
    String? errorMessage,
    bool clearError = false,
  }) {
    return StudyTimerState(
      session: clearSession ? null : session ?? this.session,
      elapsedSeconds: elapsedSeconds ?? this.elapsedSeconds,
      isBusy: isBusy ?? this.isBusy,
      completedSummary:
          clearSummary ? null : completedSummary ?? this.completedSummary,
      errorMessage: clearError ? null : errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props =>
      <Object?>[session, elapsedSeconds, isBusy, completedSummary, errorMessage];
}
