import 'dart:async';

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/push/push_service.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class AuthCubit extends Cubit<AuthState> {
  AuthCubit(
    this._repository, {
    Stream<void>? unauthorizedEvents,
    PushService? pushService,
  }) : _pushService = pushService,
       super(const AuthState()) {
    _unauthorizedSubscription = unauthorizedEvents?.listen((_) {
      expireSession();
    });
  }

  final AuthRepository _repository;

  /// A-04: oturum açıldığında cihaz push token'ı kaydedilir, kapandığında silinir.
  /// Testlerde `null` verilebilir — push, kimlik akışının önkoşulu değildir.
  final PushService? _pushService;

  StreamSubscription<void>? _unauthorizedSubscription;

  factory AuthCubit.create() => AuthCubit(
    injector<AuthRepository>(),
    unauthorizedEvents: injector<ApiClient>().unauthorizedEvents,
    pushService: injector<PushService>(),
  );

  Future<void> login({
    required String email,
    required String password,
    int roleId = 2,
  }) async {
    emit(state.copyWith(status: AuthStatus.loading, clearError: true));
    try {
      final session = await _repository.login(
        email: email,
        password: password,
        roleId: roleId,
      );
      emit(
        state.copyWith(
          status: AuthStatus.authenticated,
          session: session,
          clearError: true,
        ),
      );
      await _registerForPush(session.userId);
    } on ApiException catch (error) {
      emit(
        state.copyWith(
          status: AuthStatus.unauthenticated,
          errorMessage: error.message,
        ),
      );
    }
  }

  Future<void> logout() async {
    await _pushService?.unregister();
    await _repository.logout();
    emit(const AuthState(status: AuthStatus.unauthenticated));
  }

  Future<void> expireSession() async {
    await _pushService?.unregister();
    await _repository.logout();
    if (!isClosed) {
      emit(
        const AuthState(
          status: AuthStatus.unauthenticated,
          errorMessage: 'Oturumun süresi doldu. Lütfen tekrar giriş yap.',
        ),
      );
    }
  }

  Future<void> register({
    required String email,
    required String password,
    required String firstName,
    required String lastName,
    String? phoneNumber,
    int roleId = 2,
  }) async {
    emit(state.copyWith(status: AuthStatus.loading, clearError: true));
    try {
      final session = await _repository.register(
        email: email,
        password: password,
        firstName: firstName,
        lastName: lastName,
        phoneNumber: phoneNumber,
        roleId: roleId,
      );
      emit(
        state.copyWith(
          status: AuthStatus.authenticated,
          session: session,
          clearError: true,
        ),
      );
      await _registerForPush(session.userId);
    } on ApiException catch (error) {
      emit(
        state.copyWith(
          status: AuthStatus.unauthenticated,
          errorMessage: error.message,
        ),
      );
    }
  }

  Future<void> restoreSession() async {
    emit(state.copyWith(status: AuthStatus.loading, clearError: true));
    final session = await _repository.restoreSession();
    if (session == null) {
      emit(const AuthState(status: AuthStatus.unauthenticated));
      return;
    }

    emit(
      state.copyWith(
        status: AuthStatus.authenticated,
        session: session,
        clearError: true,
      ),
    );
    // Token rotasyona uğrayabildiği için oturum geri yüklendiğinde de tazelenir.
    await _registerForPush(session.userId);
  }

  /// Push kaydı kimlik akışını bloke etmez: hata olsa da oturum açık kalır (PushService log basar).
  Future<void> _registerForPush(String userId) async {
    await _pushService?.registerForUser(userId);
  }

  @override
  Future<void> close() async {
    await _unauthorizedSubscription?.cancel();
    return super.close();
  }
}
