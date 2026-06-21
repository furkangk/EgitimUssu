import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:equatable/equatable.dart';

class StudentDetailState extends Equatable {
  const StudentDetailState({
    this.isLoading = false,
    this.student,
    this.lessons = const <LessonSchedule>[],
    this.payments = const <PaymentRecord>[],
    this.errorMessage,
  });

  final bool isLoading;
  final StudentProfile? student;
  final List<LessonSchedule> lessons;
  final List<PaymentRecord> payments;
  final String? errorMessage;

  double get outstandingAmount {
    return payments.fold<double>(
      0,
      (total, payment) => total + payment.outstandingAmount,
    );
  }

  StudentDetailState copyWith({
    bool? isLoading,
    StudentProfile? student,
    List<LessonSchedule>? lessons,
    List<PaymentRecord>? payments,
    String? errorMessage,
    bool clearError = false,
  }) {
    return StudentDetailState(
      isLoading: isLoading ?? this.isLoading,
      student: student ?? this.student,
      lessons: lessons ?? this.lessons,
      payments: payments ?? this.payments,
      errorMessage: clearError ? null : errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props => <Object?>[
    isLoading,
    student,
    lessons,
    payments,
    errorMessage,
  ];
}
