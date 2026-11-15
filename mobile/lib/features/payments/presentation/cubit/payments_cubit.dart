import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/presentation/cubit/payments_state.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class PaymentsCubit extends Cubit<PaymentsState> {
  PaymentsCubit(this._repository) : super(const PaymentsState());

  final PaymentRepository _repository;
  AppLocalizations _l10n = lookupAppLocalizations(const Locale('tr'));
  void setLocalizations(AppLocalizations l10n) {
    _l10n = l10n;
  }

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
          errorMessage: _l10n.finance_odemelerYuklenemediLutfenTekrarDeneyin,
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
          errorMessage: _l10n.finance_odemelerYuklenemediLutfenTekrarDeneyin,
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
          errorMessage: _l10n.finance_dahaFazlaKayitYuklenemedi,
        ),
      );
    }
  }

  Future<void> create(PaymentRecord record) async {
    if (isClosed || state.isSaving) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final created = await _repository.createRecord(record);
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          records: <PaymentRecord>[created, ...state.records],
          successMessage: _l10n.finance_odemeKaydiOlusturuldu,
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
          errorMessage:
              _l10n.finance_odemeKaydiOlusturulamadiLutfenTekrarDeneyin,
        ),
      );
    }
  }

  /// Var olan bir ödeme kaydını günceller.
  Future<void> update(PaymentRecord record) async {
    if (isClosed || state.isSaving) return;
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
          successMessage: _l10n.finance_odemeKaydiGuncellendi,
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
          errorMessage:
              _l10n.finance_odemeKaydiGuncellenemediLutfenTekrarDeneyin,
        ),
      );
    }
  }

  /// Bir ödeme kaydını **iptal** eder (silmez): `Status=Cancelled`.
  Future<void> cancel(PaymentRecord record) async {
    if (isClosed || state.isSaving) return;
    emit(state.copyWith(isSaving: true, clearMessages: true));
    try {
      final updated = await _repository.cancelRecord(record.id);
      if (isClosed) return;
      emit(
        state.copyWith(
          isSaving: false,
          records: state.records
              .map((item) => item.id == updated.id ? updated : item)
              .toList(),
          successMessage: _l10n.finance_odemeIptalEdildi,
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
          errorMessage: _l10n.finance_odemeIptalEdilemediLutfenTekrarDeneyin,
        ),
      );
    }
  }

  /// Bir kayda [amountNow] kadar tahsilat işler (tam/kısmi).
  Future<void> collect(PaymentRecord record, int amountNow) async {
    if (isClosed || state.isSaving) return;
    emit(
      state.copyWith(
        isSaving: true,
        savingRecordId: record.id,
        clearMessages: true,
      ),
    );
    try {
      final updated = await _repository.addCollection(
        record.id,
        amountNow,
        occurredOnUtc: DateTime.now().toUtc(),
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
          successMessage: _l10n.finance_tahsilatKaydedildi,
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
          errorMessage: _l10n.finance_odemeGuncellenemediLutfenTekrarDeneyin,
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
}
