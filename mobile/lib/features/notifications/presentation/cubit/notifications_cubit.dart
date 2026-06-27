import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class NotificationsCubit extends Cubit<NotificationsState> {
  NotificationsCubit(this._repository) : super(const NotificationsState());

  final NotificationRepository _repository;

  factory NotificationsCubit.create() =>
      NotificationsCubit(injector<NotificationRepository>());

  Future<void> load(String teacherUserId) async {
    emit(state.copyWith(status: NotificationsStatus.loading, clearError: true));
    try {
      final reminders = await _repository.listReminders(teacherUserId: teacherUserId);
      emit(state.copyWith(status: NotificationsStatus.loaded, reminders: reminders));
    } on ApiException catch (e) {
      emit(state.copyWith(status: NotificationsStatus.error, errorMessage: e.message));
    } catch (_) {
      emit(state.copyWith(status: NotificationsStatus.error, errorMessage: 'Bildirimler yüklenemedi.'));
    }
  }

  Future<void> refresh(String teacherUserId) => load(teacherUserId);
}
