const Object _unset = Object();

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
    this.itemType = 'LessonFee',
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

/// Ödeme listesi filtreleri (sunucu tarafı arama/filtre için).
class PaymentFilters {
  const PaymentFilters({
    this.query = '',
    this.status,
    this.studentId,
    this.studentLabel,
    this.dateFromUtc,
    this.dateToUtc,
  });

  /// Açıklama metni araması.
  final String query;

  /// `Paid` / `Pending` / `PartiallyPaid` / `Overdue` / `Cancelled` — null = tümü.
  final String? status;
  final String? studentId;

  /// Seçili öğrencinin gösterim adı (rozet/çip için).
  final String? studentLabel;
  final DateTime? dateFromUtc;
  final DateTime? dateToUtc;

  bool get hasDate => dateFromUtc != null || dateToUtc != null;

  /// Gelişmiş (sekme dışı) aktif filtre sayısı: öğrenci + tarih.
  int get advancedCount => (studentId != null ? 1 : 0) + (hasDate ? 1 : 0);

  PaymentFilters copyWith({
    String? query,
    Object? status = _unset,
    Object? studentId = _unset,
    Object? studentLabel = _unset,
    Object? dateFromUtc = _unset,
    Object? dateToUtc = _unset,
  }) {
    return PaymentFilters(
      query: query ?? this.query,
      status: status == _unset ? this.status : status as String?,
      studentId: studentId == _unset ? this.studentId : studentId as String?,
      studentLabel: studentLabel == _unset
          ? this.studentLabel
          : studentLabel as String?,
      dateFromUtc: dateFromUtc == _unset
          ? this.dateFromUtc
          : dateFromUtc as DateTime?,
      dateToUtc: dateToUtc == _unset ? this.dateToUtc : dateToUtc as DateTime?,
    );
  }
}

/// Sunucudan dönen bir ödeme sayfası + toplam eşleşen kayıt sayısı.
class PaymentPage {
  const PaymentPage({required this.items, required this.totalCount});

  final List<PaymentRecord> items;
  final int totalCount;
}

class PaymentCurrencySummary {
  const PaymentCurrencySummary({
    required this.currency,
    required this.pendingCount,
    required this.partialCount,
    required this.paidCount,
    required this.overdueCount,
    required this.cancelledCount,
    required this.expectedAmountTotal,
    required this.collectedAmountTotal,
    required this.outstandingAmountTotal,
    required this.overdueAmountTotal,
  });

  final String currency;
  final int pendingCount;
  final int partialCount;
  final int paidCount;
  final int overdueCount;
  final int cancelledCount;
  final double expectedAmountTotal;
  final double collectedAmountTotal;
  final double outstandingAmountTotal;
  final double overdueAmountTotal;
}

/// Grafiklerde kullanılan aylık nokta (beklenen/tahsil edilen).
class PaymentMonthlyPoint {
  const PaymentMonthlyPoint({
    required this.year,
    required this.month,
    required this.expectedAmount,
    required this.collectedAmount,
  });

  final int year;
  final int month;
  final double expectedAmount;
  final double collectedAmount;
}

class PaymentSummary {
  const PaymentSummary({
    required this.totalRecords,
    required this.currencySummaries,
    this.monthlyBreakdown = const <PaymentMonthlyPoint>[],
  });

  final int totalRecords;
  final List<PaymentCurrencySummary> currencySummaries;
  final List<PaymentMonthlyPoint> monthlyBreakdown;

  // Para birimleri-arası toplamlar (TRY-öncelikli); özet paneli + donut için.
  double get collectedTotal =>
      currencySummaries.fold(0, (t, c) => t + c.collectedAmountTotal);
  double get outstandingTotal =>
      currencySummaries.fold(0, (t, c) => t + c.outstandingAmountTotal);
  double get overdueTotal =>
      currencySummaries.fold(0, (t, c) => t + c.overdueAmountTotal);

  /// Gecikmemiş açık bakiye = toplam açık − gecikmiş.
  double get pendingTotal => (outstandingTotal - overdueTotal).clamp(
    0,
    double.infinity,
  );
}

abstract interface class PaymentRepository {
  Future<PaymentRecord> createRecord(PaymentRecord paymentRecord);
  Future<PaymentRecord> updateRecord(PaymentRecord paymentRecord);
  Future<List<PaymentRecord>> listTeacherRecords(String teacherUserId);
  Future<PaymentPage> searchRecords(
    String teacherUserId, {
    required PaymentFilters filters,
    required int skip,
    required int take,
  });
  Future<PaymentSummary> getSummary(String teacherUserId);
}
