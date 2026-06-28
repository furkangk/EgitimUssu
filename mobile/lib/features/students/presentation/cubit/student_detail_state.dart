import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:equatable/equatable.dart';

class StudentDetailState extends Equatable {
  const StudentDetailState({
    this.isLoading = false,
    this.isSaving = false,
    this.student,
    this.lessons = const <LessonSchedule>[],
    this.payments = const <PaymentRecord>[],
    this.errorMessage,
    this.successMessage,
  });

  final bool isLoading;
  final bool isSaving;
  final StudentProfile? student;
  final List<LessonSchedule> lessons;
  final List<PaymentRecord> payments;
  final String? errorMessage;
  final String? successMessage;

  double get outstandingAmount {
    return payments.fold<double>(
      0,
      (total, payment) => total + payment.outstandingAmount,
    );
  }

  StudentDetailState copyWith({
    bool? isLoading,
    bool? isSaving,
    StudentProfile? student,
    List<LessonSchedule>? lessons,
    List<PaymentRecord>? payments,
    String? errorMessage,
    String? successMessage,
    bool clearError = false,
    bool clearMessages = false,
  }) {
    return StudentDetailState(
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      student: student ?? this.student,
      lessons: lessons ?? this.lessons,
      payments: payments ?? this.payments,
      errorMessage: clearError || clearMessages
          ? null
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
    student,
    lessons,
    payments,
    errorMessage,
    successMessage,
  ];
}
