import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:equatable/equatable.dart';

class StudyTimerState extends Equatable {
  const StudyTimerState({
    this.session,
    this.elapsedSeconds = 0,
    this.breakSeconds = 0,
    this.breakCount = 0,
    this.isBusy = false,
    this.completedSummary,
    this.errorMessage,
  });

  final StudySession? session;

  /// Net çalışma süresi (yalnızca "Çalışıyor" durumunda artar).
  final int elapsedSeconds;

  /// Bu seansta toplam mola süresi (yalnızca "Molada" durumunda artar).
  final int breakSeconds;

  /// Bu seansta verilen mola sayısı (her "Mola Ver" ile +1).
  final int breakCount;

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
    int? breakSeconds,
    int? breakCount,
    bool? isBusy,
    StudySession? completedSummary,
    bool clearSummary = false,
    String? errorMessage,
    bool clearError = false,
  }) {
    return StudyTimerState(
      session: clearSession ? null : session ?? this.session,
      elapsedSeconds: elapsedSeconds ?? this.elapsedSeconds,
      breakSeconds: breakSeconds ?? this.breakSeconds,
      breakCount: breakCount ?? this.breakCount,
      isBusy: isBusy ?? this.isBusy,
      completedSummary:
          clearSummary ? null : completedSummary ?? this.completedSummary,
      errorMessage: clearError ? null : errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props => <Object?>[
        session,
        elapsedSeconds,
        breakSeconds,
        breakCount,
        isBusy,
        completedSummary,
        errorMessage,
      ];
}
