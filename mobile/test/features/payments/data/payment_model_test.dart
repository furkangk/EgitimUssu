import 'package:egitim_ussu_mobile/features/payments/data/models/payment_model.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('maps payment record upsert payload and summary fields', () {
    final dueDate = DateTime.utc(2026, 5, 15, 12);
    final record = PaymentRecordModel(
      id: 'payment-id',
      teacherUserId: 'teacher-id',
      studentId: 'student-id',
      description: 'Matematik dersi',
      currency: 'TRY',
      expectedAmount: 750,
      collectedAmount: 250,
      outstandingAmount: 500,
      status: 'PartiallyPaid',
      dueDateUtc: dueDate,
    );

    final payload = record.toUpsertPayload();

    expect(payload['itemType'], 1);
    expect(payload['status'], 3);
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
}
