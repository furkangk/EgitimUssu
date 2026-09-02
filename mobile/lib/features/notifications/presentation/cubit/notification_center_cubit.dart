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

  Future<void> load(String userId) async {
    _userId = userId;
    emit(
      state.copyWith(
        status: NotificationCenterStatus.loading,
        clearError: true,
      ),
    );

    try {
      final page = await _repository.listUserNotifications(
        userId: userId,
        onlyUnread: state.onlyUnread,
      );
      final unread = await _repository.unreadNotificationCount(userId);

      emit(
        state.copyWith(
          status: NotificationCenterStatus.loaded,
          items: page.items,
          unreadCount: unread,
          clearError: true,
        ),
      );
    } on ApiException catch (error) {
      emit(
        state.copyWith(
          status: NotificationCenterStatus.failure,
          errorMessage: error.message,
        ),
      );
    } catch (_) {
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
    if (state.onlyUnread == onlyUnread) {
      return;
    }

    emit(state.copyWith(onlyUnread: onlyUnread));
    await refresh();
  }

  Future<void> markRead(String notificationId) async {
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
      emit(state.copyWith(items: previousItems, unreadCount: previousUnread));
    }
  }
}
