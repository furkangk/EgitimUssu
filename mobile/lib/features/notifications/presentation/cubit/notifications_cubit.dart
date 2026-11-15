import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/notifications/domain/notification_contracts.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class NotificationsCubit extends Cubit<NotificationsState> {
  NotificationsCubit(this._repository) : super(const NotificationsState());

  final NotificationRepository _repository;

  int _generation = 0;
  void reset() {
    _generation++;
    if (!isClosed) emit(const NotificationsState());
  }

  bool _current(int generation) => !isClosed && generation == _generation;

  factory NotificationsCubit.create() =>
      NotificationsCubit(injector<NotificationRepository>());

  Future<void> load(String teacherUserId) async {
    final generation = ++_generation;
    emit(state.copyWith(status: NotificationsStatus.loading, clearError: true));
    try {
      final reminders = await _repository.listReminders(
        teacherUserId: teacherUserId,
      );
      if (!_current(generation)) return;
      final unread = await _readUnreadCount(teacherUserId);
      if (!_current(generation)) return;
      emit(
        state.copyWith(
          status: NotificationsStatus.loaded,
          reminders: reminders,
          unreadCount: unread,
        ),
      );
    } on ApiException catch (e) {
      if (!_current(generation)) return;
      emit(
        state.copyWith(
          status: NotificationsStatus.error,
          errorMessage: e.message,
        ),
      );
    } catch (_) {
      if (!_current(generation)) return;
      emit(
        state.copyWith(
          status: NotificationsStatus.error,
          errorMessage: 'Bildirimler yüklenemedi.',
        ),
      );
    }
  }

  Future<void> refresh(String teacherUserId) => load(teacherUserId);

  /// M11-4: Bildirim merkezinde okundu işaretlendiğinde rozet anında güncellensin diye
  /// dışarıdan set edilebilir (yeniden istek atmadan).
  void setUnreadCount(int count) {
    emit(state.copyWith(unreadCount: count < 0 ? 0 : count));
  }

  /// Rozet sayısı liste yüklemesini bloke etmemeli: hata olursa 0 kabul edilir.
  Future<int> _readUnreadCount(String userId) async {
    try {
      return await _repository.unreadNotificationCount(userId);
    } catch (_) {
      return 0;
    }
  }
}
