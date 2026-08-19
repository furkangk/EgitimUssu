import 'package:egitim_ussu_mobile/features/study/presentation/timer/timer_accumulator.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test(
    'totalSeconds = çalışma + mola; mola net süreye eklenmez ayrı tutulur',
    () {
      const acc = TimerAccumulator(
        studySeconds: 600,
        breakSeconds: 120,
        breakCount: 1,
      );
      expect(acc.totalSeconds, 720);
      expect(acc.studySeconds, 600);
    },
  );

  test('startBreak mola sayısını artırır', () {
    const acc = TimerAccumulator();
    final next = acc.startBreak();
    expect(next.breakCount, 1);
  });
}
