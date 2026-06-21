import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:equatable/equatable.dart';

class PaymentsState extends Equatable {
  const PaymentsState({
    this.isLoading = false,
    this.isSaving = false,
    this.records = const <PaymentRecord>[],
    this.summary,
    this.errorMessage,
    this.successMessage,
  });

  final bool isLoading;
  final bool isSaving;
  final List<PaymentRecord> records;
  final PaymentSummary? summary;
  final String? errorMessage;
  final String? successMessage;

  PaymentsState copyWith({
    bool? isLoading,
    bool? isSaving,
    List<PaymentRecord>? records,
    PaymentSummary? summary,
    String? errorMessage,
    String? successMessage,
    bool clearMessages = false,
  }) {
    return PaymentsState(
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      records: records ?? this.records,
      summary: summary ?? this.summary,
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
    records,
    summary,
    errorMessage,
    successMessage,
  ];
}
