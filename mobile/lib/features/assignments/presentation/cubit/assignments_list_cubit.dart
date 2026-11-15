import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/assignments_list_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class AssignmentsListCubit extends Cubit<AssignmentsListState> {
  AssignmentsListCubit(this._repository) : super(const AssignmentsListState());
  final AssignmentRepository _repository;
  String? _userId;
  int _generation = 0;

  factory AssignmentsListCubit.create() =>
      AssignmentsListCubit(injector<AssignmentRepository>());

  Future<void> load(String teacherUserId) async {
    if (isClosed) return;
    _userId = teacherUserId;
    final generation = ++_generation;
    emit(
      state.copyWith(
        isLoading: true,
        isLoadingMore: false,
        clearError: true,
        clearCursor: true,
      ),
    );
    try {
      final page = await _repository.listPage(teacherUserId: teacherUserId);
      if (isClosed || generation != _generation) return;
      emit(
        state.copyWith(
          isLoading: false,
          assignments: page.items,
          nextCursor: page.nextCursor,
          clearCursor: page.nextCursor == null,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed || generation != _generation) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    }
  }

  Future<void> loadMore() async {
    final cursor = state.nextCursor;
    if (isClosed ||
        state.isLoading ||
        state.isLoadingMore ||
        cursor == null ||
        _userId == null) {
      return;
    }
    final generation = _generation;
    emit(state.copyWith(isLoadingMore: true, clearError: true));
    try {
      final page = await _repository.listPage(
        teacherUserId: _userId,
        after: cursor,
      );
      if (isClosed || generation != _generation) return;
      final ids = state.assignments.map((x) => x.id).toSet();
      emit(
        state.copyWith(
          isLoadingMore: false,
          assignments: [
            ...state.assignments,
            ...page.items.where((x) => !ids.contains(x.id)),
          ],
          nextCursor: page.nextCursor,
          clearCursor: page.nextCursor == null,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed || generation != _generation) return;
      emit(state.copyWith(isLoadingMore: false, errorMessage: error.message));
    }
  }

  Future<void> refresh(String teacherUserId) => load(teacherUserId);
}
