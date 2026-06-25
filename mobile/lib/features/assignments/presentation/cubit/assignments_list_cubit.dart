import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/assignments_list_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class AssignmentsListCubit extends Cubit<AssignmentsListState> {
  AssignmentsListCubit(this._repository) : super(const AssignmentsListState());

  final AssignmentRepository _repository;

  factory AssignmentsListCubit.create() =>
      AssignmentsListCubit(injector<AssignmentRepository>());

  Future<void> load(String teacherUserId) async {
    if (isClosed) return;
    emit(state.copyWith(isLoading: true, clearError: true));
    try {
      final assignments = await _repository.listByTeacher(teacherUserId);
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, assignments: assignments));
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    }
  }

  Future<void> refresh(String teacherUserId) => load(teacherUserId);
}
