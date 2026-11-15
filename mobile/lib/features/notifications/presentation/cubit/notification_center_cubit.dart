import 'dart:async';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/push/push_event_bus.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notification_center_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// M11-3/M11-4 (D-11): Rolden bağımsız bildirim merkezi.
/// `markRead` **iyimser** günceller; sunucu reddederse değişiklik geri alınır.
class NotificationCenterCubit extends Cubit<NotificationCenterState> {
  NotificationCenterCubit(this._repository, {PushEventBus? eventBus})
    : _eventBus = eventBus,
      super(const NotificationCenterState()) {
    _subscription = eventBus?.events.listen((event) {
      _onPush();
    });
    eventBus?.addListener(_sessionChanged);
  }

  factory NotificationCenterCubit.create() => NotificationCenterCubit(
    injector<NotificationRepository>(),
    eventBus: injector.isRegistered<PushEventBus>()
        ? injector<PushEventBus>()
        : null,
  );

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
    _userId = null;
    _pendingReads = 0;
    _refreshAfterRead = false;
    _generation++;
    if (!isClosed) emit(const NotificationCenterState());
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
        await load(user, silent: true);
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

  String? _userId;

  int _generation = 0;
  int _readRevision = 0;
  int _pendingReads = 0;
  bool _refreshAfterRead = false;

  Future<void> loadMore() async {
    final userId = _userId;
    final cursor = state.nextCursor;
    if (isClosed ||
        state.isLoading ||
        state.isLoadingMore ||
        cursor == null ||
        userId == null) {
      return;
    }
    final generation = _generation;
    emit(state.copyWith(isLoadingMore: true, clearError: true));
    try {
      final page = await _repository.listUserNotifications(
        userId: userId,
        onlyUnread: state.onlyUnread,
        after: cursor,
        limit: 50,
      );
      if (isClosed || generation != _generation) return;
      final ids = state.items.map((x) => x.id).toSet();
      emit(
        state.copyWith(
          items: [
            ...state.items,
            ...page.items.where((x) => !ids.contains(x.id)),
          ],
          isLoadingMore: false,
          nextCursor: page.nextCursor,
          clearCursor: page.nextCursor == null,
        ),
      );
    } catch (error) {
      if (isClosed || generation != _generation) return;
      emit(
        state.copyWith(
          isLoadingMore: false,
          errorMessage: error is ApiException
              ? error.message
              : 'Bildirimler yüklenemedi.',
        ),
      );
    }
  }

  Future<void> load(String userId, {bool silent = false}) async {
    if (isClosed) return;
    if (_pendingReads > 0) {
      _refreshAfterRead = true;
      return;
    }
    final readRevision = _readRevision;
    final generation = ++_generation;
    _userId = userId;
    emit(
      state.copyWith(
        status: silent ? state.status : NotificationCenterStatus.loading,
        isLoadingMore: false,
        clearCursor: !silent,
        clearError: true,
      ),
    );

    try {
      final page = await _repository.listUserNotifications(
        userId: userId,
        onlyUnread: state.onlyUnread,
        limit: 50,
      );
      if (isClosed ||
          generation != _generation ||
          readRevision != _readRevision) {
        return;
      }
      final unread = await _repository.unreadNotificationCount(userId);
      if (isClosed ||
          generation != _generation ||
          readRevision != _readRevision) {
        return;
      }

      emit(
        state.copyWith(
          status: NotificationCenterStatus.loaded,
          items: page.items,
          nextCursor: page.nextCursor,
          clearCursor: page.nextCursor == null,
          unreadCount: unread,
          clearError: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed || generation != _generation) return;
      emit(
        state.copyWith(
          status: silent ? state.status : NotificationCenterStatus.failure,
          errorMessage: error.message,
        ),
      );
    } catch (_) {
      if (isClosed || generation != _generation) return;
      emit(
        state.copyWith(
          status: silent ? state.status : NotificationCenterStatus.failure,
          errorMessage: 'Bildirimler yüklenemedi.',
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

  /// "Tümü / Okunmamış" filtresi; değişince liste yeniden yüklenir.
  Future<void> setOnlyUnread(bool onlyUnread) async {
    if (isClosed || state.onlyUnread == onlyUnread) {
      return;
    }

    emit(state.copyWith(onlyUnread: onlyUnread));
    await refresh();
  }

  Future<void> markRead(String notificationId) async {
    if (isClosed) return;
    final generation = _generation;
    final index = state.items.indexWhere((item) => item.id == notificationId);
    if (index < 0 || state.items[index].isRead) {
      // Zaten okunmuş: gereksiz istek gönderilmez, rozet de bozulmaz.
      return;
    }

    _readRevision++;
    _pendingReads++;
    final user = _userId;
    final session = _eventBus?.generation;
    if (_refreshing) _refreshAfterRead = true;
    final previousItems = state.items;
    final previousUnread = state.unreadCount;

    final updated = [...previousItems];
    updated[index] = updated[index].copyWith(
      isRead: true,
      readOnUtc: DateTime.now().toUtc(),
    );

    emit(
      state.copyWith(
        items: state.onlyUnread
            ? updated.where((item) => !item.isRead).toList()
            : updated,
        unreadCount: previousUnread > 0 ? previousUnread - 1 : 0,
      ),
    );

    try {
      await _repository.markNotificationRead(notificationId);
    } catch (_) {
      // Sunucu reddetti: iyimser değişikliği geri al, kullanıcı yanlış bilgilenmesin.
      if (isClosed || generation != _generation) return;
      final original = previousItems[index];
      final items = state.onlyUnread
          ? [...state.items, original]
          : state.items.map((x) => x.id == original.id ? original : x).toList();
      items.sort((a, b) {
        final time = b.createdOnUtc.compareTo(a.createdOnUtc);
        return time != 0 ? time : b.id.compareTo(a.id);
      });
      emit(state.copyWith(items: items, unreadCount: state.unreadCount + 1));
    } finally {
      if (!isClosed && user == _userId && session == _eventBus?.generation) {
        _pendingReads--;
        if (_pendingReads == 0 && _refreshAfterRead && user != null) {
          _refreshAfterRead = false;
          await load(user, silent: true);
        }
      }
    }
  }
}
