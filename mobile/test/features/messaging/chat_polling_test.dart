import 'dart:async';
import 'package:egitim_ussu_mobile/core/push/push_event_bus.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/cubit/chat_cubit.dart';
import 'package:fake_async/fake_async.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers/fake_messaging_repository.dart';

ChatMessage message(int n) => ChatMessage(
  id: 'm$n',
  conversationId: 'c1',
  senderUserId: 'other',
  body: 'message $n',
  sentOnUtc: DateTime.utc(2026, 11, 15, 10, n),
  readOnUtc: null,
  isDeleted: false,
);
void main() {
  test('poll updates after 10 seconds and stops on close', () {
    fakeAsync((t) {
      final repo = FakeMessagingRepository(messages: [message(1)]);
      final c = ChatCubit(repository: repo, currentUserId: 'me');
      c.load('c1');
      t.flushMicrotasks();
      c.setActive(true);
      repo.store.add(message(2));
      t.elapse(const Duration(seconds: 9));
      t.flushMicrotasks();
      expect(c.state.messages.length, 1);
      t.elapse(const Duration(seconds: 1));
      t.flushMicrotasks();
      expect(c.state.messages.map((m) => m.id), ['m2', 'm1']);
      c.close();
      t.flushMicrotasks();
      final calls = repo.messageCursors.length;
      t.elapse(const Duration(seconds: 30));
      t.flushMicrotasks();
      expect(repo.messageCursors.length, calls);
    });
  });
  test('targeted push catches up across pages retaining loaded history', () {
    fakeAsync((t) {
      final bus = PushEventBus()..activate('me');
      final repo = FakeMessagingRepository(
        messages: [message(0), message(1), message(2)],
      );
      final c = ChatCubit(repository: repo, currentUserId: 'me', eventBus: bus);
      c.load('c1', take: 2);
      t.flushMicrotasks();
      c.loadMore();
      t.flushMicrotasks();
      c.setActive(true);
      repo.store.addAll([message(3), message(4), message(5), message(6)]);
      bus.publish({'route': '/messages/other'});
      t.flushMicrotasks();
      expect(c.state.messages.length, 3);
      bus.publish({'route': '/messages/c1'});
      t.flushMicrotasks();
      expect(c.state.messages.map((m) => m.id), [
        'm6',
        'm5',
        'm4',
        'm3',
        'm2',
        'm1',
        'm0',
      ]);
      expect(repo.markReadCalls.length, greaterThan(1));
      c.close();
      t.flushMicrotasks();
      bus.dispose();
    });
  });
  test('inactive and logged out chats reject polling and late results', () {
    fakeAsync((t) {
      final bus = PushEventBus()..activate('me');
      final repo = DelayedMessages();
      final c = ChatCubit(repository: repo, currentUserId: 'me', eventBus: bus);
      c.load('c1');
      t.flushMicrotasks();
      c.setActive(true);
      c.setActive(false);
      t.elapse(const Duration(seconds: 30));
      t.flushMicrotasks();
      expect(repo.messageCursors.length, 1);
      c.setActive(true);
      t.flushMicrotasks();
      repo.pending = Completer<List<ChatMessage>>();
      c.refreshLatest();
      bus.activate(null);
      repo.pending!.complete([message(9)]);
      t.flushMicrotasks();
      expect(c.state.messages.any((m) => m.id == 'm9'), false);
      final calls = repo.messageCursors.length;
      t.elapse(const Duration(seconds: 30));
      t.flushMicrotasks();
      expect(repo.messageCursors.length, calls);
      c.close();
      t.flushMicrotasks();
      bus.dispose();
    });
  });
  test('push burst queues one followup while refresh is running', () {
    fakeAsync((t) {
      final bus = PushEventBus()..activate('me');
      final repo = DelayedMessages();
      final c = ChatCubit(repository: repo, currentUserId: 'me', eventBus: bus);
      c.load('c1');
      t.flushMicrotasks();
      c.setActive(true);
      t.flushMicrotasks();
      repo.pending = Completer<List<ChatMessage>>();
      c.refreshLatest();
      bus.publish({'conversationId': 'c1'});
      bus.publish({'conversationId': 'c1'});
      final pending = repo.pending!;
      repo.pending = null;
      repo.store = [message(2)];
      pending.complete([message(1)]);
      t.flushMicrotasks();
      t.flushMicrotasks();
      expect(c.state.messages.first.id, 'm2');
      c.close();
      t.flushMicrotasks();
      bus.dispose();
    });
  });
}

class DelayedMessages extends FakeMessagingRepository {
  Completer<List<ChatMessage>>? pending;
  @override
  Future<List<ChatMessage>> messages({
    required String conversationId,
    DateTime? beforeUtc,
    int take = 30,
  }) =>
      pending?.future ??
      super.messages(
        conversationId: conversationId,
        beforeUtc: beforeUtc,
        take: take,
      );
}
