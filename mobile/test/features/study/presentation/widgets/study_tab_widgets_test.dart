import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/study_tab_widgets.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Widget _wrap(Widget child) => MaterialApp(home: Scaffold(body: child));

void main() {
  testWidgets('StudyDemoBadge "Demo" metnini çizer', (tester) async {
    await tester.pumpWidget(_wrap(const StudyDemoBadge()));
    expect(find.text('Demo'), findsOneWidget);
  });

  testWidgets('StudyOwnershipBadge kendi/öğretmen etiketini ayırır', (tester) async {
    await tester.pumpWidget(_wrap(const Column(
      children: [StudyOwnershipBadge(isOwn: true), StudyOwnershipBadge(isOwn: false)],
    )));
    expect(find.text('Kendi'), findsOneWidget);
    expect(find.text('Öğretmen'), findsOneWidget);
  });

  testWidgets('StudyQuickAccessCard etiket çizer ve tıklanır', (tester) async {
    var tapped = false;
    await tester.pumpWidget(_wrap(StudyQuickAccessCard(
      icon: Icons.menu_book_rounded,
      color: AppColors.primary,
      label: 'Derslerim',
      onTap: () => tapped = true,
    )));
    expect(find.text('Derslerim'), findsOneWidget);
    await tester.tap(find.text('Derslerim'));
    expect(tapped, isTrue);
  });

  testWidgets('StudyProgressBar value 0..1 sınırına kırpar', (tester) async {
    await tester.pumpWidget(_wrap(const StudyProgressBar(value: 1.4)));
    final bar = tester.widget<FractionallySizedBox>(find.byType(FractionallySizedBox));
    expect(bar.widthFactor, 1.0);
  });
}
