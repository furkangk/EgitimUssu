import 'package:egitim_ussu_mobile/core/money/money.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  for (final entry in {
    '1.500': 150000,
    '1500': 150000,
    '750,50': 75050,
    '0,5': 50,
    '1.500,50': 150050,
    '1.500,5': 150050,
    '0': 0,
    ' 12,01 ': 1201,
  }.entries) {
    test(
      'TR ${entry.key} -> minor ${entry.value}',
      () => expect(Money.parseTr(entry.key).minor, entry.value),
    );
  }
  for (final value in [
    'abc',
    '-5',
    'NaN',
    'Infinity',
    '1.50',
    '1,234',
    '1..500',
    '1.50.000',
    '1,2,3',
    '',
  ]) {
    test(
      'invalid $value rejected',
      () => expect(() => Money.parseTr(value), throwsFormatException),
    );
  }
  test('JSON-safe upper bound is preserved or explicitly rejected', () {
    const money = Money(Money.maxMinor);
    expect(Money.fromJson(money.toJsonAmount()).minor, Money.maxMinor);
    expect(() => Money.parseTr('1.000.000.000.000'), throwsFormatException);
    expect(
      () => const Money(Money.maxMinor + 1).toJsonAmount(),
      throwsFormatException,
    );
  });
  test('format keeps cents and currency', () {
    expect(const Money(150050, 'USD').formatTr(), '1.500,50 USD');
    expect(const Money(50).formatTr(withCurrency: false), '0,50');
  });
  test('decimal JSON boundary is exact for ordinary cents', () {
    for (final value in [0, 0.01, 750.5, 1500, 99999999.99]) {
      final money = Money.fromJson(value);
      expect(money.toJsonAmount(), value);
    }
    expect(Money.fromJson(750.5).minor, 75050);
    expect(() => Money.fromJson(1.001), throwsFormatException);
  });
}
