import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_state.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class PaymentsCubit extends Cubit<PaymentsState> {
  PaymentsCubit(this._repository) : super(const PaymentsState());

  final PaymentRepository _repository;
  static const int _pageSize = 20;
  String? _teacherUserId;

  factory PaymentsCubit.create() =>
      PaymentsCubit(injector<PaymentRepository>());

  /// İlk yükleme: özet (panel + grafikler) + filtreli ilk sayfa (paralel).
  Future<void> load(String teacherUserId) async {
    _teacherUserId = teacherUserId;
    if (isClosed) return;
    emit(state.copyWith(isLoading: true, clearMessages: true));
    try {
      final summaryFuture = _repository.getSummary(teacherUserId);
      final pageFuture = _repository.searchRecords(
        teacherUserId,
        filters: state.filters,
        skip: 0,
        take: _pageSize,
      );
      final summary = await summaryFuture;
      final page = await pageFuture;
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          summary: summary,
          records: page.items,
          totalCount: page.totalCount,
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    } catch (_) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          errorMessage: 'Ödemeler yüklenemedi. Lütfen tekrar deneyin.',
        ),
      );
    }
  }

  /// Filtre değişince ilk sayfayı yeniden çeker (özet değişmez — filtresizdir).
  Future<void> applyFilters(PaymentFilters filters) async {
    if (isClosed) return;
    emit(state.copyWith(filters: filters));
    await _reloadFirstPage();
  }

  Future<void> _reloadFirstPage() async {
    final teacherUserId = _teacherUserId;
    if (teacherUserId == null || isClosed) return;
    emit(state.copyWith(isLoading: true, clearMessages: true));
    try {
      final page = await _repository.searchRecords(
        teacherUserId,
        filters: state.filters,
        skip: 0,
        take: _pageSize,
      );
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          records: page.items,
          totalCount: page.totalCount,
          clearMessages: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    } catch (_) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoading: false,
          errorMessage: 'Ödemeler yüklenemedi. Lütfen tekrar deneyin.',
        ),
      );
    }
  }

  /// Sonraki sayfayı (sonsuz kaydırma) yükler ve listeye ekler.
  Future<void> loadMore() async {
    final teacherUserId = _teacherUserId;
    if (teacherUserId == null || isClosed) return;
    if (state.isLoadingMore || state.isLoading || !state.hasMore) return;
    emit(state.copyWith(isLoadingMore: true));
    try {
      final page = await _repository.searchRecords(
        teacherUserId,
        filters: state.filters,
        skip: state.records.length,
        take: _pageSize,
      );
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoadingMore: false,
          records: <PaymentRecord>[...state.records, ...page.items],
          totalCount: page.totalCount,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoadingMore: false, errorMessage: error.message));
    } catch (_) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isLoadingMore: false,
          errorMessage: 'Daha fazla kayıt yüklenemedi.',
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

  /// Var olan bir ödeme kaydını günceller.
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

  /// Bir ödeme kaydını **iptal** eder (silmez): `Status=Cancelled`.
  Future<void> cancel(PaymentRecord record) async {
    if (isClosed) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final updated = await _repository.updateRecord(
        _copyWith(record, status: 'Cancelled', collected: record.collectedAmount, outstanding: 0),
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
      await _refreshSummary();
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

  /// Bir kayda [amountNow] kadar tahsilat işler (tam/kısmi).
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

    emit(
      state.copyWith(
        isSaving: true,
        savingRecordId: record.id,
        clearMessages: true,
      ),
    );
    try {
      final updated = await _repository.updateRecord(
        _copyWith(
          record,
          status: status,
          collected: newCollected,
          outstanding: outstanding,
          collectedOnUtc: DateTime.now().toUtc(),
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
      await _refreshSummary();
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          clearSavingRecordId: true,
          errorMessage: error.message,
        ),
      );
    } catch (_) {
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

  /// Aggregate'leri (özet paneli + grafikler) yeniden çeker; hata yut.
  Future<void> _refreshSummary() async {
    final teacherUserId = _teacherUserId;
    if (teacherUserId == null || isClosed) return;
    try {
      final summary = await _repository.getSummary(teacherUserId);
      if (isClosed) return;
      emit(state.copyWith(summary: summary));
    } catch (_) {
      // Özet güncellenemezse sessizce geç (liste ana içerik).
    }
  }

  static PaymentRecord _copyWith(
    PaymentRecord record, {
    required String status,
    required double collected,
    required double outstanding,
    DateTime? collectedOnUtc,
  }) {
    return PaymentRecord(
      id: record.id,
      teacherUserId: record.teacherUserId,
      studentId: record.studentId,
      description: record.description,
      currency: record.currency,
      expectedAmount: record.expectedAmount,
      collectedAmount: collected,
      outstandingAmount: outstanding,
      status: status,
      relatedLessonSessionId: record.relatedLessonSessionId,
      dueDateUtc: record.dueDateUtc,
      collectedOnUtc: collectedOnUtc ?? record.collectedOnUtc,
      notes: record.notes,
      isOverdue: false,
      itemType: record.itemType,
    );
  }

  static String _formatMoney(double amount) {
    return amount == amount.roundToDouble()
        ? amount.toStringAsFixed(0)
        : amount.toStringAsFixed(2);
  }
}
