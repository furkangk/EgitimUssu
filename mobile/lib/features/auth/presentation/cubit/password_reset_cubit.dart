import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/auth/domain/repositories/auth_repository.dart';
import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

enum PasswordResetStatus { idle, sending, sent, confirming, done, failure }

class PasswordResetState extends Equatable {
  const PasswordResetState({this.status = PasswordResetStatus.idle, this.message});

  final PasswordResetStatus status;
  final String? message;

  PasswordResetState copyWith({PasswordResetStatus? status, String? message}) =>
      PasswordResetState(status: status ?? this.status, message: message);

  @override
  List<Object?> get props => <Object?>[status, message];
}

/// D-01: "Şifremi unuttum" akışının durum yönetimi.
/// İstek ucu kullanıcı bulunmasa da 200 döner (e-posta enumeration engeli),
/// bu yüzden `sent` durumu "kod gönderildi" değil "istek alındı" anlamına gelir.
class PasswordResetCubit extends Cubit<PasswordResetState> {
  PasswordResetCubit(this._repository) : super(const PasswordResetState());

  final AuthRepository _repository;

  factory PasswordResetCubit.create() =>
      PasswordResetCubit(injector<AuthRepository>());

  Future<void> requestReset(String email) async {
    emit(state.copyWith(status: PasswordResetStatus.sending));
    try {
      await _repository.requestPasswordReset(email: email);
      emit(state.copyWith(status: PasswordResetStatus.sent));
    } on ApiException catch (error) {
      emit(state.copyWith(status: PasswordResetStatus.failure, message: error.message));
    }
  }

  Future<void> confirmReset({
    required String email,
    required String token,
    required String newPassword,
  }) async {
    emit(state.copyWith(status: PasswordResetStatus.confirming));
    try {
      await _repository.confirmPasswordReset(
        email: email,
        token: token,
        newPassword: newPassword,
      );
      emit(state.copyWith(status: PasswordResetStatus.done));
    } on ApiException catch (error) {
      emit(state.copyWith(status: PasswordResetStatus.failure, message: error.message));
    }
  }
}
