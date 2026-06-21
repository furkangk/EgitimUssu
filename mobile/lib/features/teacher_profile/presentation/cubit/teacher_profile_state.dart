import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:equatable/equatable.dart';

class TeacherProfileState extends Equatable {
  const TeacherProfileState({
    this.isLoading = false,
    this.isSaving = false,
    this.profile,
    this.errorMessage,
    this.successMessage,
  });

  final bool isLoading;
  final bool isSaving;
  final TeacherProfile? profile;
  final String? errorMessage;
  final String? successMessage;

  TeacherProfileState copyWith({
    bool? isLoading,
    bool? isSaving,
    TeacherProfile? profile,
    String? errorMessage,
    String? successMessage,
    bool clearMessages = false,
  }) {
    return TeacherProfileState(
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      profile: profile ?? this.profile,
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
    profile,
    errorMessage,
    successMessage,
  ];
}
