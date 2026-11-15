import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';

enum NotificationCenterStatus { initial, loading, loaded, failure }

/// M11-3/M11-4: Bildirim merkezi durumu — liste + okunmamış rozeti + filtre.
class NotificationCenterState {
  const NotificationCenterState({
    this.status = NotificationCenterStatus.initial,
    this.items = const <UserNotification>[],
    this.unreadCount = 0,
    this.onlyUnread = false,
    this.errorMessage,
    this.nextCursor,
    this.isLoadingMore = false,
  });

  final NotificationCenterStatus status;
  final List<UserNotification> items;
  final int unreadCount;
  final bool onlyUnread;
  final String? errorMessage;
  final String? nextCursor;
  final bool isLoadingMore;

  bool get isLoading => status == NotificationCenterStatus.loading;

  bool get isEmpty =>
      status == NotificationCenterStatus.loaded && items.isEmpty;

  NotificationCenterState copyWith({
    NotificationCenterStatus? status,
    List<UserNotification>? items,
    int? unreadCount,
    bool? onlyUnread,
    String? errorMessage,
    bool clearError = false,
    String? nextCursor,
    bool clearCursor = false,
    bool? isLoadingMore,
  }) {
    return NotificationCenterState(
      status: status ?? this.status,
      items: items ?? this.items,
      unreadCount: unreadCount ?? this.unreadCount,
      onlyUnread: onlyUnread ?? this.onlyUnread,
      nextCursor: clearCursor ? null : nextCursor ?? this.nextCursor,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}
