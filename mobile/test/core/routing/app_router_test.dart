import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_auth_repository.dart';

void main() {
  testWidgets('redirects unauthenticated user to welcome page', (tester) async {
    final cubit = AuthCubit(FakeAuthRepository());
    await cubit.restoreSession();
    final router = AppRouter(authCubit: cubit, initialLocation: '/dashboard');

    await tester.pumpWidget(
      BlocProvider<AuthCubit>.value(
        value: cubit,
        child: MaterialApp.router(
          // D-19: Sayfalar AppLocalizations'a bağlı; delegate olmadan build edilemezler.
          // Uygulama gibi Türkçe koş: yerel ayar verilmezse test ortamının varsayılanı
          // (en_US) devreye girer ve İngilizce değerler render edilir.
          locale: const Locale('tr', 'TR'),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          routerConfig: router.router,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('EğitimÜssü'), findsOneWidget);
    expect(
      find.text('Özel ders süreçlerinizi tek bir yerde yönetin.'),
      findsOneWidget,
    );
    expect(find.text('Kayıt Ol'), findsOneWidget);

    router.dispose();
    await cubit.close();
  });
}
