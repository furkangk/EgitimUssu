import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/routing/deep_links.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/email_verification_page.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import '../../helpers/fake_auth_repository.dart';

void main() {
  setUp(() {
    injector.registerFactory<AuthRepository>(FakeAuthRepository.new);
  });

  tearDown(injector.reset);

  group('normalizeDeepLink', () {
    test('sema-bazli baglantida host yola tasinir', () {
      expect(
        normalizeDeepLink(Uri.parse('egitimussu://verify-email?token=ABC123')),
        '/verify-email?token=ABC123',
      );
    });

    test('sifre sifirlama baglantisi token varsa onay ekranina gider', () {
      expect(
        normalizeDeepLink(Uri.parse('egitimussu://password-reset?token=T1')),
        '/password-reset/confirm?token=T1',
      );
    });

    test('token yoksa sifre sifirlama istek ekranina gider', () {
      expect(
        normalizeDeepLink(Uri.parse('egitimussu://password-reset')),
        '/password-reset',
      );
    });

    test('bilinmeyen host icin null doner', () {
      expect(normalizeDeepLink(Uri.parse('egitimussu://baska-sey')), isNull);
    });

    test('zaten yol tasiyan uygulama-ici rotalara dokunmaz', () {
      expect(normalizeDeepLink(Uri.parse('/verify-email?token=X')), isNull);
    });
  });

  testWidgets(
    'egitimussu://verify-email?token=ABC dogrulama ekranini token ile acar',
    (tester) async {
      final router = GoRouter(
        initialLocation: normalizeDeepLink(
          Uri.parse('egitimussu://verify-email?token=ABC123'),
        )!,
        routes: <RouteBase>[
          GoRoute(
            path: '/verify-email',
            builder: (context, state) => EmailVerificationPage(
              initialToken: state.uri.queryParameters['token'] ?? '',
            ),
          ),
        ],
      );

      await tester.pumpWidget(
        MaterialApp.router(
          // D-19: Sayfalar AppLocalizations'a bağlı; delegate olmadan build edilemezler.
          // Uygulama gibi Türkçe koş: yerel ayar verilmezse test ortamının varsayılanı
          // (en_US) devreye girer ve İngilizce değerler render edilir.
          locale: const Locale('tr', 'TR'),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          routerConfig: router,
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('ABC123'), findsOneWidget);
    },
  );
}
