import 'package:egitim_ussu_mobile/features/payments/data/models/payment_model.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test(
    'monthly currency and cents survive JSON without combining currencies',
    () {
      final summary = PaymentSummaryModel.fromJson({
        'totalRecords': 2,
        'currencySummaries': [
          {'currency': 'TRY', 'collectedAmountTotal': 1500.5},
          {'currency': 'USD', 'collectedAmountTotal': 20.01},
        ],
        'monthlyBreakdown': [
          {
            'year': 2026,
            'month': 11,
            'currency': 'TRY',
            'expectedAmount': 1500.5,
            'collectedAmount': 1500.5,
          },
          {
            'year': 2026,
            'month': 11,
            'currency': 'USD',
            'expectedAmount': 20.01,
            'collectedAmount': 20.01,
          },
        ],
      });
      expect(summary.currencySummaries.first.collectedAmountTotal, 150050);
      expect(summary.currencySummaries.last.collectedAmountTotal, 2001);
      final dynamic point = summary.monthlyBreakdown.last;
      expect(point.currency, 'USD');
      expect(point.collectedAmount, 2001);
    },
  );
}
