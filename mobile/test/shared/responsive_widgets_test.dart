import 'package:egitim_ussu_mobile/core/theme/app_theme.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_bottom_nav.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

void main() {
  testWidgets('all teacher navigation targets fit and remain tappable', (
    tester,
  ) async {
    final router = GoRouter(
      initialLocation: '/dashboard',
      routes: AppNavTab.values.where((tab) => tab != AppNavTab.none).map((tab) {
        return GoRoute(
          path: tab.route,
          builder: (context, state) => Scaffold(
            body: Text('Current: ${tab.name}'),
            bottomNavigationBar: AppBottomNav(current: tab),
          ),
        );
      }).toList(),
    );
    addTearDown(router.dispose);
    tester.view.physicalSize = const Size(320, 568);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(
      MaterialApp.router(
        routerConfig: router,
        theme: AppTheme.lightTheme,
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(
            context,
          ).copyWith(textScaler: const TextScaler.linear(2)),
          child: child!,
        ),
      ),
    );
    await tester.pumpAndSettle();
    for (final tab in AppNavTab.values.where((tab) => tab != AppNavTab.none)) {
      final label = find.text(tab.label);
      expect(label.hitTestable(), findsOneWidget);
      await tester.tap(label);
      await tester.pumpAndSettle();
      expect(find.text('Current: ${tab.name}'), findsOneWidget);
      expect(tester.takeException(), isNull);
    }
  });

  testWidgets('dropdown keeps long options inside a narrow form', (
    tester,
  ) async {
    await _pump(
      tester,
      AppDropdownField<String>(
        value: 'long',
        labelText: 'Ders',
        items: const <DropdownMenuItem<String>>[
          DropdownMenuItem(
            value: 'long',
            child: Text('Matematik ve Geometri Hazırlık Çalışması'),
          ),
        ],
        onChanged: (_) {},
      ),
    );
    expect(tester.takeException(), isNull);
    await tester.tap(find.byType(DropdownButton<String>));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });

  testWidgets('retry stays reachable without squeezing the error message', (
    tester,
  ) async {
    var retryCount = 0;
    await _pump(
      tester,
      ErrorStateView(
        message: 'Veriler yüklenemedi. İnternet bağlantınızı kontrol edin.',
        onRetry: () => retryCount++,
      ),
    );
    expect(tester.takeException(), isNull);
    await tester.tap(find.text('Tekrar dene'));
    expect(retryCount, 1);
  });

  testWidgets('empty state remains readable in a short viewport', (
    tester,
  ) async {
    await _pump(
      tester,
      const SizedBox(
        height: 180,
        child: EmptyStateView(
          title: 'Henüz kayıtlı öğrenci bulunmuyor',
          subtitle: 'Öğrencilerinizi ekledikten sonra derslerini takip edin.',
        ),
      ),
    );
    expect(tester.takeException(), isNull);
  });
}

Future<void> _pump(WidgetTester tester, Widget child) async {
  tester.view.physicalSize = const Size(320, 568);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    MaterialApp(
      theme: AppTheme.lightTheme,
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(
          context,
        ).copyWith(textScaler: const TextScaler.linear(2)),
        child: child!,
      ),
      home: Scaffold(
        body: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: child,
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}
