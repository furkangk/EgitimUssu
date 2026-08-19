import 'package:egitim_ussu_mobile/features/study/presentation/performance/personal_records.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('bestNet / averageNet boş listede 0', () {
    expect(bestNet(const []), 0);
    expect(averageNet(const []), 0);
  });

  test('bestNet en yüksek; averageNet ortalama', () {
    expect(bestNet(const [12.5, 30.0, 22.0]), 30.0);
    expect(averageNet(const [10, 20, 30]), closeTo(20.0, 1e-9));
  });

  test('weakTopics eşik altını artan skorla döndürür', () {
    final weak = weakTopics({'Türev': 40, 'Limit': 80, 'İntegral': 55});
    expect(weak, ['Türev', 'İntegral']);
  });
}
