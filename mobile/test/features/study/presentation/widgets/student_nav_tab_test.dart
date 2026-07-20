import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('StudentNavTab 5 görünür sekme; sıra ve rotalar IA ile uyumlu', () {
    const expected = <(StudentNavTab, String, String)>[
      (StudentNavTab.work, 'Çalış', '/student-home'),
      (StudentNavTab.performance, 'Performans', '/student/performance'),
      (StudentNavTab.lessons, 'Derslerim', '/student/lessons'),
      (StudentNavTab.discover, 'Keşfet', '/student/discover'),
      (StudentNavTab.profile, 'Profil', '/student/profile'),
    ];
    for (final (tab, label, route) in expected) {
      expect(tab.label, label);
      expect(tab.route, route);
    }
    // Eski üyeler kaldırıldı: home/studies/tests/calendar/more artık yok.
    expect(StudentNavTab.values.map((t) => t.name), isNot(contains('studies')));
    expect(StudentNavTab.values.map((t) => t.name), isNot(contains('more')));
  });

  testWidgets('StudentBottomNav 5 sekme etiketini çizer', (tester) async {
    await tester.pumpWidget(const MaterialApp(
      home: Scaffold(
        bottomNavigationBar: StudentBottomNav(current: StudentNavTab.work),
      ),
    ));
    for (final label in ['Çalış', 'Performans', 'Derslerim', 'Keşfet', 'Profil']) {
      expect(find.text(label), findsOneWidget);
    }
  });
}
