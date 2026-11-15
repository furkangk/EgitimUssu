import 'dart:async';
import 'package:egitim_ussu_mobile/core/push/push_event_bus.dart';
import 'package:egitim_ussu_mobile/core/push/push_service.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/cubit/conversations_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notification_center_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers/fake_messaging_repository.dart';
import '../../helpers/fake_notification_repository.dart';

void main() {
  test('foreground source refreshes badge center and inbox from API', () async {
    final bus = PushEventBus()..activate('me');
    final source = StreamController<Map<String, dynamic>>();
    final service = PushService(
      repository: Tokens(),
      tokenProvider: () async => null,
      permissionRequester: () async => false,
      platform: DevicePlatform.android,
      eventBus: bus,
      foregroundMessages: source.stream,
    );
    final repo = Notifications();
    final messages = FakeMessagingRepository();
    final badge = NotificationsCubit(repo, eventBus: bus);
    final center = NotificationCenterCubit(repo, eventBus: bus);
    final inbox = ConversationsCubit(messages, eventBus: bus);
    await badge.load('me');
    await center.load('me');
    await center.setOnlyUnread(true);
    await inbox.load();
    repo.count = 3;
    messages.inbox = [
      ConversationSummary(
        id: 'c1',
        counterpartUserId: 'other',
        counterpartRole: 'Teacher',
        studentId: 's',
        lastMessageOnUtc: DateTime.utc(2026),
        lastMessagePreview: 'new',
        unreadCount: 2,
        isBlocked: false,
      ),
    ];
    source.add({'route': '/messages/c1'});
    await Future<void>.delayed(Duration.zero);
    expect(badge.state.unreadCount, 3);
    expect(center.state.unreadCount, 3);
    expect(center.state.onlyUnread, true);
    expect(inbox.state.totalUnread, 2);
    await badge.close();
    await center.close();
    await inbox.close();
    await service.dispose();
    await source.close();
    bus.dispose();
  });
  test('new session push is not held behind old account refresh', () async {
    final bus = PushEventBus()..activate('a');
    final repo = DelayedUnread();
    final badge = NotificationsCubit(repo, eventBus: bus);
    bus.publish({'kind': 'System'});
    bus.activate('b');
    await badge.load('b');
    repo.count = 7;
    bus.publish({'kind': 'System'});
    await Future<void>.delayed(Duration.zero);
    expect(badge.state.unreadCount, 7);
    repo.old.complete(99);
    await Future<void>.delayed(Duration.zero);
    expect(badge.state.unreadCount, 7);
    await badge.close();
    bus.dispose();
  });
  test('late foreground list cannot undo a successful read', () async {
    final repo = DelayedCenter();
    final center = NotificationCenterCubit(repo);
    await center.load('me');
    final oldPage = repo.page();
    repo.pending = Completer<UserNotificationPage>();
    final refresh = center.load('me', silent: true);
    final pending = repo.pending!;
    repo.pending = null;
    await center.markRead('n');
    pending.complete(oldPage);
    await refresh;
    expect(center.state.items.single.isRead, true);
    expect(center.state.unreadCount, 0);
    await center.close();
  });
  test('late badge response cannot undo center read count', () async {
    final repo = DelayedUnread();
    final badge = NotificationsCubit(repo);
    badge.setUnreadCount(1);
    final refresh = badge.refreshBadge('a');
    badge.setUnreadCount(0);
    repo.old.complete(1);
    await refresh;
    expect(badge.state.unreadCount, 0);
    await badge.close();
  });
  test('failed silent refresh preserves pagination retry cursor', () async {
    final repo = DelayedCenter();
    final center = NotificationCenterCubit(repo);
    await center.load('me');
    repo.fail = true;
    await center.load('me', silent: true);
    expect(center.state.nextCursor, 'older');
    repo.fail = false;
    await center.loadMore();
    expect(repo.cursors.last, 'older');
    await center.close();
  });
  test('logout during permission prevents late registration', () async {
    final permission = Completer<bool>();
    final repo = Tokens();
    final service = PushService(
      repository: repo,
      tokenProvider: () async => 'token',
      permissionRequester: () => permission.future,
      platform: DevicePlatform.android,
    );
    final pending = service.registerForUser('old');
    await service.unregister();
    permission.complete(true);
    expect(await pending, isNull);
    expect(repo.users, isEmpty);
    await service.dispose();
  });
  test(
    'late saved token is removed before next account registration',
    () async {
      final repo = Tokens()..pending = Completer<void>();
      final service = PushService(
        repository: repo,
        tokenProvider: () async => 'token',
        permissionRequester: () async => true,
        platform: DevicePlatform.android,
      );
      final old = service.registerForUser('old');
      await Future<void>.delayed(Duration.zero);
      final logout = service.unregister();
      final next = service.registerForUser('new');
      repo.pending!.complete();
      repo.pending = null;
      await old;
      await logout;
      await next;
      expect(repo.actions, ['save:old', 'remove:token', 'save:new']);
      expect(service.currentToken, 'token');
      await service.dispose();
    },
  );
}

class Notifications extends FakeNotificationRepository {
  int count = 0;
  @override
  Future<int> unreadNotificationCount(String userId) async => count;
}

class Tokens implements DeviceTokenRepository {
  final users = <String>[];
  final actions = <String>[];
  Completer<void>? pending;
  @override
  Future<void> register({
    required String userId,
    required String token,
    required DevicePlatform platform,
    String? deviceName,
  }) async {
    users.add(userId);
    actions.add('save:$userId');
    await pending?.future;
  }

  @override
  Future<void> unregister(String token) async {
    actions.add('remove:$token');
  }
}

class DelayedUnread extends FakeNotificationRepository {
  final old = Completer<int>();
  int count = 0;
  @override
  Future<int> unreadNotificationCount(String userId) =>
      userId == 'a' ? old.future : Future.value(count);
}

class DelayedCenter extends FakeNotificationRepository {
  Completer<UserNotificationPage>? pending;
  bool read = false, fail = false;
  final cursors = <String?>[];
  UserNotificationPage page() => UserNotificationPage(
    items: [
      UserNotification(
        id: 'n',
        userId: 'me',
        kind: 'System',
        title: 'title',
        body: 'body',
        route: null,
        isRead: read,
        createdOnUtc: DateTime.utc(2026),
      ),
    ],
    totalCount: 2,
    nextCursor: 'older',
  );
  @override
  Future<UserNotificationPage> listUserNotifications({
    required String userId,
    bool onlyUnread = false,
    int skip = 0,
    int take = 20,
    String? after,
    int? limit,
  }) async {
    cursors.add(after);
    if (fail) throw Exception('offline');
    return pending?.future ?? page();
  }

  @override
  Future<int> unreadNotificationCount(String userId) async => read ? 0 : 1;
  @override
  Future<void> markNotificationRead(String id) async {
    read = true;
  }
}
