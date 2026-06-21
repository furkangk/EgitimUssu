import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class StudentsCubit extends Cubit<StudentsState> {
  StudentsCubit(this._repository) : super(const StudentsState());

  final StudentRepository _repository;

  factory StudentsCubit.create() =>
      StudentsCubit(injector<StudentRepository>());

  Future<void> load(String teacherUserId) async {
    if (isClosed) return;
    emit(state.copyWith(isLoading: true, clearMessages: true));
    try {
      final students = await _repository.listByTeacher(teacherUserId);
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          students: students,
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    }
  }

  Future<void> addStudent(StudentProfile student) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final created = await _repository.createStudent(student);
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          students: <StudentProfile>[...state.students, created],
          successMessage: 'Ogrenci eklendi.',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    }
  }
}
