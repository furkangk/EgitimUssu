import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';

enum NotificationsStatus { initial, loading, loaded, error }

class NotificationsState {
  const NotificationsState({
    this.status = NotificationsStatus.initial,
    this.reminders = const [],
    this.unreadCount = 0,
    this.errorMessage,
  });

  final NotificationsStatus status;
  final List<LessonReminder> reminders;

  /// M11-4: Rozet sayısı — `GET /users/{userId}/unread-count` sonucudur.
  /// (Önceden bekleyen ders hatırlatması sayısından türetiliyordu; artık gerçek okunmamış sayısı.)
  final int unreadCount;

  final String? errorMessage;

  bool get isLoading => status == NotificationsStatus.loading;

  List<LessonReminder> get pending =>
      reminders.where((r) => r.isPending).toList()
        ..sort((a, b) => a.remindAtUtc.compareTo(b.remindAtUtc));

  List<LessonReminder> get past =>
      reminders.where((r) => r.isSent).toList()
        ..sort((a, b) => b.remindAtUtc.compareTo(a.remindAtUtc));

  NotificationsState copyWith({
    NotificationsStatus? status,
    List<LessonReminder>? reminders,
    int? unreadCount,
    String? errorMessage,
    bool clearError = false,
  }) {
    return NotificationsState(
      status: status ?? this.status,
      reminders: reminders ?? this.reminders,
      unreadCount: unreadCount ?? this.unreadCount,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}
