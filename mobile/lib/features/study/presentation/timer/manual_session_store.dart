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

/// Sonradan elle eklenen ("unutulan") çalışma kaydı. Demo/yerel — backend yok.
@immutable
class ManualSession {
  const ManualSession({
    required this.id,
    required this.subject,
    this.topic,
    required this.dayUtc,
    required this.minutes,
  });

  final String id;
  final String subject;
  final String? topic;
  final DateTime dayUtc;
  final int minutes;
}

/// Manuel seansların yerel/bellek-içi demo deposu (Ö-A2/Ö-E backend'i gelene dek).
/// UI tam çalışır ama kayıtlar oturum içinde kalır, backend'e gitmez.
///
/// Sayfa tarafı tek bir örneği ekranın state'inde tutup (sheet'ler arasında)
/// yeniden kullanır ki eklenen kayıtlar sheet kapanıp açılınca kaybolmasın;
/// sınıfın kendisi tekil (singleton) zorunluluğu getirmez — testte olduğu gibi
/// bağımsız bir örnek de oluşturulabilir.
class ManualSessionStore extends ChangeNotifier {
  final List<ManualSession> _sessions = <ManualSession>[];

  List<ManualSession> get sessions => List.unmodifiable(_sessions);
  Listenable get listenable => this;

  void add(ManualSession session) {
    _sessions.add(session);
    notifyListeners();
  }

  void remove(String id) {
    _sessions.removeWhere((s) => s.id == id);
    notifyListeners();
  }
}
