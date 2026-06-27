import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';

enum NotificationsStatus { initial, loading, loaded, error }

class NotificationsState {
  const NotificationsState({
    this.status = NotificationsStatus.initial,
    this.reminders = const [],
    this.errorMessage,
  });

  final NotificationsStatus status;
  final List<LessonReminder> reminders;
  final String? errorMessage;

  bool get isLoading => status == NotificationsStatus.loading;

  List<LessonReminder> get pending =>
      reminders.where((r) => r.isPending).toList()
        ..sort((a, b) => a.remindAtUtc.compareTo(b.remindAtUtc));

  List<LessonReminder> get past =>
      reminders.where((r) => r.isSent).toList()
        ..sort((a, b) => b.remindAtUtc.compareTo(a.remindAtUtc));

  int get unreadCount => pending.length;

  NotificationsState copyWith({
    NotificationsStatus? status,
    List<LessonReminder>? reminders,
    String? errorMessage,
    bool clearError = false,
  }) {
    return NotificationsState(
      status: status ?? this.status,
      reminders: reminders ?? this.reminders,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}
