import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';

enum ParentStatus { initial, loading, loaded, error }

class ParentState {
  const ParentState({
    this.status = ParentStatus.initial,
    this.profile,
    this.children = const <ChildLink>[],
    this.selectedStudentId,
    this.dashboard,
    this.dashboardLoading = false,
    this.prefsSaving = false,
    this.errorMessage,
  });

  final ParentStatus status;
  final ParentProfile? profile;
  final List<ChildLink> children;
  final String? selectedStudentId;
  final ChildDashboard? dashboard;
  final bool dashboardLoading;
  final bool prefsSaving;
  final String? errorMessage;

  bool get isLoading => status == ParentStatus.loading;

  List<ChildLink> get approvedChildren =>
      children.where((c) => c.isApproved).toList();

  ChildLink? get selectedChild {
    for (final child in children) {
      if (child.studentId == selectedStudentId) return child;
    }
    return null;
  }

  ParentState copyWith({
    ParentStatus? status,
    ParentProfile? profile,
    List<ChildLink>? children,
    String? selectedStudentId,
    ChildDashboard? dashboard,
    bool? dashboardLoading,
    bool? prefsSaving,
    String? errorMessage,
    bool clearError = false,
    bool clearDashboard = false,
  }) {
    return ParentState(
      status: status ?? this.status,
      profile: profile ?? this.profile,
      children: children ?? this.children,
      selectedStudentId: selectedStudentId ?? this.selectedStudentId,
      dashboard: clearDashboard ? null : (dashboard ?? this.dashboard),
      dashboardLoading: dashboardLoading ?? this.dashboardLoading,
      prefsSaving: prefsSaving ?? this.prefsSaving,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}
