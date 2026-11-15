import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/domain/messaging_contracts.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/cubit/chat_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/cubit/conversations_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/pages/chat_page.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/pages/messaging_inbox_page.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_messaging_repository.dart';

/// P10 Task 4 (D-08/M16-1): Gelen kutusu + sohbet ekranı.
///
/// Sözleşme kontrolleri: okunmamış rozeti gerçek sayıyı gösterir, engelli sohbette
/// yazma alanı **kapanır** (kullanıcı boşuna yazmasın), silinmiş mesaj listede kalır
/// ama içeriği gösterilmez ve başarısız gönderim "tekrar dene" ile geri alınabilir.
void main() {
  const conversationId = 'c-1';
  final start = DateTime.utc(2026, 9, 4, 12);

  ConversationSummary summary({int unread = 0, bool blocked = false}) =>
      ConversationSummary(
        id: conversationId,
        counterpartUserId: 'other',
        counterpartRole: kParticipantRoleStudent,
        studentId: 's-1',
        lastMessageOnUtc: start,
        lastMessagePreview: 'Yarınki dersi konuşalım',
        unreadCount: unread,
        isBlocked: blocked,
      );

  group('MessagingInboxPage', () {
    testWidgets('sohbet yoksa bos durum gosterilir', (tester) async {
      await _pumpInbox(tester, FakeMessagingRepository());

      expect(find.text('Henüz mesajın yok'), findsOneWidget);
    });

    testWidgets('sohbet satiri onizleme ve okunmamis rozetini gosterir', (
      tester,
    ) async {
      await _pumpInbox(
        tester,
        FakeMessagingRepository(
          inbox: <ConversationSummary>[summary(unread: 3)],
        ),
      );

      expect(find.text('Yarınki dersi konuşalım'), findsOneWidget);
      expect(find.text('3'), findsOneWidget);
      expect(find.text('3 okunmamış mesaj'), findsOneWidget);
    });

    testWidgets('okunmamis yoksa rozet cikmaz', (tester) async {
      await _pumpInbox(
        tester,
        FakeMessagingRepository(inbox: <ConversationSummary>[summary()]),
      );

      expect(find.text('0'), findsNothing);
      expect(find.textContaining('okunmamış'), findsNothing);
    });

    testWidgets('yukleme hatasi tekrar dene ile gosterilir', (tester) async {
      await _pumpInbox(
        tester,
        FakeMessagingRepository(
          inboxError: const ApiException(message: 'Sunucuya baglanilamadi.'),
        ),
      );

      expect(find.text('Sunucuya baglanilamadi.'), findsOneWidget);
    });
  });

  group('ChatPage', () {
    testWidgets('mesajlar balonlarda gorunur ve yazma alani aciktir', (
      tester,
    ) async {
      final repository = FakeMessagingRepository(
        messages: <ChatMessage>[
          ChatMessage(
            id: 'm-1',
            conversationId: conversationId,
            senderUserId: 'other',
            body: 'Merhaba',
            sentOnUtc: start,
            readOnUtc: null,
            isDeleted: false,
          ),
        ],
      );

      await _pumpChat(tester, repository);

      expect(find.text('Merhaba'), findsOneWidget);
      expect(find.byType(TextField), findsOneWidget);
    });

    testWidgets('silinmis mesaj listede kalir ama icerigi gizlenir', (
      tester,
    ) async {
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

      await _pumpChat(tester, repository);

      expect(find.text('Bu mesaj silindi'), findsOneWidget);
    });

    testWidgets('engelli sohbette yazma alani kapalidir', (tester) async {
      final repository = FakeMessagingRepository(
        messages: <ChatMessage>[
          ChatMessage(
            id: 'm-1',
            conversationId: conversationId,
            senderUserId: 'other',
            body: 'Merhaba',
            sentOnUtc: start,
            readOnUtc: null,
            isDeleted: false,
          ),
        ],
        detail: ConversationDetail(
          id: conversationId,
          teacherUserId: 'teacher-1',
          counterpartUserId: 'other',
          counterpartRole: kParticipantRoleStudent,
          studentId: 's-1',
          lastMessageOnUtc: start,
          lastMessagePreview: null,
          isBlocked: true,
          blockedByMe: true,
        ),
      );

      await _pumpChat(tester, repository, detail: repository.detail);

      expect(find.byType(TextField), findsNothing);
      expect(
        find.text('Bu sohbet engellendiği için mesaj gönderemezsin.'),
        findsOneWidget,
      );
    });

    testWidgets('gonderim basarisiz olursa mesaj kalir ve tekrar dene cikar', (
      tester,
    ) async {
      final repository = FakeMessagingRepository(
        sendError: const ApiException(message: 'Sunucuya baglanilamadi.'),
      );

      await _pumpChat(tester, repository);

      await tester.enterText(find.byType(TextField), 'Merhaba');
      await tester.tap(find.byIcon(Icons.send_rounded));
      await tester.pumpAndSettle();

      expect(find.text('Merhaba'), findsOneWidget);
      expect(find.text('Tekrar dene'), findsOneWidget);
    });
  });
}

Future<void> _pumpInbox(
  WidgetTester tester,
  FakeMessagingRepository repository,
) async {
  await _pump(
    tester,
    MessagingInboxPage(cubit: ConversationsCubit(repository)),
  );
}

Future<void> _pumpChat(
  WidgetTester tester,
  FakeMessagingRepository repository, {
  ConversationDetail? detail,
}) async {
  await _pump(
    tester,
    ChatPage(
      conversationId: 'c-1',
      detail: detail,
      cubit: ChatCubit(repository: repository, currentUserId: 'test-user'),
    ),
  );
}

Future<void> _pump(WidgetTester tester, Widget page) async {
  final authCubit = AuthCubit(
    FakeAuthRepository(session: FakeAuthRepository.defaultSession()),
  );
  await authCubit.restoreSession();
  addTearDown(authCubit.close);

  await tester.pumpWidget(
    BlocProvider<AuthCubit>.value(
      value: authCubit,
      child: MaterialApp(
        locale: const Locale('tr'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: page,
      ),
    ),
  );
  await tester.pumpAndSettle();
}
