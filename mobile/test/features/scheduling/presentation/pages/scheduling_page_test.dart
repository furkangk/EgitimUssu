import 'package:egitim_ussu_mobile/features/scheduling/presentation/pages/scheduling_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('renders calendar views without add menu chrome', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: SchedulingPage()));

    expect(find.text('Takvim'), findsWidgets);
    expect(find.text('Aylik'), findsOneWidget);
    expect(find.text('Haftalik'), findsOneWidget);
    expect(find.text('Gunluk'), findsOneWidget);
    expect(find.text('Etkinlik ekle'), findsNothing);
    expect(find.text('Ders Ekle'), findsNothing);
  });
}
