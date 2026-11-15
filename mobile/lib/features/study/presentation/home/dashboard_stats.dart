/// Dashboard haftalık istatistik değer nesnesi (verilen/tamamlanan).
class WeeklyStat {
  const WeeklyStat({required this.given, required this.done});

  final int given;
  final int done;

  double get ratio => given == 0 ? 0.0 : (done / given).clamp(0.0, 1.0);
}
