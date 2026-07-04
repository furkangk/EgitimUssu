import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:equatable/equatable.dart';

enum StudyHomeStatus { initial, loading, ready, error }

class StudyHomeState extends Equatable {
  const StudyHomeState({
    this.status = StudyHomeStatus.initial,
    this.studentId,
    this.dashboard,
    this.errorMessage,
  });

  final StudyHomeStatus status;
  final String? studentId;
  final StudyDashboard? dashboard;
  final String? errorMessage;

  StudyHomeState copyWith({
    StudyHomeStatus? status,
    String? studentId,
    StudyDashboard? dashboard,
    String? errorMessage,
    bool clearError = false,
  }) {
    return StudyHomeState(
      status: status ?? this.status,
      studentId: studentId ?? this.studentId,
      dashboard: dashboard ?? this.dashboard,
      errorMessage: clearError ? null : errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props => <Object?>[status, studentId, dashboard, errorMessage];
}
