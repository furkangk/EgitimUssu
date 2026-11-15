import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/cubit/chat_cubit.dart';
import 'package:egitim_ussu_mobile/features/messaging/presentation/pages/chat_page.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_messaging_repository.dart';
import 'chat_polling_test.dart' show message;

void main() {
  testWidgets('only top foreground chat polls and resumes immediately', (
    t,
  ) async {
    final auth = AuthCubit(
      FakeAuthRepository(session: FakeAuthRepository.defaultSession()),
    );
    await auth.restoreSession();
    final repo = FakeMessagingRepository(messages: [message(1)]);
    final c = ChatCubit(repository: repo, currentUserId: 'test-user');
    final key = GlobalKey<NavigatorState>();
    await t.pumpWidget(
      BlocProvider.value(
        value: auth,
        child: MaterialApp(
          navigatorKey: key,
          locale: const Locale('tr'),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: ChatPage(conversationId: 'c1', cubit: c),
        ),
      ),
    );
    await t.pumpAndSettle();
    repo.store.add(message(2));
    await t.pump(const Duration(seconds: 10));
    expect(find.text('message 2'), findsOneWidget);
    key.currentState!.push(
      MaterialPageRoute<void>(
        builder: (_) => const Scaffold(body: Text('cover')),
      ),
    );
    await t.pumpAndSettle();
    final covered = repo.messageCursors.length;
    await t.pump(const Duration(seconds: 20));
    expect(repo.messageCursors.length, covered);
    repo.store.add(message(3));
    key.currentState!.pop();
    await t.pumpAndSettle();
    expect(find.text('message 3'), findsOneWidget);
    t.binding.handleAppLifecycleStateChanged(AppLifecycleState.paused);
    final paused = repo.messageCursors.length;
    repo.store.add(message(4));
    await t.pump(const Duration(seconds: 20));
    expect(repo.messageCursors.length, paused);
    t.binding.handleAppLifecycleStateChanged(AppLifecycleState.hidden);
    t.binding.handleAppLifecycleStateChanged(AppLifecycleState.inactive);
    t.binding.handleAppLifecycleStateChanged(AppLifecycleState.resumed);
    await t.pumpAndSettle();
    expect(find.text('message 4'), findsOneWidget);
    await t.pumpWidget(const SizedBox());
    await t.pumpAndSettle();
    expect(c.isClosed, true);
    await auth.close();
  });
}
