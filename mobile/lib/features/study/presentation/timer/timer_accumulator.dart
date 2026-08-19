import 'package:flutter/foundation.dart';

/// Kronometre çalışma + mola birikimini tutan değişmez değer nesnesi.
/// Mola süresi net (çalışma) süreye EKLENMEZ; ayrı tutulur, toplam ayrı hesaplanır.
@immutable
class TimerAccumulator {
  const TimerAccumulator({
    this.studySeconds = 0,
    this.breakSeconds = 0,
    this.breakCount = 0,
  });

  final int studySeconds;
  final int breakSeconds;
  final int breakCount;

  int get totalSeconds => studySeconds + breakSeconds;

  TimerAccumulator startBreak() => copyWith(breakCount: breakCount + 1);

  TimerAccumulator copyWith({
    int? studySeconds,
    int? breakSeconds,
    int? breakCount,
  }) => TimerAccumulator(
    studySeconds: studySeconds ?? this.studySeconds,
    breakSeconds: breakSeconds ?? this.breakSeconds,
    breakCount: breakCount ?? this.breakCount,
  );
}
