import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';

/// Ortak `NotificationRepository` sahtesi (mobile_flutter.md §17.1 — sahteler tek noktada).
/// Varsayılan: boş liste, 0 okunmamış. Test ihtiyacına göre kurucudan beslenir.
class FakeNotificationRepository implements NotificationRepository {
  FakeNotificationRepository({
    this.reminders = const <LessonReminder>[],
    this.notifications = const <UserNotification>[],
    this.unreadCount = 0,
  });

  final List<LessonReminder> reminders;
  final List<UserNotification> notifications;
  final int unreadCount;

  final List<String> markedRead = <String>[];

  @override
  Future<List<LessonReminder>> listReminders({
    required String teacherUserId,
    bool activeOnly = false,
  }) async => reminders;

  @override
  Future<UserNotificationPage> listUserNotifications({
    required String userId,
    bool onlyUnread = false,
    int skip = 0,
    int take = 20,
    String? after,
    int? limit,
  }) async {
    final items = onlyUnread
        ? notifications.where((item) => !item.isRead).toList()
        : notifications;
    return UserNotificationPage(items: items, totalCount: items.length);
  }

  @override
  Future<int> unreadNotificationCount(String userId) async => unreadCount;

  @override
  Future<void> markNotificationRead(String notificationId) async {
    markedRead.add(notificationId);
  }
}
