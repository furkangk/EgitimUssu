/// Para yalnız kuruş olarak tutulur. Decimal dönüşümü yalnız JSON sınırındadır.
class Money {
  const Money(this.minor, [this.currency = 'TRY']);
  final int minor;
  final String currency;

  // JSON number ve Flutter web üzerinde kuruş kaybetmeyen uygulama sınırı.
  static const maxMinor = 99999999999999;

  factory Money.parseTr(String value, [String currency = 'TRY']) {
    final text = value.trim();
    if (!RegExp(
      r'^(?:[0-9]+|[1-9][0-9]{0,2}(?:\.[0-9]{3})+)(?:,[0-9]{1,2})?$',
    ).hasMatch(text)) {
      throw const FormatException('money.invalid');
    }
    final parts = text.replaceAll('.', '').split(',');
    return Money(_minor(parts[0], parts.length == 2 ? parts[1] : ''), currency);
  }

  factory Money.fromJson(Object? value, [String currency = 'TRY']) {
    if (value == null) return Money(0, currency);
    final match = RegExp(r'^(-?)([0-9]+)(?:\.([0-9]+))?$').firstMatch('$value');
    if (match == null) throw const FormatException('money.invalid_decimal');
    var fraction = match[3] ?? '';
    while (fraction.endsWith('0')) {
      fraction = fraction.substring(0, fraction.length - 1);
    }
    if (fraction.length > 2) {
      throw const FormatException('money.invalid_precision');
    }
    final minor = _minor(match[2]!, fraction);
    return Money(match[1] == '-' ? -minor : minor, currency);
  }

  static int _minor(String whole, String fraction) {
    final parsed =
        BigInt.parse(whole) * BigInt.from(100) +
        BigInt.parse(fraction.padRight(2, '0'));
    if (parsed > BigInt.from(maxMinor)) {
      throw const FormatException('money.too_large');
    }
    return parsed.toInt();
  }

  num toJsonAmount() {
    if (minor.abs() > maxMinor) throw const FormatException('money.too_large');
    return minor / 100;
  }

  String formatTr({bool withCurrency = true}) {
    final digits = (minor.abs() ~/ 100).toString();
    final grouped = digits.replaceAllMapped(
      RegExp(r'\B(?=(\d{3})+(?!\d))'),
      (_) => '.',
    );
    final cents = (minor.abs() % 100).toString().padLeft(2, '0');
    return '${minor < 0 ? '-' : ''}$grouped,$cents${withCurrency ? ' $currency' : ''}';
  }
}
