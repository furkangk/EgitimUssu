import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/cubit/chat_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/cubit/conversations_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_messaging_repository.dart';

/// P10 Task 4 (D-08/M16-1): Gelen kutusu ve sohbet ekranının durum makineleri.
///
/// Kritik ayrımlar:
/// - **Keyset sayfalama**: eskiye kaydırınca imleç en eski mesajın zamanıdır; aynı mesaj
///   iki kez listeye girmez. Offset kullanılsaydı yeni mesaj geldiğinde satırlar kayardı.
/// - **İyimser gönderim**: mesaj sunucu yanıtını beklemeden listede görünür ("gönderiliyor"),
///   hata olursa **kaybolmaz** — "tekrar dene" durumuna düşer. Kaybolsaydı kullanıcı
///   yazdığı metni yeniden yazmak zorunda kalırdı.
void main() {
  const conversationId = 'c-1';
  const me = 'me';
  final start = DateTime.utc(2026, 9, 4, 12);

  ChatMessage message(int index, {String sender = 'other'}) => ChatMessage(
    id: 'm-$index',
    conversationId: conversationId,
    senderUserId: sender,
    body: 'mesaj-$index',
    sentOnUtc: start.add(Duration(minutes: index)),
    readOnUtc: null,
    isDeleted: false,
  );

  group('ConversationsCubit', () {
    test('gelen kutusunu yukler ve toplam okunmamis sayisini hesaplar', () async {
      final repository = FakeMessagingRepository(
        inbox: <ConversationSummary>[
          ConversationSummary(
            id: 'c-1',
            counterpartUserId: 'u-1',
            counterpartRole: kParticipantRoleStudent,
            studentId: 's-1',
            lastMessageOnUtc: start,
            lastMessagePreview: 'Merhaba',
            unreadCount: 2,
            isBlocked: false,
          ),
          ConversationSummary(
            id: 'c-2',
            counterpartUserId: 'u-2',
            counterpartRole: kParticipantRoleParent,
            studentId: 's-1',
            lastMessageOnUtc: start,
            lastMessagePreview: null,
            unreadCount: 3,
            isBlocked: false,
          ),
        ],
      );
      final cubit = ConversationsCubit(repository);

      await cubit.load();

      expect(cubit.state.status, ConversationsStatus.loaded);
      expect(cubit.state.conversations, hasLength(2));
      expect(cubit.state.totalUnread, 5);
      await cubit.close();
    });

    test('bos gelen kutusu hata degil bos durumdur', () async {
      final cubit = ConversationsCubit(FakeMessagingRepository());

      await cubit.load();

      expect(cubit.state.status, ConversationsStatus.loaded);
      expect(cubit.state.isEmpty, isTrue);
      await cubit.close();
    });

    test('yukleme hatasi kullaniciya bildirilir', () async {
      final cubit = ConversationsCubit(
        FakeMessagingRepository(
          inboxError: const ApiException(message: 'Sunucuya baglanilamadi.'),
        ),
      );

      await cubit.load();

      expect(cubit.state.status, ConversationsStatus.failure);
      expect(cubit.state.errorMessage, 'Sunucuya baglanilamadi.');
      await cubit.close();
    });
  });

  group('ChatCubit', () {
    test('ilk sayfa yeniden eskiye gelir', () async {
      final repository = FakeMessagingRepository(
        messages: <ChatMessage>[for (var i = 1; i <= 5; i++) message(i)],
      );
      final cubit = ChatCubit(repository: repository, currentUserId: me);

      await cubit.load(conversationId, take: 5);

      expect(cubit.state.status, ChatStatus.loaded);
      expect(cubit.state.messages.first.body, 'mesaj-5');
      expect(cubit.state.messages.last.body, 'mesaj-1');
      await cubit.close();
    });

    test('eskiye kaydirinca ek sayfa yuklenir, tekrar olmaz', () async {
      final repository = FakeMessagingRepository(
        messages: <ChatMessage>[for (var i = 1; i <= 10; i++) message(i)],
      );
      final cubit = ChatCubit(repository: repository, currentUserId: me);

      await cubit.load(conversationId, take: 4);
      await cubit.loadMore();
      await cubit.loadMore();

      final bodies = cubit.state.messages.map((row) => row.body).toList();
      expect(bodies, hasLength(10));
      expect(bodies.toSet(), hasLength(bodies.length));
      expect(bodies.first, 'mesaj-10');
      expect(bodies.last, 'mesaj-1');
      // İmleç: ilk sayfa null, sonrakiler o ana kadarki EN ESKİ mesajın zamanı.
      expect(repository.messageCursors[0], isNull);
      expect(repository.messageCursors[1], start.add(const Duration(minutes: 7)));
      await cubit.close();
    });

    test('sunucu take kadar satir donmezse sayfa sonu isaretlenir', () async {
      final repository = FakeMessagingRepository(
        messages: <ChatMessage>[for (var i = 1; i <= 3; i++) message(i)],
      );
      final cubit = ChatCubit(repository: repository, currentUserId: me);

      await cubit.load(conversationId, take: 10);

      expect(cubit.state.hasMore, isFalse);

      await cubit.loadMore();

      // Sayfa sonu bilindiğinde ikinci bir istek atılmaz.
      expect(repository.messageCursors, hasLength(1));
      await cubit.close();
    });

    test('iyimser gonderim mesaji hemen listeye koyar', () async {
      final repository = FakeMessagingRepository()
        ..nextMessageId = 'server-9'
        ..nextSentOnUtc = start.add(const Duration(minutes: 30));
      final cubit = ChatCubit(repository: repository, currentUserId: me);
      await cubit.load(conversationId);

      final future = cubit.send('Merhaba hocam');

      expect(cubit.state.messages.first.body, 'Merhaba hocam');
      expect(cubit.state.messages.first.status, ChatMessageStatus.sending);

      await future;

      expect(cubit.state.messages.first.id, 'server-9');
      expect(cubit.state.messages.first.status, ChatMessageStatus.sent);
      expect(cubit.state.messages, hasLength(1));
      await cubit.close();
    });

    test('gonderim hatasinda mesaj kaybolmaz, tekrar denenebilir', () async {
      final repository = FakeMessagingRepository(
        sendError: const ApiException(message: 'Sunucuya baglanilamadi.'),
      );
      final cubit = ChatCubit(repository: repository, currentUserId: me);
      await cubit.load(conversationId);

      await cubit.send('Merhaba');

      final failed = cubit.state.messages.single;
      expect(failed.status, ChatMessageStatus.failed);
      expect(failed.body, 'Merhaba');
      expect(failed.failureMessage, 'Sunucuya baglanilamadi.');
      await cubit.close();
    });

    test('tekrar dene ayni metni yeniden gonderir ve kopya birakmaz', () async {
      final repository = FakeMessagingRepository(
        sendError: const ApiException(message: 'Sunucuya baglanilamadi.'),
      );
      final cubit = ChatCubit(repository: repository, currentUserId: me);
      await cubit.load(conversationId);
      await cubit.send('Merhaba');
      final failedId = cubit.state.messages.single.id;

      repository
        ..sendError = null
        ..nextMessageId = 'server-2';
      await cubit.retry(failedId);

      expect(cubit.state.messages, hasLength(1));
      expect(cubit.state.messages.single.id, 'server-2');
      expect(cubit.state.messages.single.status, ChatMessageStatus.sent);
      await cubit.close();
    });

    test('bos mesaj gonderilmez', () async {
      final repository = FakeMessagingRepository();
      final cubit = ChatCubit(repository: repository, currentUserId: me);
      await cubit.load(conversationId);

      await cubit.send('   ');

      expect(repository.sentBodies, isEmpty);
      expect(cubit.state.messages, isEmpty);
      await cubit.close();
    });

    test('ekran acilinca okundu isaretlenir', () async {
      final repository = FakeMessagingRepository(
        messages: <ChatMessage>[message(1)],
      );
      final cubit = ChatCubit(repository: repository, currentUserId: me);

      await cubit.load(conversationId);

      expect(repository.markReadCalls, <String>[conversationId]);
      await cubit.close();
    });

    test('okundu isaretleme hatasi ekrani bozmaz', () async {
      final repository = FakeMessagingRepository(
        messages: <ChatMessage>[message(1)],
        markReadError: const ApiException(message: 'Olmadi.'),
      );
      final cubit = ChatCubit(repository: repository, currentUserId: me);

      await cubit.load(conversationId);

      // Okundu işareti yan etkidir: başarısız olsa da mesajlar görünür kalır.
      expect(cubit.state.status, ChatStatus.loaded);
      expect(cubit.state.messages, hasLength(1));
      await cubit.close();
    });

    test('engelleme gonderimi kapatir, kaldirma geri acar', () async {
      final repository = FakeMessagingRepository();
      final cubit = ChatCubit(repository: repository, currentUserId: me);
      await cubit.load(conversationId);

      await cubit.setBlocked(true);
      expect(cubit.state.canSend, isFalse);
      expect(cubit.state.conversation?.blockedByMe, isTrue);

      await cubit.setBlocked(false);
      expect(cubit.state.canSend, isTrue);
      await cubit.close();
    });

    test('karsi tarafin koydugu engelde kaldirma dugmesi gosterilmez', () async {
      final repository = FakeMessagingRepository(
        detail: ConversationDetail(
          id: conversationId,
          teacherUserId: 'teacher-1',
          counterpartUserId: 'other',
          counterpartRole: kParticipantRoleStudent,
          studentId: 's-1',
          lastMessageOnUtc: start,
          lastMessagePreview: null,
          isBlocked: true,
          blockedByMe: false,
        ),
      );
      final cubit = ChatCubit(repository: repository, currentUserId: me);
      await cubit.load(conversationId, detail: repository.detail);

      expect(cubit.state.canSend, isFalse);
      expect(cubit.state.canUnblock, isFalse);
      await cubit.close();
    });

    test('silinmis mesaj listede kalir ama icerigi gizlidir', () async {
      final repository = FakeMessagingRepository(
        messages: <ChatMessage>[
          ChatMessage(
            id: 'm-1',
            conversationId: conversationId,
            senderUserId: 'other',
            body: null,
            sentOnUtc: start,
            readOnUtc: null,
            isDeleted: true,
          ),
        ],
      );
      final cubit = ChatCubit(repository: repository, currentUserId: me);

      await cubit.load(conversationId);

      expect(cubit.state.messages.single.isDeleted, isTrue);
      expect(cubit.state.messages.single.body, isNull);
      await cubit.close();
    });
  });
}
