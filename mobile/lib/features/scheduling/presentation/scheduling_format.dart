import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:flutter/material.dart';

/// Takvim/programlama ekranları için ortak biçimlendirme yardımcıları (yerel saat, Türkçe).
class SchedulingFormat {
  const SchedulingFormat._();

  static const List<String> _monthsLong = <String>[
    '',
    'Ocak',
    'Şubat',
    'Mart',
    'Nisan',
    'Mayıs',
    'Haziran',
    'Temmuz',
    'Ağustos',
    'Eylül',
    'Ekim',
    'Kasım',
    'Aralık',
  ];

  static const List<String> _weekdaysLong = <String>[
    'Pazartesi',
    'Salı',
    'Çarşamba',
    'Perşembe',
    'Cuma',
    'Cumartesi',
    'Pazar',
  ];

  static const List<String> _weekdaysShort = <String>[
    'Pzt',
    'Sal',
    'Çar',
    'Per',
    'Cum',
    'Cmt',
    'Paz',
  ];

  /// UTC anı yerel saate çevirip "HH:mm" döner.
  static String hm(DateTime utc) {
    final d = utc.toLocal();
    return '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
  }

  /// "15:00 - 16:00" (yerel).
  static String timeRange(DateTime startUtc, DateTime endUtc) =>
      '${hm(startUtc)} - ${hm(endUtc)}';

  /// "8 Temmuz 2026 Salı" (yerel, verilen tarih zaten yerel kabul edilir).
  static String longDate(DateTime local) =>
      '${local.day} ${_monthsLong[local.month]} ${local.year} ${_weekdaysLong[local.weekday - 1]}';

  /// "8 Tem" gibi kısa gün etiketi (gün şeridi için).
  static String dayChip(DateTime local) =>
      '${local.day} ${_monthsLong[local.month].substring(0, 3)}';

  static String weekdayShort(DateTime local) =>
      _weekdaysShort[local.weekday - 1];

  static String weekdayLong(DateTime local) => _weekdaysLong[local.weekday - 1];

  /// "8 Temmuz Salı" — gün başlığı.
  static String dayHeader(DateTime local) =>
      '${local.day} ${_monthsLong[local.month]} ${_weekdaysLong[local.weekday - 1]}';

  /// "Temmuz 2026" — ay görünümü başlığı.
  static String monthYear(DateTime local) =>
      '${_monthsLong[local.month]} ${local.year}';

  /// "1 Tem - 7 Tem 2026" — hafta görünümü başlığı (Pazartesi başlangıçlı).
  static String weekRange(DateTime local) {
    final day = DateTime(local.year, local.month, local.day);
    final start = day.subtract(Duration(days: day.weekday - 1));
    final end = start.add(const Duration(days: 6));
    String short(DateTime d) =>
        '${d.day} ${_monthsLong[d.month].substring(0, 3)}';
    return '${short(start)} - ${short(end)} ${end.year}';
  }

  /// iCal tekrar kuralını insan-okur özete çevirir ("Her hafta Pzt, Çar").
  static String recurrenceSummary(String? rule) {
    if (rule == null || rule.trim().isEmpty) return 'Tek seferlik';
    final upper = rule.toUpperCase();
    if (upper.contains('FREQ=DAILY')) return 'Her gün';
    if (upper.contains('FREQ=MONTHLY')) return 'Her ay';
    if (upper.contains('FREQ=WEEKLY')) {
      final byDay = RegExp(r'BYDAY=([A-Z,]+)').firstMatch(upper)?.group(1);
      if (byDay == null || byDay.isEmpty) return 'Her hafta';
      const map = <String, String>{
        'MO': 'Pzt',
        'TU': 'Sal',
        'WE': 'Çar',
        'TH': 'Per',
        'FR': 'Cum',
        'SA': 'Cmt',
        'SU': 'Paz',
      };
      final days = byDay.split(',').map((d) => map[d] ?? d).join(', ');
      return 'Her hafta $days';
    }
    return 'Tekrarlı';
  }

  /// "#RRGGBB" → Color; geçersizse [fallback].
  static Color colorFromHex(
    String? hex, {
    Color fallback = AppColors.accentTeal,
  }) {
    if (hex == null) return fallback;
    var value = hex.trim().replaceFirst('#', '');
    if (value.length == 6) value = 'FF$value';
    if (value.length != 8) return fallback;
    final parsed = int.tryParse(value, radix: 16);
    return parsed == null ? fallback : Color(parsed);
  }
}
