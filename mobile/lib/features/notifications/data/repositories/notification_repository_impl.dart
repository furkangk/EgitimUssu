import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/notifications/data/models/notification_model.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';

class NotificationRepositoryImpl implements NotificationRepository {
  NotificationRepositoryImpl({
    required ApiClient apiClient,
    required AppConfig config,
  })  : _apiClient = apiClient,
        _config = config;

  final ApiClient _apiClient;
  final AppConfig _config;

  @override
  Future<List<LessonReminder>> listReminders({
    required String teacherUserId,
    bool activeOnly = false,
  }) async {
    if (_config.isMockFallbackEnabled('notifications')) {
      return _mockReminders(teacherUserId);
    }
    try {
      final response = await _apiClient.getList(
        '/api/notifications/teachers/$teacherUserId/lesson-reminders',
        queryParameters: <String, dynamic>{'activeOnly': activeOnly},
      );
      return response
          .whereType<Map<String, dynamic>>()
          .map(LessonReminderModel.fromJson)
          .toList();
    } on ApiException {
      rethrow;
    }
  }

  List<LessonReminder> _mockReminders(String teacherUserId) {
    final now = DateTime.now().toUtc();
    return [
      LessonReminderModel(
        id: 'reminder-1',
        lessonScheduleId: 'lesson-1',
        teacherUserId: teacherUserId,
        studentId: 'student-1',
        title: 'Ders Hatırlatması',
        message: 'Mehmet Demir ile Matematik dersiniz 30 dakika sonra başlıyor.',
        scheduledLessonStartAtUtc: now.add(const Duration(minutes: 30)),
        remindAtUtc: now.subtract(const Duration(minutes: 1)),
        channel: 'InApp',
        status: 'Pending',
        createdOnUtc: now.subtract(const Duration(hours: 1)),
      ),
      LessonReminderModel(
        id: 'reminder-2',
        lessonScheduleId: 'lesson-2',
        teacherUserId: teacherUserId,
        studentId: 'student-2',
        title: 'Ders Hatırlatması',
        message: 'Ece Ak ile Fizik dersiniz 2 saat sonra başlıyor.',
        scheduledLessonStartAtUtc: now.add(const Duration(hours: 2)),
        remindAtUtc: now.add(const Duration(hours: 1)),
        channel: 'InApp',
        status: 'Pending',
        createdOnUtc: now.subtract(const Duration(hours: 2)),
      ),
      LessonReminderModel(
        id: 'reminder-3',
        lessonScheduleId: 'lesson-4',
        teacherUserId: teacherUserId,
        studentId: 'student-4',
        title: 'Ders Hatırlatması',
        message: 'Zeynep Yılmaz ile Matematik dersiniz yarın saat 17:00\'da.',
        scheduledLessonStartAtUtc: now.add(const Duration(days: 1, hours: 5)),
        remindAtUtc: now.add(const Duration(days: 1)),
        channel: 'InApp',
        status: 'Pending',
        createdOnUtc: now.subtract(const Duration(hours: 3)),
      ),
      LessonReminderModel(
        id: 'reminder-4',
        lessonScheduleId: 'lesson-6',
        teacherUserId: teacherUserId,
        studentId: 'student-1',
        title: 'Ders Tamamlandı',
        message: 'Mehmet Demir ile Geometri dersi tamamlandı. Ders notunu eklemeyi unutmayın.',
        scheduledLessonStartAtUtc: now.subtract(const Duration(days: 1, hours: 2)),
        remindAtUtc: now.subtract(const Duration(days: 1, hours: 1)),
        channel: 'InApp',
        status: 'Sent',
        createdOnUtc: now.subtract(const Duration(days: 1, hours: 3)),
      ),
      LessonReminderModel(
        id: 'reminder-5',
        lessonScheduleId: 'lesson-7',
        teacherUserId: teacherUserId,
        studentId: 'student-2',
        title: 'Ders Tamamlandı',
        message: 'Ece Ak ile Kimya dersi tamamlandı.',
        scheduledLessonStartAtUtc: now.subtract(const Duration(days: 2, hours: 3)),
        remindAtUtc: now.subtract(const Duration(days: 2, hours: 2)),
        channel: 'InApp',
        status: 'Sent',
        createdOnUtc: now.subtract(const Duration(days: 2, hours: 4)),
      ),
      LessonReminderModel(
        id: 'reminder-6',
        lessonScheduleId: 'lesson-3',
        teacherUserId: teacherUserId,
        studentId: 'student-3',
        title: 'Ders Hatırlatması',
        message: 'Ali Kaya ile Biyoloji dersiniz öbür gün saat 14:00\'da.',
        scheduledLessonStartAtUtc: now.add(const Duration(days: 2, hours: 4)),
        remindAtUtc: now.add(const Duration(days: 2, hours: 2)),
        channel: 'InApp',
        status: 'Pending',
        createdOnUtc: now.subtract(const Duration(hours: 5)),
      ),
    ];
  }
}
