import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/student_detail_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class StudentDetailCubit extends Cubit<StudentDetailState> {
  StudentDetailCubit({
    required StudentRepository studentRepository,
    required SchedulingRepository schedulingRepository,
    required PaymentRepository paymentRepository,
  }) : _studentRepository = studentRepository,
       _schedulingRepository = schedulingRepository,
       _paymentRepository = paymentRepository,
       super(const StudentDetailState());

  final StudentRepository _studentRepository;
  final SchedulingRepository _schedulingRepository;
  final PaymentRepository _paymentRepository;

  factory StudentDetailCubit.create() {
    return StudentDetailCubit(
      studentRepository: injector<StudentRepository>(),
      schedulingRepository: injector<SchedulingRepository>(),
      paymentRepository: injector<PaymentRepository>(),
    );
  }

  Future<void> load({
    required String studentId,
    required String teacherUserId,
  }) async {
    if (isClosed) return;
    emit(state.copyWith(isLoading: true, clearError: true));
    try {
      final student = await _studentRepository.getStudent(studentId);
      if (isClosed) return;
      final lessons = await _schedulingRepository.listTeacherLessons(
        teacherUserId: teacherUserId,
      );
      if (isClosed) return;
      final payments = await _paymentRepository.listTeacherRecords(
        teacherUserId,
      );
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          student: student,
          lessons: lessons
              .where((lesson) => lesson.studentId == studentId)
              .toList(),
          payments: payments
              .where((payment) => payment.studentId == studentId)
              .toList(),
          clearError: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    }
  }
}
