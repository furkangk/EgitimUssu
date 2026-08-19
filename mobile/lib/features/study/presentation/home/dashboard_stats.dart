/// Dashboard haftalık istatistik değer nesnesi (verilen/tamamlanan).
class WeeklyStat {
  const WeeklyStat({required this.given, required this.done});

  final int given;
  final int done;

  double get ratio => given == 0 ? 0.0 : (done / given).clamp(0.0, 1.0);
}

/// Backend'i henüz olmayan haftalık istatistikler için demo değerleri.
/// Gerçek veri gelince bu fabrikalar repository'ye taşınır (Ö-A/Ö-B işleri).
class DashboardStats {
  const DashboardStats._();

  static WeeklyStat demoWeeklyHomework() => const WeeklyStat(given: 5, done: 3);
  static WeeklyStat demoWeeklyLessons() => const WeeklyStat(given: 4, done: 2);
}
