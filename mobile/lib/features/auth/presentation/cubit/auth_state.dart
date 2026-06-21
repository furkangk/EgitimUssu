import 'package:egitim_ussu_mobile/features/auth/domain/entities/user_session.dart';
import 'package:equatable/equatable.dart';

enum AuthStatus { initial, loading, authenticated, unauthenticated }

class AuthState extends Equatable {
  const AuthState({
    this.status = AuthStatus.initial,
    this.session,
    this.errorMessage,
  });

  final AuthStatus status;
  final UserSession? session;
  final String? errorMessage;

  AuthState copyWith({
    AuthStatus? status,
    UserSession? session,
    String? errorMessage,
    bool clearError = false,
  }) {
    return AuthState(
      status: status ?? this.status,
      session: session ?? this.session,
      errorMessage: clearError ? null : errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props => <Object?>[status, session, errorMessage];
}
