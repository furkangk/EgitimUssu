class PaymentRecord {
  const PaymentRecord({
    required this.id,
    required this.teacherUserId,
    required this.studentId,
    required this.description,
    required this.currency,
    required this.expectedAmount,
    required this.collectedAmount,
    required this.outstandingAmount,
    required this.status,
    this.relatedLessonSessionId,
    this.dueDateUtc,
    this.collectedOnUtc,
    this.notes,
    this.isOverdue = false,
    this.itemType = 'SingleLesson',
  });

  final String id;
  final String teacherUserId;
  final String studentId;
  final String description;
  final String currency;
  final double expectedAmount;
  final double collectedAmount;
  final double outstandingAmount;
  final String status;
  final String? relatedLessonSessionId;
  final DateTime? dueDateUtc;
  final DateTime? collectedOnUtc;
  final String? notes;
  final bool isOverdue;
  final String itemType;
}

class PaymentCurrencySummary {
  const PaymentCurrencySummary({
    required this.currency,
    required this.pendingCount,
    required this.partialCount,
    required this.paidCount,
    required this.overdueCount,
    required this.outstandingAmountTotal,
    required this.overdueAmountTotal,
  });

  final String currency;
  final int pendingCount;
  final int partialCount;
  final int paidCount;
  final int overdueCount;
  final double outstandingAmountTotal;
  final double overdueAmountTotal;
}

class PaymentSummary {
  const PaymentSummary({
    required this.totalRecords,
    required this.currencySummaries,
  });

  final int totalRecords;
  final List<PaymentCurrencySummary> currencySummaries;
}

abstract interface class PaymentRepository {
  Future<PaymentRecord> createRecord(PaymentRecord paymentRecord);
  Future<PaymentRecord> updateRecord(PaymentRecord paymentRecord);
  Future<List<PaymentRecord>> listTeacherRecords(String teacherUserId);
  Future<PaymentSummary> getSummary(String teacherUserId);
}
