import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:equatable/equatable.dart';

class AssignmentsListState extends Equatable {
  const AssignmentsListState({
    this.isLoading = false,
    this.assignments = const <AssignmentItem>[],
    this.errorMessage,
    this.nextCursor,
    this.isLoadingMore = false,
  });

  final bool isLoading;
  final List<AssignmentItem> assignments;
  final String? errorMessage;
  final String? nextCursor;
  final bool isLoadingMore;

  AssignmentsListState copyWith({
    bool? isLoading,
    List<AssignmentItem>? assignments,
    String? errorMessage,
    bool clearError = false,
    String? nextCursor,
    bool clearCursor = false,
    bool? isLoadingMore,
  }) {
    return AssignmentsListState(
      isLoading: isLoading ?? this.isLoading,
      assignments: assignments ?? this.assignments,
      nextCursor: clearCursor ? null : nextCursor ?? this.nextCursor,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      errorMessage: clearError ? null : errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props => <Object?>[
    isLoading,
    assignments,
    errorMessage,
    nextCursor,
    isLoadingMore,
  ];
}
