import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:equatable/equatable.dart';

class PaymentsState extends Equatable {
  const PaymentsState({
    this.isLoading = false,
    this.isSaving = false,
    this.savingRecordId,
    this.records = const <PaymentRecord>[],
    this.errorMessage,
    this.successMessage,
  });

  final bool isLoading;
  final bool isSaving;

  /// Şu an güncellenmekte olan kaydın kimliği (tıklanan "Tahsil Et" butonunda
  /// yükleniyor göstergesi için). İşlem bitince null'a döner.
  final String? savingRecordId;
  final List<PaymentRecord> records;
  final String? errorMessage;
  final String? successMessage;

  PaymentsState copyWith({
    bool? isLoading,
    bool? isSaving,
    String? savingRecordId,
    bool clearSavingRecordId = false,
    List<PaymentRecord>? records,
    String? errorMessage,
    String? successMessage,
    bool clearMessages = false,
  }) {
    return PaymentsState(
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      savingRecordId: clearSavingRecordId
          ? null
          : savingRecordId ?? this.savingRecordId,
      records: records ?? this.records,
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
    isSaving,
    savingRecordId,
    records,
    errorMessage,
    successMessage,
  ];
}
