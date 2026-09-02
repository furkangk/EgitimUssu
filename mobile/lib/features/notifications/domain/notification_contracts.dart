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
