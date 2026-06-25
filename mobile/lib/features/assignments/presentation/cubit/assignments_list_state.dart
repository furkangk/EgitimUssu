import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:equatable/equatable.dart';

class AssignmentsListState extends Equatable {
  const AssignmentsListState({
    this.isLoading = false,
    this.assignments = const <AssignmentItem>[],
    this.errorMessage,
  });

  final bool isLoading;
  final List<AssignmentItem> assignments;
  final String? errorMessage;

  AssignmentsListState copyWith({
    bool? isLoading,
    List<AssignmentItem>? assignments,
    String? errorMessage,
    bool clearError = false,
  }) {
    return AssignmentsListState(
      isLoading: isLoading ?? this.isLoading,
      assignments: assignments ?? this.assignments,
      errorMessage: clearError ? null : errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props => <Object?>[isLoading, assignments, errorMessage];
}
