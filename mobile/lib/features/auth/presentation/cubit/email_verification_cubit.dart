import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

enum EmailVerificationStatus { idle, resending, resent, verifying, verified, failure }

class EmailVerificationState extends Equatable {
  const EmailVerificationState({
    this.status = EmailVerificationStatus.idle,
    this.message,
  });

  final EmailVerificationStatus status;
  final String? message;

  EmailVerificationState copyWith({
    EmailVerificationStatus? status,
    String? message,
  }) => EmailVerificationState(status: status ?? this.status, message: message);

  @override
  List<Object?> get props => <Object?>[status, message];
}

/// D-02: E-posta doğrulama ekranının durum yönetimi.
class EmailVerificationCubit extends Cubit<EmailVerificationState> {
  EmailVerificationCubit(this._repository)
    : super(const EmailVerificationState());

  final AuthRepository _repository;

  factory EmailVerificationCubit.create() =>
      EmailVerificationCubit(injector<AuthRepository>());

  Future<void> resendCode(String email) async {
    emit(state.copyWith(status: EmailVerificationStatus.resending));
    try {
      await _repository.requestEmailVerification(email: email);
      emit(state.copyWith(status: EmailVerificationStatus.resent));
    } on ApiException catch (error) {
      emit(
        state.copyWith(
          status: EmailVerificationStatus.failure,
          message: error.message,
        ),
      );
    }
  }

  Future<void> verify({required String email, required String token}) async {
    emit(state.copyWith(status: EmailVerificationStatus.verifying));
    try {
      await _repository.confirmEmailVerification(email: email, token: token);
      emit(state.copyWith(status: EmailVerificationStatus.verified));
    } on ApiException catch (error) {
      emit(
        state.copyWith(
          status: EmailVerificationStatus.failure,
          message: error.message,
        ),
      );
    }
  }
}
