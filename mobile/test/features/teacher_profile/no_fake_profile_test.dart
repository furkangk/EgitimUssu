import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/presentation/pages/teacher_profile_page.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_auth_repository.dart';

void main() {
  tearDown(injector.reset);
  testWidgets(
    'profil yükleme hatasında sahte profil ya da düzenleme görünmez',
    (tester) async {
      injector.registerSingleton<TeacherRepository>(_Teachers());
      final auth = AuthCubit(
        FakeAuthRepository(session: FakeAuthRepository.defaultSession()),
      );
      await auth.restoreSession();
      await tester.pumpWidget(
        BlocProvider<AuthCubit>.value(
          value: auth,
          child: const MaterialApp(
            locale: Locale('tr'),
            localizationsDelegates: AppLocalizations.localizationsDelegates,
            supportedLocales: AppLocalizations.supportedLocales,
            home: TeacherProfilePage(),
          ),
        ),
      );
      await tester.pump();
      await tester.pump(const Duration(seconds: 1));
      expect(find.text('Ahmet Yılmaz'), findsNothing);
      expect(find.text('4.8'), findsNothing);
      expect(find.text('126 yorum'), findsNothing);
      expect(find.text('Kişisel bilgiler'), findsNothing);
      expect(find.text('Profil alınamadı'), findsWidgets);
      await tester.pumpWidget(const SizedBox());
      await auth.close();
    },
  );
}

class _Teachers implements TeacherRepository {
  @override
  Future<TeacherProfile> getProfile(String userId) async =>
      throw const ApiException(message: 'Profil alınamadı');
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}
