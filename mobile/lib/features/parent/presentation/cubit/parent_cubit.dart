import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class ParentCubit extends Cubit<ParentState> {
  ParentCubit(this._repository) : super(const ParentState());

  final ParentRepository _repository;

  factory ParentCubit.create() => ParentCubit(injector<ParentRepository>());

  /// Veli panelini yükler: profil (yoksa oluşturur) + bağlı çocuklar + ilk onaylı
  /// çocuğun paneli.
  Future<void> load(String parentUserId, {String? fullName}) async {
    emit(state.copyWith(status: ParentStatus.loading, clearError: true));
    try {
      final profile = await _repository.ensureProfile(
        userId: parentUserId,
        fullName: (fullName == null || fullName.trim().isEmpty)
            ? 'Veli'
            : fullName,
      );
      final children = await _repository.listChildren(parentUserId);
      if (isClosed) return;

      final selected = _pickSelected(children, state.selectedStudentId);
      emit(
        state.copyWith(
          status: ParentStatus.loaded,
          profile: profile,
          children: children,
          selectedStudentId: selected,
          clearDashboard: selected == null,
        ),
      );

      if (selected != null) {
        await _loadDashboard(parentUserId, selected);
      }
    } on ApiException catch (error) {
      if (!isClosed) {
        emit(
          state.copyWith(
            status: ParentStatus.error,
            errorMessage: error.message,
          ),
        );
      }
    } catch (_) {
      if (!isClosed) {
        emit(
          state.copyWith(
            status: ParentStatus.error,
            errorMessage: 'Veli paneli yüklenemedi.',
          ),
        );
      }
    }
  }

  /// Detay ekranı için: belirli bir çocuğun panelini doğrudan açar.
  Future<void> focusChild(String parentUserId, String studentId) async {
    emit(state.copyWith(status: ParentStatus.loading, clearError: true));
    try {
      final children = await _repository.listChildren(parentUserId);
      if (isClosed) return;
      emit(
        state.copyWith(
          status: ParentStatus.loaded,
          children: children,
          selectedStudentId: studentId,
          clearDashboard: true,
        ),
      );
      await _loadDashboard(parentUserId, studentId);
    } on ApiException catch (error) {
      if (!isClosed) {
        emit(
          state.copyWith(
            status: ParentStatus.error,
            errorMessage: error.message,
          ),
        );
      }
    }
  }

  Future<void> selectChild(String parentUserId, String studentId) async {
    if (studentId == state.selectedStudentId) return;
    emit(state.copyWith(selectedStudentId: studentId, clearDashboard: true));
    await _loadDashboard(parentUserId, studentId);
  }

  Future<void> refresh(String parentUserId, {String? fullName}) =>
      load(parentUserId, fullName: fullName);

  Future<void> updatePreferences(
    String parentUserId,
    ParentNotificationPreferences preferences,
  ) async {
    emit(state.copyWith(prefsSaving: true, clearError: true));
    try {
      final profile = await _repository.updateNotificationPreferences(
        parentUserId: parentUserId,
        preferences: preferences,
      );
      if (!isClosed) {
        emit(state.copyWith(profile: profile, prefsSaving: false));
      }
    } on ApiException catch (error) {
      if (!isClosed) {
        emit(state.copyWith(prefsSaving: false, errorMessage: error.message));
      }
    }
  }

  /// Yeni çocuk bağlama talebi; başarılıysa çocuk listesini tazeler.
  Future<ChildLink?> requestChildLink({
    required String parentUserId,
    required String studentId,
    String? relationship,
    String? childDisplayName,
  }) async {
    try {
      final link = await _repository.requestChildLink(
        parentUserId: parentUserId,
        studentId: studentId,
        relationship: relationship,
        childDisplayName: childDisplayName,
        isPrimaryContact: state.children.isEmpty,
      );
      final children = await _repository.listChildren(parentUserId);
      if (!isClosed) {
        emit(state.copyWith(children: children, clearError: true));
      }
      return link;
    } on ApiException catch (error) {
      if (!isClosed) {
        emit(state.copyWith(errorMessage: error.message));
      }
      return null;
    }
  }

  Future<void> _loadDashboard(String parentUserId, String studentId) async {
    emit(state.copyWith(dashboardLoading: true));
    try {
      final dashboard = await _repository.getChildDashboard(
        parentUserId: parentUserId,
        studentId: studentId,
      );
      if (!isClosed) {
        emit(state.copyWith(dashboard: dashboard, dashboardLoading: false));
      }
    } on ApiException {
      // Onay bekleyen / erişilemeyen çocuk: panel boş kalır, hata bildirmeyiz.
      if (!isClosed) {
        emit(state.copyWith(dashboardLoading: false, clearDashboard: true));
      }
    }
  }

  static String? _pickSelected(List<ChildLink> children, String? current) {
    if (children.any((c) => c.studentId == current && c.isApproved)) {
      return current;
    }
    for (final child in children) {
      if (child.isApproved) return child.studentId;
    }
    return null;
  }
}
