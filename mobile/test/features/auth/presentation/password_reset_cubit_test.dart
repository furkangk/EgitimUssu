import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/password_reset_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../../helpers/fake_auth_repository.dart';

void main() {
  group('PasswordResetCubit', () {
    test('istek basarili olunca sending -> sent gecisi yapar', () async {
      final repository = FakeAuthRepository();
      final cubit = PasswordResetCubit(repository);

      expect(cubit.state.status, PasswordResetStatus.idle);

      final states = expectLater(
        cubit.stream,
        emitsInOrder(<Matcher>[
          isA<PasswordResetState>().having(
            (s) => s.status,
            'status',
            PasswordResetStatus.sending,
          ),
          isA<PasswordResetState>().having(
            (s) => s.status,
            'status',
            PasswordResetStatus.sent,
          ),
        ]),
      );

      await cubit.requestReset('ogretmen@example.com');
      await states;

      expect(repository.requestPasswordResetCallCount, 1);
      expect(repository.lastPasswordResetEmail, 'ogretmen@example.com');
    });

    test('onay basarili olunca confirming -> done gecisi yapar', () async {
      final repository = FakeAuthRepository();
      final cubit = PasswordResetCubit(repository);

      final states = expectLater(
        cubit.stream,
        emitsInOrder(<Matcher>[
          isA<PasswordResetState>().having(
            (s) => s.status,
            'status',
            PasswordResetStatus.confirming,
          ),
          isA<PasswordResetState>().having(
            (s) => s.status,
            'status',
            PasswordResetStatus.done,
          ),
        ]),
      );

      await cubit.confirmReset(
        email: 'ogretmen@example.com',
        token: 'TOKEN123',
        newPassword: 'YeniSifre123!',
      );
      await states;

      expect(repository.confirmPasswordResetCallCount, 1);
      expect(repository.lastPasswordResetToken, 'TOKEN123');
    });

    test('API hatasinda failure durumuna ve mesaja gecer', () async {
      final repository = FakeAuthRepository(
        passwordResetError: const ApiException(
          message: 'Sifre sifirlama tokeni gecersiz.',
          statusCode: 400,
        ),
      );
      final cubit = PasswordResetCubit(repository);

      await cubit.confirmReset(
        email: 'ogretmen@example.com',
        token: 'BOZUK',
        newPassword: 'YeniSifre123!',
      );

      expect(cubit.state.status, PasswordResetStatus.failure);
      expect(cubit.state.message, 'Sifre sifirlama tokeni gecersiz.');
    });
  });
}
