import 'dart:async';
import 'dart:ui';

import 'package:egitim_ussu_mobile/core/bloc/safe_emit.dart';
import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/push/push_service.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class AuthCubit extends Cubit<AuthState> with SafeEmit<AuthState> {
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
  int _sessionGeneration = 0;
  bool _current(int generation) =>
      !isClosed && generation == _sessionGeneration;

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
    final generation = _sessionGeneration;
    safeEmit(state.copyWith(status: AuthStatus.loading, clearError: true));
    try {
      final session = await _repository.login(
        email: email,
        password: password,
        roleId: roleId,
      );
      if (!_current(generation)) return;
      safeEmit(
        state.copyWith(
          status: AuthStatus.authenticated,
          session: session,
          clearError: true,
        ),
      );
      await _registerForPush(session.userId);
    } on ApiException catch (error) {
      if (!_current(generation)) return;
      safeEmit(
        AuthState(
          status: AuthStatus.unauthenticated,
          errorMessage: error.message,
        ),
      );
    } catch (error) {
      if (!_current(generation)) return;
      _authenticationFailed(error);
    }
  }

  Future<void> logout() async {
    _sessionGeneration++;
    await _pushService?.unregister();
    await _repository.logout();
    safeEmit(const AuthState(status: AuthStatus.unauthenticated));
  }

  Future<void> expireSession() async {
    _sessionGeneration++;
    await _pushService?.unregister();
    await _repository.logout();
    if (!isClosed) {
      safeEmit(
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
    final generation = _sessionGeneration;
    safeEmit(state.copyWith(status: AuthStatus.loading, clearError: true));
    try {
      final session = await _repository.register(
        email: email,
        password: password,
        firstName: firstName,
        lastName: lastName,
        phoneNumber: phoneNumber,
        roleId: roleId,
      );
      if (!_current(generation)) return;
      safeEmit(
        state.copyWith(
          status: AuthStatus.authenticated,
          session: session,
          clearError: true,
        ),
      );
      await _registerForPush(session.userId);
    } on ApiException catch (error) {
      if (!_current(generation)) return;
      safeEmit(
        AuthState(
          status: AuthStatus.unauthenticated,
          errorMessage: error.message,
        ),
      );
    } catch (error) {
      if (!_current(generation)) return;
      _authenticationFailed(error);
    }
  }

  Future<void> restoreSession() async {
    final generation = _sessionGeneration;
    safeEmit(state.copyWith(status: AuthStatus.loading, clearError: true));
    try {
      final session = await _repository.restoreSession();
      if (!_current(generation)) return;
      if (session == null) {
        safeEmit(const AuthState(status: AuthStatus.unauthenticated));
        return;
      }
      safeEmit(
        state.copyWith(
          status: AuthStatus.authenticated,
          session: session,
          clearError: true,
        ),
      );
      await _registerForPush(session.userId);
    } catch (error) {
      if (!_current(generation)) return;
      _authenticationFailed(error);
    }
  }

  void _authenticationFailed(Object error) {
    // Exception messages can include credentials, tokens or server payloads.
    debugPrint('Authentication failed: ${error.runtimeType}');
    safeEmit(
      AuthState(
        status: AuthStatus.unauthenticated,
        errorMessage: lookupAppLocalizations(
          const Locale('tr'),
        ).api_unexpectedError,
      ),
    );
  }

  /// Push kaydı kimlik akışını bloke etmez: hata olsa da oturum açık kalır (PushService log basar).
  Future<void> _registerForPush(String userId) async {
    try {
      await _pushService?.registerForUser(userId);
    } catch (error) {
      debugPrint('Push registration failed: ${error.runtimeType}');
    }
  }

  @override
  Future<void> close() async {
    await _unauthorizedSubscription?.cancel();
    return super.close();
  }
}
