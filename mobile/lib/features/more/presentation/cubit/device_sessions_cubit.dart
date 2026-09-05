import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/more/domain/device_session_contracts.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

enum DeviceSessionsStatus { initial, loading, loaded, error }

class DeviceSessionsState {
  const DeviceSessionsState({
    this.status = DeviceSessionsStatus.initial,
    this.sessions = const <DeviceSession>[],
    this.busySessionId,
    this.revokingAll = false,
    this.errorMessage,
    this.lastRevokedCount,
  });

  final DeviceSessionsStatus status;
  final List<DeviceSession> sessions;

  /// Kapatılmakta olan oturumun kimliği (o satırın düğmesi kilitlenir).
  final String? busySessionId;
  final bool revokingAll;
  final String? errorMessage;

  /// Son "diğerlerini kapat" işleminde kapatılan oturum sayısı (bildirim için).
  final int? lastRevokedCount;

  bool get isLoading => status == DeviceSessionsStatus.loading;

  /// Mevcut oturum dışında kapatılabilecek oturum var mı.
  bool get hasOtherSessions => sessions.any((session) => !session.isCurrent);

  DeviceSessionsState copyWith({
    DeviceSessionsStatus? status,
    List<DeviceSession>? sessions,
    String? busySessionId,
    bool? revokingAll,
    String? errorMessage,
    int? lastRevokedCount,
    bool clearBusy = false,
    bool clearError = false,
    bool clearRevokedCount = false,
  }) => DeviceSessionsState(
    status: status ?? this.status,
    sessions: sessions ?? this.sessions,
    busySessionId: clearBusy ? null : (busySessionId ?? this.busySessionId),
    revokingAll: revokingAll ?? this.revokingAll,
    errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    lastRevokedCount: clearRevokedCount ? null : (lastRevokedCount ?? this.lastRevokedCount),
  );
}

/// M01-4 / D-03: Cihaz oturumları ekranının durumu.
///
/// Her işlemden sonra liste **sunucudan yeniden okunur**: yerelde satır silmek, sunucuda
/// başarısız olan bir iptali başarılı göstermeye açık kapı bırakır (güvenlik ekranında
/// iyimser güncelleme yanlış güven verir).
class DeviceSessionsCubit extends Cubit<DeviceSessionsState> {
  DeviceSessionsCubit(this._repository) : super(const DeviceSessionsState());

  factory DeviceSessionsCubit.create() =>
      DeviceSessionsCubit(injector<DeviceSessionRepository>());

  final DeviceSessionRepository _repository;

  Future<void> load() async {
    emit(state.copyWith(status: DeviceSessionsStatus.loading, clearError: true));
    await _refresh();
  }

  Future<void> revoke(String sessionId) async {
    emit(state.copyWith(busySessionId: sessionId, clearError: true));
    try {
      await _repository.revoke(sessionId);
      if (isClosed) return;
      emit(state.copyWith(clearBusy: true));
      await _refresh();
    } on ApiException catch (error) {
      if (!isClosed) {
        emit(state.copyWith(clearBusy: true, errorMessage: error.message));
      }
    }
  }

  Future<void> revokeAll() async {
    emit(state.copyWith(revokingAll: true, clearError: true, clearRevokedCount: true));
    try {
      final count = await _repository.revokeAll();
      if (isClosed) return;
      emit(state.copyWith(revokingAll: false, lastRevokedCount: count));
      await _refresh();
    } on ApiException catch (error) {
      if (!isClosed) {
        emit(state.copyWith(revokingAll: false, errorMessage: error.message));
      }
    }
  }

  Future<void> _refresh() async {
    try {
      final sessions = await _repository.list();
      if (isClosed) return;
      emit(state.copyWith(status: DeviceSessionsStatus.loaded, sessions: sessions));
    } on ApiException catch (error) {
      if (!isClosed) {
        emit(state.copyWith(
          status: DeviceSessionsStatus.error,
          errorMessage: error.message,
        ));
      }
    }
  }
}
