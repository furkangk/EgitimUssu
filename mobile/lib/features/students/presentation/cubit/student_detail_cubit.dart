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

  Future<void> updateStudent(StudentProfile updated) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final saved = await _studentRepository.updateStudent(updated);
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          student: saved,
          successMessage: 'Ogrenci guncellendi.',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    }
  }

  Future<void> setIsActive({required bool isActive}) async {
    final current = state.student;
    if (current == null) return;
    final updated = StudentProfile(
      id: current.id,
      fullName: current.fullName,
      gradeLevel: current.gradeLevel,
      origin: current.origin,
      subjects: current.subjects,
      teacherUserId: current.teacherUserId,
      contactEmail: current.contactEmail,
      contactPhone: current.contactPhone,
      goalSummary: current.goalSummary,
      levelNotes: current.levelNotes,
      isActive: isActive,
    );
    await updateStudent(updated);
  }
}
