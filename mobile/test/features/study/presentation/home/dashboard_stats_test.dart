import 'package:egitim_ussu_mobile/features/study/presentation/home/dashboard_stats.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('ratio tamamlanan/verilen; verilen 0 ise 0', () {
    expect(const WeeklyStat(given: 4, done: 3).ratio, closeTo(0.75, 1e-9));
    expect(const WeeklyStat(given: 0, done: 0).ratio, 0.0);
  });

  test('demo istatistikleri makul aralıkta', () {
    expect(DashboardStats.demoWeeklyHomework().given, greaterThanOrEqualTo(DashboardStats.demoWeeklyHomework().done));
    expect(DashboardStats.demoWeeklyLessons().given, greaterThanOrEqualTo(DashboardStats.demoWeeklyLessons().done));
  });
}
