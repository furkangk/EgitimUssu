import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notification_center_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// M11-3/M11-4 (D-11): Rolden bağımsız bildirim merkezi.
/// `markRead` **iyimser** günceller; sunucu reddederse değişiklik geri alınır.
class NotificationCenterCubit extends Cubit<NotificationCenterState> {
  NotificationCenterCubit(this._repository)
    : super(const NotificationCenterState());

  factory NotificationCenterCubit.create() =>
      NotificationCenterCubit(injector<NotificationRepository>());

  final NotificationRepository _repository;

  String? _userId;

  int _generation = 0;

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

  Future<void> load(String userId) async {
    if (isClosed) return;
    final generation = ++_generation;
    _userId = userId;
    emit(
      state.copyWith(
        status: NotificationCenterStatus.loading,
        isLoadingMore: false,
        clearCursor: true,
        clearError: true,
      ),
    );

    try {
      final page = await _repository.listUserNotifications(
        userId: userId,
        onlyUnread: state.onlyUnread,
        limit: 50,
      );
      final unread = await _repository.unreadNotificationCount(userId);
      if (isClosed || generation != _generation) return;

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
          status: NotificationCenterStatus.failure,
          errorMessage: error.message,
        ),
      );
    } catch (_) {
      if (isClosed || generation != _generation) return;
      emit(
        state.copyWith(
          status: NotificationCenterStatus.failure,
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
    }
  }
}
