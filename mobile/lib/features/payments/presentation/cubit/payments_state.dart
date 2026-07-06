import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:equatable/equatable.dart';

class PaymentsState extends Equatable {
  const PaymentsState({
    this.isLoading = false,
    this.isLoadingMore = false,
    this.isSaving = false,
    this.savingRecordId,
    this.records = const <PaymentRecord>[],
    this.totalCount = 0,
    this.summary,
    this.filters = const PaymentFilters(),
    this.errorMessage,
    this.successMessage,
  });

  final bool isLoading;

  /// Sonraki sayfa (sonsuz kaydırma) yükleniyor.
  final bool isLoadingMore;
  final bool isSaving;

  /// Şu an güncellenmekte olan kaydın kimliği (tıklanan "Tahsil Et" için).
  final String? savingRecordId;

  /// Şu ana kadar yüklenmiş (biriken) kayıtlar.
  final List<PaymentRecord> records;

  /// Aktif filtreyle eşleşen **toplam** kayıt sayısı (sunucudan).
  final int totalCount;
  final PaymentSummary? summary;
  final PaymentFilters filters;
  final String? errorMessage;
  final String? successMessage;

  /// Daha yüklenecek kayıt var mı?
  bool get hasMore => records.length < totalCount;

  PaymentsState copyWith({
    bool? isLoading,
    bool? isLoadingMore,
    bool? isSaving,
    String? savingRecordId,
    bool clearSavingRecordId = false,
    List<PaymentRecord>? records,
    int? totalCount,
    PaymentSummary? summary,
    PaymentFilters? filters,
    String? errorMessage,
    String? successMessage,
    bool clearMessages = false,
  }) {
    return PaymentsState(
      isLoading: isLoading ?? this.isLoading,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      isSaving: isSaving ?? this.isSaving,
      savingRecordId: clearSavingRecordId
          ? null
          : savingRecordId ?? this.savingRecordId,
      records: records ?? this.records,
      totalCount: totalCount ?? this.totalCount,
      summary: summary ?? this.summary,
      filters: filters ?? this.filters,
      errorMessage: clearMessages
          ? errorMessage
          : errorMessage ?? this.errorMessage,
      successMessage: clearMessages
          ? successMessage
          : successMessage ?? this.successMessage,
    );
  }

  @override
  List<Object?> get props => <Object?>[
    isLoading,
    isLoadingMore,
    isSaving,
    savingRecordId,
    records,
    totalCount,
    summary,
    filters,
    errorMessage,
    successMessage,
  ];
}
