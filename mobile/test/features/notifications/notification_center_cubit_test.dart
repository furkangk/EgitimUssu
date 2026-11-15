import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notification_center_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notification_center_state.dart';
import 'package:flutter_test/flutter_test.dart';

/// P03 Task 6 (M11-3/M11-4/D-11): Bildirim merkezi — liste, okunmamış rozeti, okundu işareti.
void main() {
  group('NotificationCenterCubit', () {
    test('load listeyi ve okunmamis sayisini doldurur', () async {
      final repository = _FakeNotificationRepository(
        notifications: [_notification('n-1'), _notification('n-2', isRead: true)],
        unreadCount: 1,
      );
      final cubit = NotificationCenterCubit(repository);

      await cubit.load('user-1');

      expect(cubit.state.status, NotificationCenterStatus.loaded);
      expect(cubit.state.items.length, 2);
      expect(cubit.state.unreadCount, 1);
      await cubit.close();
    });

    test('markRead okunmamis sayisini bir azaltir ve ogeyi okundu yapar', () async {
      final repository = _FakeNotificationRepository(
        notifications: [_notification('n-1'), _notification('n-2')],
        unreadCount: 2,
      );
      final cubit = NotificationCenterCubit(repository);
      await cubit.load('user-1');

      await cubit.markRead('n-1');

      expect(cubit.state.unreadCount, 1);
      expect(cubit.state.items.firstWhere((n) => n.id == 'n-1').isRead, isTrue);
      expect(repository.markedRead, ['n-1']);
      await cubit.close();
    });

    test('zaten okunmus ogede sayi azalmaz ve istek gitmez', () async {
      final repository = _FakeNotificationRepository(
        notifications: [_notification('n-1', isRead: true)],
        unreadCount: 0,
      );
      final cubit = NotificationCenterCubit(repository);
      await cubit.load('user-1');

      await cubit.markRead('n-1');

      expect(cubit.state.unreadCount, 0);
      expect(repository.markedRead, isEmpty);
      await cubit.close();
    });

    test('markRead basarisiz olursa iyimser guncelleme geri alinir', () async {
      final repository = _FakeNotificationRepository(
        notifications: [_notification('n-1')],
        unreadCount: 1,
        failMarkRead: true,
      );
      final cubit = NotificationCenterCubit(repository);
      await cubit.load('user-1');

      await cubit.markRead('n-1');

      expect(cubit.state.items.single.isRead, isFalse);
      expect(cubit.state.unreadCount, 1);
      await cubit.close();
    });

    test('sadece okunmamislari goster filtresi istegi degistirir', () async {
      final repository = _FakeNotificationRepository(
        notifications: [_notification('n-1')],
        unreadCount: 1,
      );
      final cubit = NotificationCenterCubit(repository);
      await cubit.load('user-1');

      await cubit.setOnlyUnread(true);

      expect(cubit.state.onlyUnread, isTrue);
      expect(repository.lastOnlyUnread, isTrue);
      await cubit.close();
    });

    test('API hatasinda failure durumuna gecer', () async {
      final repository = _FakeNotificationRepository(failList: true);
      final cubit = NotificationCenterCubit(repository);

      await cubit.load('user-1');

      expect(cubit.state.status, NotificationCenterStatus.failure);
      expect(cubit.state.errorMessage, isNotNull);
      await cubit.close();
    });
  });

  group('UserNotification', () {
    test('rota yoksa null kalir', () {
      expect(_notification('n-1', route: null).route, isNull);
    });
  });
}

UserNotification _notification(
  String id, {
  bool isRead = false,
  String? route = '/scheduling',
}) => UserNotification(
  id: id,
  userId: 'user-1',
  kind: 'AssignmentCreated',
  title: 'Yeni ödev',
  body: 'Matematik ödevi eklendi',
  route: route,
  isRead: isRead,
  createdOnUtc: DateTime.utc(2026, 9, 3, 10),
  readOnUtc: isRead ? DateTime.utc(2026, 9, 3, 11) : null,
);

class _FakeNotificationRepository implements NotificationRepository {
  _FakeNotificationRepository({
    this.notifications = const [],
    this.unreadCount = 0,
    this.failList = false,
    this.failMarkRead = false,
  });

  final List<UserNotification> notifications;
  final int unreadCount;
  final bool failList;
  final bool failMarkRead;

  final List<String> markedRead = [];
  bool? lastOnlyUnread;

  @override
  Future<List<LessonReminder>> listReminders({
    required String teacherUserId,
    bool activeOnly = false,
  }) async => const [];

  @override
  Future<UserNotificationPage> listUserNotifications({
    required String userId,
    bool onlyUnread = false,
    int skip = 0,
    int take = 20,
    String? after,
    int? limit,
  }) async {
    if (failList) {
      throw const ApiException(message: 'Bildirimler yüklenemedi.');
    }
    lastOnlyUnread = onlyUnread;
    final items = onlyUnread
        ? notifications.where((n) => !n.isRead).toList()
        : notifications;
    return UserNotificationPage(items: items, totalCount: items.length);
  }

  @override
  Future<int> unreadNotificationCount(String userId) async {
    if (failList) {
      throw const ApiException(message: 'Bildirimler yüklenemedi.');
    }
    return unreadCount;
  }

  @override
  Future<void> markNotificationRead(String notificationId) async {
    if (failMarkRead) {
      throw const ApiException(message: 'İşaretlenemedi.');
    }
    markedRead.add(notificationId);
  }
}
