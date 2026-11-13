import 'package:egitim_ussu_mobile/core/theme/app_theme.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/login_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/register_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/role_selection_page.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/pages/welcome_page.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../helpers/fake_auth_repository.dart';

void main() {
  for (final scale in <double>[1, 2]) {
    for (final entry in <String, Widget>{
      'welcome': const WelcomePage(),
      'roles': const RoleSelectionPage(),
      'login': const LoginPage(),
      'register': const RegisterPage(),
    }.entries) {
      testWidgets('${entry.key} fits a narrow screen at text scale $scale', (
        tester,
      ) async {
        await _pump(tester, entry.value, scale: scale);
        // Framework reports any overflow with its originating widget.
        final scroll = find.byType(SingleChildScrollView).first;
        await tester.drag(scroll, const Offset(0, -1600));
        await tester.pumpAndSettle();
        // Framework reports any overflow with its originating widget.
      });
    }
  }

  testWidgets('login submit remains reachable with the keyboard open', (
    tester,
  ) async {
    await _pump(tester, const LoginPage(), keyboardHeight: 280);
    final submit = find.widgetWithText(FilledButton, 'Giriş Yap');
    await tester.ensureVisible(submit);
    await tester.pumpAndSettle();
    expect(submit.hitTestable(), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('login fields do not contain demo credentials', (tester) async {
    await _pump(tester, const LoginPage());
    for (final field in tester.widgetList<TextFormField>(
      find.byType(TextFormField),
    )) {
      expect(field.controller!.text, isEmpty);
    }
  });

  testWidgets('registration fields do not contain demo personal data', (
    tester,
  ) async {
    await _pump(tester, const RegisterPage());
    for (final field in tester.widgetList<TextFormField>(
      find.byType(TextFormField),
    )) {
      expect(field.controller!.text, isEmpty);
    }
  });

  testWidgets('login preserves spaces in the submitted password', (
    tester,
  ) async {
    final repository = _CapturingAuthRepository();
    await _pump(tester, const LoginPage(), repository: repository);
    final fields = find.byType(TextFormField);
    await tester.enterText(fields.at(0), 'audit@example.com');
    await tester.enterText(fields.at(1), ' Passw0rd! ');
    final submit = find.widgetWithText(FilledButton, 'Giriş Yap');
    await tester.ensureVisible(submit);
    await tester.tap(submit);
    await tester.pumpAndSettle();
    expect(repository.password, ' Passw0rd! ');
  });
}

Future<void> _pump(
  WidgetTester tester,
  Widget page, {
  double scale = 1,
  double keyboardHeight = 0,
  FakeAuthRepository? repository,
}) async {
  tester.view.physicalSize = const Size(320, 568);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final auth = AuthCubit(repository ?? FakeAuthRepository());
  addTearDown(auth.close);
  await tester.pumpWidget(
    BlocProvider.value(
      value: auth,
      child: MaterialApp(
        theme: AppTheme.lightTheme,
        locale: const Locale('tr'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(context).copyWith(
            textScaler: TextScaler.linear(scale),
            viewInsets: EdgeInsets.only(bottom: keyboardHeight),
          ),
          child: child!,
        ),
        home: page,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

class _CapturingAuthRepository extends FakeAuthRepository {
  String? password;

  @override
  Future<UserSession> login({
    required String email,
    required String password,
    int roleId = 2,
  }) {
    this.password = password;
    return super.login(email: email, password: password, roleId: roleId);
  }
}
