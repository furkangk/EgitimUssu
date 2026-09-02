import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/settings/domain/settings_contracts.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

enum SettingsStatus { initial, loading, loaded, failure }

/// M15-1: Ayar ekranlarının durumu. `saving`, hangi satırın kaydedildiğini
/// göstermek için ayrı bir bayrak olarak taşınır (status `loaded` kalır).
class SettingsState {
  const SettingsState({
    this.status = SettingsStatus.initial,
    this.settings,
    this.isSaving = false,
    this.errorMessage,
  });

  final SettingsStatus status;
  final UserSettings? settings;
  final bool isSaving;
  final String? errorMessage;

  bool get isLoading => status == SettingsStatus.loading;

  SettingsState copyWith({
    SettingsStatus? status,
    UserSettings? settings,
    bool? isSaving,
    String? errorMessage,
    bool clearError = false,
  }) {
    return SettingsState(
      status: status ?? this.status,
      settings: settings ?? this.settings,
      isSaving: isSaving ?? this.isSaving,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}

/// D-14: Ayar ekranları gerçek backend'e bağlıdır. Her değişiklik **iyimser**
/// uygulanır ve anında kaydedilir; sunucu reddederse eski değere dönülür.
class SettingsCubit extends Cubit<SettingsState> {
  SettingsCubit(this._repository) : super(const SettingsState());

  factory SettingsCubit.create() => SettingsCubit(injector<SettingsRepository>());

  final SettingsRepository _repository;

  String? _userId;

  Future<void> load(String userId) async {
    _userId = userId;
    emit(state.copyWith(status: SettingsStatus.loading, clearError: true));

    try {
      final settings = await _repository.load(userId);
      emit(
        state.copyWith(
          status: SettingsStatus.loaded,
          settings: settings,
          clearError: true,
        ),
      );
    } on ApiException catch (error) {
      emit(
        state.copyWith(
          status: SettingsStatus.failure,
          errorMessage: error.message,
        ),
      );
    } catch (_) {
      emit(
        state.copyWith(
          status: SettingsStatus.failure,
          errorMessage: 'Ayarlar yüklenemedi.',
        ),
      );
    }
  }

  Future<void> refresh() async {
    final userId = _userId;
    if (userId != null) {
      await load(userId);
    }
  }

  Future<void> setPushNotificationsEnabled(bool value) =>
      _apply((s) => s.copyWith(pushNotificationsEnabled: value));

  Future<void> setEmailNotificationsEnabled(bool value) =>
      _apply((s) => s.copyWith(emailNotificationsEnabled: value));

  Future<void> setUpcomingLessonReminderEnabled(bool value) =>
      _apply((s) => s.copyWith(upcomingLessonReminderEnabled: value));

  Future<void> setHomeworkReminderEnabled(bool value) =>
      _apply((s) => s.copyWith(homeworkReminderEnabled: value));

  Future<void> setPaymentReminderEnabled(bool value) =>
      _apply((s) => s.copyWith(paymentReminderEnabled: value));

  Future<void> setWeeklySummaryEnabled(bool value) =>
      _apply((s) => s.copyWith(weeklySummaryEnabled: value));

  Future<void> setPrivacyLevel(PrivacyLevel value) =>
      _apply((s) => s.copyWith(privacyLevel: value));

  Future<void> setSessionTerminationPolicy(SessionTerminationPolicy value) =>
      _apply((s) => s.copyWith(sessionTerminationPolicy: value));

  /// İyimser güncelleme + anında kayıt. Hata olursa önceki durum geri gelir.
  Future<void> _apply(UserSettings Function(UserSettings current) change) async {
    final userId = _userId;
    final current = state.settings;
    if (userId == null || current == null) {
      // Henüz yüklenmedi: kullanıcı bir şey değiştiremez, boşa istek de gitmez.
      return;
    }

    final updated = change(current);
    emit(state.copyWith(settings: updated, isSaving: true, clearError: true));

    try {
      final saved = await _repository.save(userId, updated);
      emit(state.copyWith(settings: saved, isSaving: false, clearError: true));
    } on ApiException catch (error) {
      emit(
        state.copyWith(
          settings: current,
          isSaving: false,
          errorMessage: error.message,
        ),
      );
    } catch (_) {
      emit(
        state.copyWith(
          settings: current,
          isSaving: false,
          errorMessage: 'Ayar kaydedilemedi.',
        ),
      );
    }
  }
}
