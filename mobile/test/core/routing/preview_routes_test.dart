import 'package:egitim_ussu_mobile/core/routing/app_router.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import '../../helpers/fake_auth_repository.dart';

void main() {
  for (final enabled in <bool>[false, true]) {
    test('preview kayıtları debug tercihi $enabled kapısına uyar', () async {
      final auth = AuthCubit(FakeAuthRepository());
      final app = AppRouter(authCubit: auth, enablePreviewRoutes: enabled);
      final paths = app.router.configuration.routes.whereType<GoRoute>().map(
        (r) => r.path,
      );
      expect(paths.contains('/teacher-panel-preview'), enabled);
      expect(paths.contains('/account-info-preview'), enabled);
      expect(paths.contains('/account-info'), enabled);
      app.dispose();
      await auth.close();
    });
  }
}
