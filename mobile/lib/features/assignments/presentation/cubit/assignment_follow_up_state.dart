import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:equatable/equatable.dart';

class AssignmentFollowUpState extends Equatable {
  const AssignmentFollowUpState({
    this.isLoading = false,
    this.isSaving = false,
    this.followUp,
    this.errorMessage,
    this.successMessage,
  });

  final bool isLoading;
  final bool isSaving;
  final FollowUpAssignment? followUp;
  final String? errorMessage;
  final String? successMessage;

  AssignmentFollowUpState copyWith({
    bool? isLoading,
    bool? isSaving,
    FollowUpAssignment? followUp,
    String? errorMessage,
    String? successMessage,
    bool clearMessages = false,
  }) {
    return AssignmentFollowUpState(
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      followUp: followUp ?? this.followUp,
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
    followUp,
    errorMessage,
    successMessage,
  ];
}
