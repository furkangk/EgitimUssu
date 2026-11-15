import 'package:egitim_ussu_mobile/core/money/money.dart';
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
      expectedAmount: Money.fromJson(json['expectedAmount']).minor,
      collectedAmount: Money.fromJson(json['collectedAmount']).minor,
      outstandingAmount: Money.fromJson(json['outstandingAmount']).minor,
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
    int expectedAmount = 75000,
    int collectedAmount = 0,
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
      'expectedAmount': Money(expectedAmount, currency).toJsonAmount(),
      'collectedAmount': Money(collectedAmount, currency).toJsonAmount(),
      'dueDateUtc': (dueDateUtc ?? DateTime.now().toUtc()).toIso8601String(),
      'collectedOnUtc': collectedOnUtc?.toIso8601String(),
      'status': _statusToApi(status),
      'billingPeriodStartUtc': null,
      'billingPeriodEndUtc': null,
      'notes': notes,
    };
  }

  Map<String, dynamic> toDetailsPayload() => {
    'description': description,
    'expectedAmount': Money(expectedAmount, currency).toJsonAmount(),
    'dueDateUtc': dueDateUtc?.toUtc().toIso8601String(),
    'notes': notes,
  };

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

  // Backend enum: BillingItemType { LessonFee=1, MonthlyPackage=2, ManualAdjustment=3, LateCancellation=4 }.
  // API yanıtı enum ADINI string döndürür (`ItemType.ToString()`), bu yüzden aynı adları eşleriz.
  static int _itemTypeToApi(String? value) {
    return switch (value) {
      'MonthlyPackage' => 2,
      'ManualAdjustment' => 3,
      'LateCancellation' => 4,
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
      expectedAmountTotal: Money.fromJson(json['expectedAmountTotal']).minor,
      collectedAmountTotal: Money.fromJson(json['collectedAmountTotal']).minor,
      outstandingAmountTotal: Money.fromJson(
        json['outstandingAmountTotal'],
      ).minor,
      overdueAmountTotal: Money.fromJson(json['overdueAmountTotal']).minor,
    );
  }
}

class PaymentMonthlyPointModel extends PaymentMonthlyPoint {
  const PaymentMonthlyPointModel({
    required super.year,
    required super.month,
    super.currency,
    required super.expectedAmount,
    required super.collectedAmount,
  });

  factory PaymentMonthlyPointModel.fromJson(Map<String, dynamic> json) {
    return PaymentMonthlyPointModel(
      currency: json['currency']?.toString() ?? 'TRY',
      year: json['year'] as int? ?? 0,
      month: json['month'] as int? ?? 0,
      expectedAmount: Money.fromJson(json['expectedAmount']).minor,
      collectedAmount: Money.fromJson(json['collectedAmount']).minor,
    );
  }
}

class PaymentSummaryModel extends PaymentSummary {
  const PaymentSummaryModel({
    required super.totalRecords,
    required super.currencySummaries,
    super.monthlyBreakdown,
  });

  factory PaymentSummaryModel.fromJson(Map<String, dynamic> json) {
    return PaymentSummaryModel(
      totalRecords: json['totalRecords'] as int? ?? 0,
      currencySummaries:
          ((json['currencySummaries'] as List<dynamic>? ?? <dynamic>[]))
              .whereType<Map<String, dynamic>>()
              .map(PaymentCurrencySummaryModel.fromJson)
              .toList(),
      monthlyBreakdown:
          ((json['monthlyBreakdown'] as List<dynamic>? ?? <dynamic>[]))
              .whereType<Map<String, dynamic>>()
              .map(PaymentMonthlyPointModel.fromJson)
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
          expectedAmountTotal: 150000,
          collectedAmountTotal: 75000,
          outstandingAmountTotal: 75000,
          overdueAmountTotal: 25000,
        ),
      ],
    );
  }
}
