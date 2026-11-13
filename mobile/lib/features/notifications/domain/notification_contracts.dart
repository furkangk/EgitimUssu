class LessonReminder {
  const LessonReminder({
    required this.id,
    required this.lessonScheduleId,
    required this.teacherUserId,
    required this.studentId,
    required this.title,
    required this.message,
    required this.scheduledLessonStartAtUtc,
    required this.remindAtUtc,
    required this.channel,
    required this.status,
    required this.createdOnUtc,
  });

  final String id;
  final String lessonScheduleId;
  final String teacherUserId;
  final String studentId;
  final String title;
  final String message;
  final DateTime scheduledLessonStartAtUtc;
  final DateTime remindAtUtc;
  final String channel;
  final String status;
  final DateTime createdOnUtc;

  bool get isPending => status == 'Pending';
  bool get isSent => status == 'Sent';
  bool get isCancelled => status == 'Cancelled';
}

abstract interface class NotificationRepository {
  Future<List<LessonReminder>> listReminders({
    required String teacherUserId,
    bool activeOnly = false,
  });

  /// M11-3: Kullanıcının in-app bildirimleri (en yeni önce).
  Future<UserNotificationPage> listUserNotifications({
    required String userId,
    bool onlyUnread = false,
    int skip = 0,
    int take = 20,
  });

  /// M11-4: Okunmamış bildirim sayısı — rozet.
  Future<int> unreadNotificationCount(String userId);

  /// M11-4: Bildirimi okundu işaretler (idempotent).
  Future<void> markNotificationRead(String notificationId);
}

/// Cihaz platformu. Değerler backend'in `DevicePlatform` enum'uyla birebir eşleşir
/// (System.Text.Json varsayılanı enum'ları **sayı** olarak serileştirir — M11-2).
enum DevicePlatform {
  android(1),
  ios(2),
  web(3);

  const DevicePlatform(this.wireValue);

  /// API gövdesinde gönderilen sayısal değer.
  final int wireValue;
}

/// M11-2: Cihaz push token'ının kaydı/silinmesi. Çıkışta token devre dışı bırakılır.
abstract interface class DeviceTokenRepository {
  Future<void> register({
    required String userId,
    required String token,
    required DevicePlatform platform,
    String? deviceName,
  });

  Future<void> unregister(String token);
}

/// M11-3: Rolden bağımsız in-app bildirim (öğrenci/öğretmen/veli aynı merkezi kullanır).
class UserNotification {
  const UserNotification({
    required this.id,
    required this.userId,
    required this.kind,
    required this.title,
    required this.body,
    required this.route,
    required this.isRead,
    required this.createdOnUtc,
    this.readOnUtc,
  });

  final String id;
  final String userId;

  /// Backend `UserNotificationKind` adı (ör. `AssignmentCreated`) — ikon seçiminde kullanılır.
  final String kind;
  final String title;
  final String body;

  /// Dokunulunca gidilecek uygulama-içi rota; `null` ise yalnız okundu işaretlenir.
  final String? route;
  final bool isRead;
  final DateTime createdOnUtc;
  final DateTime? readOnUtc;

  UserNotification copyWith({bool? isRead, DateTime? readOnUtc}) =>
      UserNotification(
        id: id,
        userId: userId,
        kind: kind,
        title: title,
        body: body,
        route: route,
        isRead: isRead ?? this.isRead,
        createdOnUtc: createdOnUtc,
        readOnUtc: readOnUtc ?? this.readOnUtc,
      );
}

/// Sayfalı bildirim listesi (`PagedResult<UserNotificationResponse>` karşılığı).
class UserNotificationPage {
  const UserNotificationPage({required this.items, required this.totalCount});

  final List<UserNotification> items;
  final int totalCount;
}
