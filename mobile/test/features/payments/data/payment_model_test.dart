import 'package:egitim_ussu_mobile/features/payments/data/models/payment_model.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  PaymentRecordModel recordWith({
    required String status,
    String itemType = 'LessonFee',
  }) {
    return PaymentRecordModel(
      id: 'payment-id',
      teacherUserId: 'teacher-id',
      studentId: 'student-id',
      description: 'Matematik dersi',
      currency: 'TRY',
      expectedAmount: 750,
      collectedAmount: 250,
      outstandingAmount: 500,
      status: status,
      itemType: itemType,
      dueDateUtc: DateTime.utc(2026, 5, 15, 12),
    );
  }

  test('maps payment record upsert payload and summary fields', () {
    final dueDate = DateTime.utc(2026, 5, 15, 12);
    final record = recordWith(status: 'PartiallyPaid');

    final payload = record.toUpsertPayload();

    expect(payload['itemType'], 1);
    // Backend PaymentStatus.PartiallyPaid == 2 (önceden hatalı biçimde 3 gönderiliyordu).
    expect(payload['status'], 2);
    expect(payload['dueDateUtc'], dueDate.toIso8601String());

    final summary = PaymentCurrencySummaryModel.fromJson(<String, dynamic>{
      'currency': 'TRY',
      'pendingCount': 1,
      'partialCount': 2,
      'paidCount': 3,
      'overdueCount': 4,
      'outstandingAmountTotal': 500,
      'overdueAmountTotal': 125,
    });

    expect(summary.partialCount, 2);
    expect(summary.overdueAmountTotal, 125);
  });

  // Regresyon: mobil status/itemType eşlemesi backend enum'larıyla birebir olmalı.
  // Backend: PaymentStatus { Pending=1, PartiallyPaid=2, Paid=3, Overdue=4, Cancelled=5 },
  //          BillingItemType { LessonFee=1, MonthlyPackage=2, ManualAdjustment=3 }.
  // Önceki hata: 'Tahsil Et' 'Paid' göndermesine rağmen 2 (PartiallyPaid) map'leniyor,
  // kayıt "Ödendi"ye geçmiyordu ("hiçbir şey olmuyor").
  test('status maps to backend PaymentStatus values', () {
    expect(recordWith(status: 'Pending').toUpsertPayload()['status'], 1);
    expect(recordWith(status: 'PartiallyPaid').toUpsertPayload()['status'], 2);
    expect(recordWith(status: 'Paid').toUpsertPayload()['status'], 3);
    expect(recordWith(status: 'Overdue').toUpsertPayload()['status'], 4);
    expect(recordWith(status: 'Cancelled').toUpsertPayload()['status'], 5);
  });

  test('itemType maps to backend BillingItemType values', () {
    expect(
      recordWith(status: 'Pending', itemType: 'LessonFee')
          .toUpsertPayload()['itemType'],
      1,
    );
    expect(
      recordWith(status: 'Pending', itemType: 'MonthlyPackage')
          .toUpsertPayload()['itemType'],
      2,
    );
    expect(
      recordWith(status: 'Pending', itemType: 'ManualAdjustment')
          .toUpsertPayload()['itemType'],
      3,
    );
  });
}
