import 'package:egitim_ussu_mobile/app/app.dart';
import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

import 'helpers/fake_auth_repository.dart';
import 'helpers/fake_membership_repository.dart';
import 'helpers/fake_notification_repository.dart';

void main() {
  testWidgets('app shows welcome screen while auth restores', (tester) async {
    final authCubit = AuthCubit(FakeAuthRepository(hangOnRestore: true));
    final appRouter = AppRouter(authCubit: authCubit);
    final notificationsCubit = NotificationsCubit(
      FakeNotificationRepository(),
    );
    // D-10: Uygulama kokunde `MembershipCubit` var; testte DI'ya gitmemesi icin
    // sahte depoyla disaridan verilir.
    final membershipCubit = MembershipCubit(FakeMembershipRepository());

    await tester.pumpWidget(
      EgitimUssuApp(
        authCubit: authCubit,
        appRouter: appRouter,
        notificationsCubit: notificationsCubit,
        membershipCubit: membershipCubit,
      ),
    );

    expect(find.text('EgitimUssu'), findsOneWidget);
    expect(find.text('Giris Yap'), findsOneWidget);
    expect(find.text('Kayit Ol'), findsOneWidget);

    appRouter.dispose();
    await authCubit.close();
    await notificationsCubit.close();
    await membershipCubit.close();
  });
}


