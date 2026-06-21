import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/assignment_follow_up_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class AssignmentFollowUpCubit extends Cubit<AssignmentFollowUpState> {
  AssignmentFollowUpCubit(this._repository)
    : super(const AssignmentFollowUpState());

  final AssignmentRepository _repository;

  factory AssignmentFollowUpCubit.create() {
    return AssignmentFollowUpCubit(injector<AssignmentRepository>());
  }

  Future<void> load(String lessonSessionId) async {
    if (isClosed) return;
    emit(state.copyWith(isLoading: true, clearMessages: true));
    try {
      final followUp = await _repository.getFollowUp(lessonSessionId);
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          followUp: followUp,
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    }
  }

  Future<void> save(FollowUpAssignment followUp) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final saved = await _repository.saveFollowUp(followUp);
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          followUp: saved,
          successMessage: 'Follow-up kaydedildi.',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      final message = error.isConflict
          ? 'Bu ders icin follow-up daha once olusturulmus.'
          : error.message;
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: message));
    }
  }
}
