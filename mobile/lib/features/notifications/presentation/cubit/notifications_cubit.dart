import 'dart:async';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/push/push_event_bus.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_state.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class NotificationsCubit extends Cubit<NotificationsState> {
  NotificationsCubit(this._repository, {PushEventBus? eventBus})
    : _eventBus = eventBus,
      super(const NotificationsState()) {
    _subscription = eventBus?.events.listen((event) {
      _onPush();
    });
    eventBus?.addListener(_sessionChanged);
  }

  final NotificationRepository _repository;
  final PushEventBus? _eventBus;
  StreamSubscription<PushEvent>? _subscription;
  int _refreshRun = 0;
  bool _refreshing = false;
  bool _again = false;
  void _sessionChanged() {
    _refreshRun++;
    _refreshing = false;
    _again = false;
    reset();
  }

  Future<void> _onPush() async {
    final user = _eventBus?.userId;
    if (isClosed || user == null) return;
    if (_refreshing) {
      _again = true;
      return;
    }
    _refreshing = true;
    final run = ++_refreshRun;
    final session = _eventBus?.generation;
    try {
      do {
        _again = false;
        await refreshBadge(user);
      } while (_again && !isClosed && session == _eventBus?.generation);
    } finally {
      if (run == _refreshRun) _refreshing = false;
    }
  }

  @override
  Future<void> close() async {
    _generation++;
    _eventBus?.removeListener(_sessionChanged);
    await _subscription?.cancel();
    return super.close();
  }

  int _generation = 0;
  int _badgeGeneration = 0;
  void reset() {
    _generation++;
    _badgeGeneration++;
    if (!isClosed) emit(const NotificationsState());
  }

  bool _current(int generation) => !isClosed && generation == _generation;

  factory NotificationsCubit.create() => NotificationsCubit(
    injector<NotificationRepository>(),
    eventBus: injector.isRegistered<PushEventBus>()
        ? injector<PushEventBus>()
        : null,
  );

  Future<void> refreshBadge(String userId) async {
    final generation = _generation;
    final badgeGeneration = ++_badgeGeneration;
    try {
      final count = await _repository.unreadNotificationCount(userId);
      if (_current(generation) && badgeGeneration == _badgeGeneration) {
        emit(state.copyWith(unreadCount: count));
      }
    } catch (error) {
      debugPrint('Badge refresh failed: ${error.runtimeType}');
    }
  }

  Future<void> load(String teacherUserId) async {
    if (isClosed) return;
    final generation = ++_generation;
    final badgeGeneration = _badgeGeneration;
    emit(state.copyWith(status: NotificationsStatus.loading, clearError: true));
    try {
      final reminders = await _repository.listReminders(
        teacherUserId: teacherUserId,
      );
      if (!_current(generation)) return;
      final unread = await _readUnreadCount(teacherUserId);
      if (!_current(generation)) return;
      emit(
        state.copyWith(
          status: NotificationsStatus.loaded,
          reminders: reminders,
          unreadCount: badgeGeneration == _badgeGeneration
              ? unread
              : state.unreadCount,
        ),
      );
    } on ApiException catch (e) {
      if (!_current(generation)) return;
      emit(
        state.copyWith(
          status: NotificationsStatus.error,
          errorMessage: e.message,
        ),
      );
    } catch (_) {
      if (!_current(generation)) return;
      emit(
        state.copyWith(
          status: NotificationsStatus.error,
          errorMessage: 'Bildirimler yüklenemedi.',
        ),
      );
    }
  }

  Future<void> refresh(String teacherUserId) => load(teacherUserId);

  /// M11-4: Bildirim merkezinde okundu işaretlendiğinde rozet anında güncellensin diye
  /// dışarıdan set edilebilir (yeniden istek atmadan).
  void setUnreadCount(int count) {
    _badgeGeneration++;
    emit(state.copyWith(unreadCount: count < 0 ? 0 : count));
  }

  /// Rozet sayısı liste yüklemesini bloke etmemeli: hata olursa 0 kabul edilir.
  Future<int> _readUnreadCount(String userId) async {
    try {
      return await _repository.unreadNotificationCount(userId);
    } catch (_) {
      return 0;
    }
  }
}
