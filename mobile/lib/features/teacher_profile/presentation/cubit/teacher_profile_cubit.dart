import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/presentation/cubit/teacher_profile_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class TeacherProfileCubit extends Cubit<TeacherProfileState> {
  TeacherProfileCubit(this._repository) : super(const TeacherProfileState());

  final TeacherRepository _repository;

  factory TeacherProfileCubit.create() {
    return TeacherProfileCubit(injector<TeacherRepository>());
  }

  Future<void> load(String userId) async {
    if (isClosed) return;
    emit(state.copyWith(isLoading: true, clearMessages: true));
    try {
      final profile = await _repository.getProfile(userId);
      if (isClosed) return;
      emit(
        state.copyWith(isLoading: false, profile: profile, clearMessages: true),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    }
  }

  Future<void> save(TeacherProfile profile) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final current = state.profile;
      final saved = current == null
          ? await _repository.createProfile(profile)
          : await _repository.updateProfile(profile);
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          profile: saved,
          successMessage: 'Ogretmen profili kaydedildi.',
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    }
  }
}
