import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';

enum LessonResourcesStatus { initial, loading, loaded, failure }

/// M06-1: Ders kaynakları listesi durumu.
class LessonResourcesState {
  const LessonResourcesState({
    this.status = LessonResourcesStatus.initial,
    this.resources = const <LessonResource>[],
    this.isSaving = false,
    this.errorMessage,
  });

  final LessonResourcesStatus status;
  final List<LessonResource> resources;
  final bool isSaving;
  final String? errorMessage;

  bool get isLoading => status == LessonResourcesStatus.loading;

  LessonResourcesState copyWith({
    LessonResourcesStatus? status,
    List<LessonResource>? resources,
    bool? isSaving,
    String? errorMessage,
    bool clearError = false,
  }) {
    return LessonResourcesState(
      status: status ?? this.status,
      resources: resources ?? this.resources,
      isSaving: isSaving ?? this.isSaving,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}
