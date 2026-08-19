import 'package:egitim_ussu_mobile/features/study/presentation/timer/manual_session_store.dart';
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

  test('ManualSessionStore ekle/sil çalışır (yerel demo)', () {
    final store = ManualSessionStore();
    store.add(
      ManualSession(
        id: 'a',
        subject: 'Matematik',
        dayUtc: DateTime.utc(2026, 8, 19),
        minutes: 45,
      ),
    );
    expect(store.sessions.length, 1);
    store.remove('a');
    expect(store.sessions, isEmpty);
  });
}
