import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/pages/student_discover_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('Keşfet yer tutucu: başlık + Faz 4 boş durumu + devre dışı çipler',
      (tester) async {
    await tester.pumpWidget(
      BlocProvider<NotificationsCubit>(
        create: (_) => NotificationsCubit(_FakeNotificationRepository()),
        child: const MaterialApp(home: StudentDiscoverPage()),
      ),
    );
    await tester.pump();

    // 'Keşfet' hem AppPageHeader başlığında hem StudentBottomNav sekme
    // etiketinde görünür (nav her zaman render edilir) → findsWidgets.
    expect(find.text('Keşfet'), findsWidgets);
    expect(find.textContaining('Faz 4'), findsWidgets);
    // Devre dışı filtre çiplerinden en az biri.
    expect(find.text('Branş'), findsOneWidget);
  });
}

class _FakeNotificationRepository implements NotificationRepository {
  @override
  Future<List<LessonReminder>> listReminders({
    required String teacherUserId,
    bool activeOnly = false,
  }) async {
    return const <LessonReminder>[];
  }
}
