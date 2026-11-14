import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/widgets/occurrence_scope_sheet.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// P17 T1 / N-01: tekrarlı derste kapsam seçimi — varsayılan "Yalnız bu ders".
Future<OccurrenceScope?> _open(
  WidgetTester tester,
  Future<void> Function() act,
) async {
  OccurrenceScope? picked;
  var closed = false;
  await tester.pumpWidget(
    MaterialApp(
      locale: const Locale('tr'),
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: Builder(
        builder: (context) => Scaffold(
          body: TextButton(
            onPressed: () async {
              picked = await showOccurrenceScopeSheet(context);
              closed = true;
            },
            child: const Text('aç'),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('aç'));
  await tester.pumpAndSettle();
  await act();
  await tester.pumpAndSettle();
  expect(closed, isTrue, reason: 'Sayfa kapanmalıydı.');
  return picked;
}

void main() {
  testWidgets('defaults to "Yalnız bu ders"', (tester) async {
    final picked = await _open(tester, () => tester.tap(find.text('Devam et')));

    expect(picked, OccurrenceScope.single);
  });

  testWidgets('shows the three scopes in Turkish', (tester) async {
    await _open(tester, () async {
      expect(find.text('Hangi dersler etkilensin?'), findsOneWidget);
      expect(find.text('Yalnız bu ders'), findsOneWidget);
      expect(find.text('Bu ve sonrakiler'), findsOneWidget);
      expect(find.text('Tüm seri'), findsOneWidget);
      await tester.tap(find.text('Vazgeç'));
    });
  });

  testWidgets('picking "Tüm seri" returns all', (tester) async {
    final picked = await _open(tester, () async {
      await tester.tap(find.text('Tüm seri'));
      await tester.pump();
      await tester.tap(find.text('Devam et'));
    });

    expect(picked, OccurrenceScope.all);
  });

  testWidgets('cancel returns null', (tester) async {
    final picked = await _open(tester, () => tester.tap(find.text('Vazgeç')));

    expect(picked, isNull);
  });
}
