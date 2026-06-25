import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class PaymentsCubit extends Cubit<PaymentsState> {
  PaymentsCubit(this._repository) : super(const PaymentsState());

  final PaymentRepository _repository;

  factory PaymentsCubit.create() =>
      PaymentsCubit(injector<PaymentRepository>());

  Future<void> load(String teacherUserId) async {
    if (isClosed) return;
    emit(state.copyWith(isLoading: true, clearMessages: true));
    try {
      final records = await _repository.listTeacherRecords(teacherUserId);
      if (isClosed) return;
      final summary = await _repository.getSummary(teacherUserId);
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          records: records,
          summary: summary,
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    }
  }

  Future<void> create(PaymentRecord record) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final created = await _repository.createRecord(record);
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          records: <PaymentRecord>[created, ...state.records],
          successMessage: 'Ödeme kaydı oluşturuldu.',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    }
  }

  Future<void> markPaid(PaymentRecord record) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final updated = await _repository.updateRecord(
        PaymentRecord(
          id: record.id,
          teacherUserId: record.teacherUserId,
          studentId: record.studentId,
          description: record.description,
          currency: record.currency,
          expectedAmount: record.expectedAmount,
          collectedAmount: record.expectedAmount,
          outstandingAmount: 0,
          status: 'Paid',
          relatedLessonSessionId: record.relatedLessonSessionId,
          dueDateUtc: record.dueDateUtc,
          collectedOnUtc: DateTime.now().toUtc(),
          notes: record.notes,
          isOverdue: false,
          itemType: record.itemType,
        ),
      );
      if (isClosed) return;
      final records = state.records
          .map((item) => item.id == updated.id ? updated : item)
          .toList();
      emit(
        state.copyWith(
          isSaving: false,
          records: records,
          successMessage: 'Ödeme tahsil edildi olarak işaretlendi.',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    }
  }
}
