import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('StudentNavTab 4 görünür sekme; sıra ve rotalar 4-sekme IA ile uyumlu', () {
    const expected = <(StudentNavTab, String, String)>[
      (StudentNavTab.work, 'Çalışma', '/student-home'),
      (StudentNavTab.lessons, 'Derslerim', '/student/lessons'),
      (StudentNavTab.performance, 'Performans', '/student/performance'),
      (StudentNavTab.profile, 'Profil', '/student/profile'),
    ];
    for (final (tab, label, route) in expected) {
      expect(tab.label, label);
      expect(tab.route, route);
    }
    expect(StudentNavTab.values.map((t) => t.name), isNot(contains('discover')));
  });

  testWidgets('StudentBottomNav 4 sekme etiketi çizer, Keşfet yok', (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(
        bottomNavigationBar: StudentBottomNav(current: StudentNavTab.work),
      ),
    ));
    for (final label in ['Çalışma', 'Derslerim', 'Performans', 'Profil']) {
      expect(find.text(label), findsOneWidget);
    }
    expect(find.text('Keşfet'), findsNothing);
  });
}
