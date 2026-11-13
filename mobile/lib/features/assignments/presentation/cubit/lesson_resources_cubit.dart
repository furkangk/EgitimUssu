import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/lesson_resources_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// M06-1 (P04): Ders kaynakları — öğretmen ekler/arşivler, öğrenci görüntüler.
/// Sunucu sahiplik filtresini zorlar; istemci yalnız kendi kimliğini gönderir.
class LessonResourcesCubit extends Cubit<LessonResourcesState> {
  LessonResourcesCubit(this._repository) : super(const LessonResourcesState());

  factory LessonResourcesCubit.create() =>
      LessonResourcesCubit(injector<AssignmentRepository>());

  final AssignmentRepository _repository;

  String? _teacherUserId;
  String? _studentId;
  String? _lessonSessionId;

  Future<void> load({
    String? teacherUserId,
    String? studentId,
    String? lessonSessionId,
  }) async {
    _teacherUserId = teacherUserId;
    _studentId = studentId;
    _lessonSessionId = lessonSessionId;

    emit(state.copyWith(status: LessonResourcesStatus.loading, clearError: true));
    try {
      final resources = await _repository.listLessonResources(
        teacherUserId: teacherUserId,
        studentId: studentId,
        lessonSessionId: lessonSessionId,
      );
      emit(
        state.copyWith(
          status: LessonResourcesStatus.loaded,
          resources: resources,
          clearError: true,
        ),
      );
    } on ApiException catch (error) {
      emit(
        state.copyWith(
          status: LessonResourcesStatus.failure,
          errorMessage: error.message,
        ),
      );
    } catch (_) {
      emit(
        state.copyWith(
          status: LessonResourcesStatus.failure,
          errorMessage: 'Ders kaynakları yüklenemedi.',
        ),
      );
    }
  }

  Future<void> refresh() => load(
    teacherUserId: _teacherUserId,
    studentId: _studentId,
    lessonSessionId: _lessonSessionId,
  );

  /// Kaynak ekler; başarılıysa liste tazelenir. Hata sessizce yutulmaz.
  Future<bool> addResource({
    required String title,
    String? description,
    String? url,
    String? filePath,
    String? studentId,
  }) async {
    emit(state.copyWith(isSaving: true, clearError: true));
    try {
      await _repository.createLessonResource(
        title: title,
        description: description,
        url: url,
        filePath: filePath,
        studentId: studentId,
        lessonSessionId: _lessonSessionId,
      );
      emit(state.copyWith(isSaving: false));
      await refresh();
      return true;
    } on ApiException catch (error) {
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
      return false;
    } catch (_) {
      emit(state.copyWith(isSaving: false, errorMessage: 'Kaynak eklenemedi.'));
      return false;
    }
  }

  /// Arşivler; liste **iyimser** güncellenir, sunucu reddederse geri alınır.
  Future<bool> archiveResource(String resourceId) async {
    final previous = state.resources;
    emit(
      state.copyWith(
        resources: previous.where((r) => r.id != resourceId).toList(),
        clearError: true,
      ),
    );

    try {
      await _repository.archiveLessonResource(resourceId);
      return true;
    } catch (_) {
      emit(state.copyWith(resources: previous, errorMessage: 'Kaynak silinemedi.'));
      return false;
    }
  }
}
