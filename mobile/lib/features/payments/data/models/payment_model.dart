import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';

class PaymentRecordModel extends PaymentRecord {
  const PaymentRecordModel({
    required super.id,
    required super.teacherUserId,
    required super.studentId,
    required super.description,
    required super.currency,
    required super.expectedAmount,
    required super.collectedAmount,
    required super.outstandingAmount,
    required super.status,
    super.relatedLessonSessionId,
    super.dueDateUtc,
    super.collectedOnUtc,
    super.notes,
    super.isOverdue,
    super.itemType,
  });

  factory PaymentRecordModel.fromJson(Map<String, dynamic> json) {
    return PaymentRecordModel(
      id: json['id']?.toString() ?? '',
      teacherUserId: json['teacherUserId']?.toString() ?? '',
      studentId: json['studentId']?.toString() ?? '',
      description: json['description']?.toString() ?? '',
      currency: json['currency']?.toString() ?? 'TRY',
      expectedAmount: (json['expectedAmount'] as num?)?.toDouble() ?? 0,
      collectedAmount: (json['collectedAmount'] as num?)?.toDouble() ?? 0,
      outstandingAmount: (json['outstandingAmount'] as num?)?.toDouble() ?? 0,
      status: json['status']?.toString() ?? 'Pending',
      relatedLessonSessionId: json['relatedLessonSessionId']?.toString(),
      dueDateUtc: _parseDate(json['dueDateUtc']),
      collectedOnUtc: _parseDate(json['collectedOnUtc']),
      notes: json['notes']?.toString(),
      isOverdue: json['isOverdue'] as bool? ?? false,
      itemType: json['itemType']?.toString() ?? 'LessonFee',
    );
  }

  factory PaymentRecordModel.demo({
    required String id,
    required String teacherUserId,
    required String studentId,
    required String description,
    double expectedAmount = 750,
    double collectedAmount = 0,
    DateTime? dueDateUtc,
  }) {
    return PaymentRecordModel(
      id: id,
      teacherUserId: teacherUserId,
      studentId: studentId,
      description: description,
      currency: 'TRY',
      expectedAmount: expectedAmount,
      collectedAmount: collectedAmount,
      outstandingAmount: expectedAmount - collectedAmount,
      status: collectedAmount >= expectedAmount ? 'Paid' : 'Pending',
      dueDateUtc:
          dueDateUtc ?? DateTime.now().toUtc().add(const Duration(days: 3)),
      isOverdue: false,
      itemType: 'LessonFee',
    );
  }

  Map<String, dynamic> toUpsertPayload() {
    return <String, dynamic>{
      'teacherUserId': teacherUserId,
      'studentId': studentId,
      'relatedLessonSessionId': _emptyToNull(relatedLessonSessionId),
      'itemType': _itemTypeToApi(itemType),
      'description': description,
      'currency': currency,
      'expectedAmount': expectedAmount,
      'collectedAmount': collectedAmount,
      'dueDateUtc': (dueDateUtc ?? DateTime.now().toUtc()).toIso8601String(),
      'collectedOnUtc': collectedOnUtc?.toIso8601String(),
      'status': _statusToApi(status),
      'billingPeriodStartUtc': null,
      'billingPeriodEndUtc': null,
      'notes': notes,
    };
  }

  static DateTime? _parseDate(Object? value) {
    if (value == null) {
      return null;
    }
    return DateTime.tryParse(value.toString())?.toUtc();
  }

  static Object? _emptyToNull(String? value) {
    if (value == null || value.trim().isEmpty) {
      return null;
    }
    return value;
  }

  // Backend enum: BillingItemType { LessonFee=1, MonthlyPackage=2, ManualAdjustment=3 }.
  // API yanıtı enum ADINI string döndürür (`ItemType.ToString()`), bu yüzden aynı adları eşleriz.
  static int _itemTypeToApi(String? value) {
    return switch (value) {
      'MonthlyPackage' => 2,
      'ManualAdjustment' => 3,
      _ => 1, // LessonFee (varsayılan)
    };
  }

  // Backend enum: PaymentStatus { Pending=1, PartiallyPaid=2, Paid=3, Overdue=4, Cancelled=5 }.
  static int _statusToApi(String value) {
    return switch (value) {
      'PartiallyPaid' => 2,
      'Paid' => 3,
      'Overdue' => 4,
      'Cancelled' => 5,
      _ => 1, // Pending
    };
  }
}

class PaymentCurrencySummaryModel extends PaymentCurrencySummary {
  const PaymentCurrencySummaryModel({
    required super.currency,
    required super.pendingCount,
    required super.partialCount,
    required super.paidCount,
    required super.overdueCount,
    required super.cancelledCount,
    required super.expectedAmountTotal,
    required super.collectedAmountTotal,
    required super.outstandingAmountTotal,
    required super.overdueAmountTotal,
  });

  factory PaymentCurrencySummaryModel.fromJson(Map<String, dynamic> json) {
    return PaymentCurrencySummaryModel(
      currency: json['currency']?.toString() ?? 'TRY',
      pendingCount: json['pendingCount'] as int? ?? 0,
      partialCount: json['partialCount'] as int? ?? 0,
      paidCount: json['paidCount'] as int? ?? 0,
      overdueCount: json['overdueCount'] as int? ?? 0,
      cancelledCount: json['cancelledCount'] as int? ?? 0,
      expectedAmountTotal:
          (json['expectedAmountTotal'] as num?)?.toDouble() ?? 0,
      collectedAmountTotal:
          (json['collectedAmountTotal'] as num?)?.toDouble() ?? 0,
      outstandingAmountTotal:
          (json['outstandingAmountTotal'] as num?)?.toDouble() ?? 0,
      overdueAmountTotal: (json['overdueAmountTotal'] as num?)?.toDouble() ?? 0,
    );
  }
}

class PaymentSummaryModel extends PaymentSummary {
  const PaymentSummaryModel({
    required super.totalRecords,
    required super.currencySummaries,
  });

  factory PaymentSummaryModel.fromJson(Map<String, dynamic> json) {
    return PaymentSummaryModel(
      totalRecords: json['totalRecords'] as int? ?? 0,
      currencySummaries:
          ((json['currencySummaries'] as List<dynamic>? ?? <dynamic>[]))
              .whereType<Map<String, dynamic>>()
              .map(PaymentCurrencySummaryModel.fromJson)
              .toList(),
    );
  }

  factory PaymentSummaryModel.demo() {
    return const PaymentSummaryModel(
      totalRecords: 2,
      currencySummaries: <PaymentCurrencySummaryModel>[
        PaymentCurrencySummaryModel(
          currency: 'TRY',
          pendingCount: 1,
          partialCount: 0,
          paidCount: 1,
          overdueCount: 1,
          cancelledCount: 0,
          expectedAmountTotal: 1500,
          collectedAmountTotal: 750,
          outstandingAmountTotal: 750,
          overdueAmountTotal: 250,
        ),
      ],
    );
  }
}
