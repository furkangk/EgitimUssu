import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/stale_data_banner.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  for (final locale in ['tr', 'en']) {
    testWidgets(
      'stale data warning includes a readable saved time in $locale',
      (tester) async {
        await tester.pumpWidget(
          MaterialApp(
            locale: Locale(locale),
            supportedLocales: AppLocalizations.supportedLocales,
            localizationsDelegates: AppLocalizations.localizationsDelegates,
            home: Scaffold(
              body: StaleDataBanner(savedAt: DateTime(2026, 11, 15, 10, 30)),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.byIcon(Icons.cloud_off_outlined), findsOneWidget);
        expect(
          find.textContaining(locale == 'tr' ? 'kaydedilen' : 'saved'),
          findsOneWidget,
        );
        expect(find.textContaining('10:30'), findsOneWidget);
      },
    );
  }
}
