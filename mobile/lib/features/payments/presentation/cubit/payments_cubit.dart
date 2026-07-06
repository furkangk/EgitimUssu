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
      emit(
        state.copyWith(
          isLoading: false,
          records: records,
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    } catch (_) {
      // Beklenmeyen hatada isLoading'i sıfırla; aksi halde ekran kalıcı olarak
      // shimmer'da (yükleniyor) asılı kalır.
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          errorMessage: 'Ödemeler yüklenemedi. Lütfen tekrar deneyin.',
        ),
      );
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
    } catch (_) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          errorMessage: 'Ödeme kaydı oluşturulamadı. Lütfen tekrar deneyin.',
        ),
      );
    }
  }

  /// Var olan bir ödeme kaydını (tutar, açıklama, vade, not vb.) günceller.
  Future<void> update(PaymentRecord record) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final updated = await _repository.updateRecord(record);
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          records: state.records
              .map((item) => item.id == updated.id ? updated : item)
              .toList(),
          successMessage: 'Ödeme kaydı güncellendi.',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    } catch (_) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          errorMessage: 'Ödeme kaydı güncellenemedi. Lütfen tekrar deneyin.',
        ),
      );
    }
  }

  /// Bir ödeme kaydını **iptal** eder (kalıcı silme değil): `Status=Cancelled`.
  /// Kayıt listede kalır ("İptal" olarak görünür); iptal edilen borç doğurmaz
  /// (backend `OutstandingAmount` = 0). Tahsil edilen tutar korunur.
  Future<void> cancel(PaymentRecord record) async {
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
          collectedAmount: record.collectedAmount,
          outstandingAmount: 0,
          status: 'Cancelled',
          relatedLessonSessionId: record.relatedLessonSessionId,
          dueDateUtc: record.dueDateUtc,
          collectedOnUtc: record.collectedOnUtc,
          notes: record.notes,
          isOverdue: false,
          itemType: record.itemType,
        ),
      );
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          records: state.records
              .map((item) => item.id == updated.id ? updated : item)
              .toList(),
          successMessage: 'Ödeme iptal edildi.',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isSaving: false, errorMessage: error.message));
    } catch (_) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          errorMessage: 'Ödeme iptal edilemedi. Lütfen tekrar deneyin.',
        ),
      );
    }
  }

  /// Bir kayda [amountNow] kadar tahsilat işler. Yeni tahsil edilen tutar
  /// (mevcut + [amountNow]) beklenen tutarı aşamaz; duruma göre `Paid` / `PartiallyPaid`
  /// olarak işaretlenir. `amountNow == kalan` verilirse ödemenin tamamı alınmış olur.
  Future<void> collect(PaymentRecord record, double amountNow) async {
    if (isClosed) return;
    final expected = record.expectedAmount;
    final newCollected = (record.collectedAmount + amountNow)
        .clamp(0, expected)
        .toDouble();
    final outstanding = (expected - newCollected)
        .clamp(0, double.infinity)
        .toDouble();
    final isFullyPaid = newCollected >= expected;
    final status = isFullyPaid
        ? 'Paid'
        : newCollected > 0
        ? 'PartiallyPaid'
        : 'Pending';

    emit(state.copyWith(
      isSaving: true,
      savingRecordId: record.id,
      clearMessages: true,
    ));
    try {
      final updated = await _repository.updateRecord(
        PaymentRecord(
          id: record.id,
          teacherUserId: record.teacherUserId,
          studentId: record.studentId,
          description: record.description,
          currency: record.currency,
          expectedAmount: expected,
          collectedAmount: newCollected,
          outstandingAmount: outstanding,
          status: status,
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
          clearSavingRecordId: true,
          records: records,
          successMessage: isFullyPaid
              ? 'Ödeme tahsil edildi olarak işaretlendi.'
              : 'Kısmi tahsilat kaydedildi. Kalan: ${_formatMoney(outstanding)} ${record.currency}',
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(
        isSaving: false,
        clearSavingRecordId: true,
        errorMessage: error.message,
      ));
    } catch (_) {
      // Beklenmeyen hatada da isSaving'i sıfırla; aksi halde "Tahsil Et" butonu
      // kalıcı olarak devre dışı kalır (kullanıcıya "hiçbir şey olmuyor" gibi görünür).
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          clearSavingRecordId: true,
          errorMessage: 'Ödeme güncellenemedi. Lütfen tekrar deneyin.',
        ),
      );
    }
  }

  static String _formatMoney(double amount) {
    // Küçük yardımcı: tam sayıysa ondalık gösterme.
    return amount == amount.roundToDouble()
        ? amount.toStringAsFixed(0)
        : amount.toStringAsFixed(2);
  }
}
