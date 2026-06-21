import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:equatable/equatable.dart';

class StudentsState extends Equatable {
  const StudentsState({
    this.isLoading = false,
    this.isSaving = false,
    this.students = const <StudentProfile>[],
    this.errorMessage,
    this.successMessage,
  });

  final bool isLoading;
  final bool isSaving;
  final List<StudentProfile> students;
  final String? errorMessage;
  final String? successMessage;

  StudentsState copyWith({
    bool? isLoading,
    bool? isSaving,
    List<StudentProfile>? students,
    String? errorMessage,
    String? successMessage,
    bool clearError = false,
    bool clearMessages = false,
  }) {
    return StudentsState(
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      students: students ?? this.students,
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
    students,
    errorMessage,
    successMessage,
  ];
}
