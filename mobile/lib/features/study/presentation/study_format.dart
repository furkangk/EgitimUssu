/// Study ekranlarında ortak biçimlendirme yardımcıları.
class StudyFormat {
  /// Dakikayı "2s 15dk" / "45dk" biçiminde gösterir.
  static String minutes(int totalMinutes) {
    if (totalMinutes <= 0) return '0dk';
    final h = totalMinutes ~/ 60;
    final m = totalMinutes % 60;
    if (h > 0 && m > 0) return '${h}s ${m}dk';
    if (h > 0) return '${h}s';
    return '${m}dk';
  }

  /// Saniyeyi kronometre biçiminde gösterir (HH:MM:SS veya MM:SS).
  static String stopwatch(int totalSeconds) {
    final s = totalSeconds < 0 ? 0 : totalSeconds;
    final h = s ~/ 3600;
    final m = (s % 3600) ~/ 60;
    final sec = s % 60;
    String two(int v) => v.toString().padLeft(2, '0');
    return h > 0 ? '${two(h)}:${two(m)}:${two(sec)}' : '${two(m)}:${two(sec)}';
  }

  /// Net değerini gereksiz ondalıkları atarak gösterir (28, 28.5, 28.25).
  static String net(double value) {
    if (value == value.roundToDouble()) return value.toStringAsFixed(0);
    final s = value.toStringAsFixed(2);
    return s.endsWith('0') ? s.substring(0, s.length - 1) : s;
  }
}
