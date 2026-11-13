// M10 Gelişim Takibi (ProgressTracking) — mobil domain sözleşmeleri.
// Backend: `/api/progress-tracking`. Öğrenci kendi konu hâkimiyetini/gelişimini görür.

class TopicMastery {
  const TopicMastery({
    required this.subject,
    required this.topic,
    required this.masteryLevel,
    required this.masteryScore,
    required this.totalStudyMinutes,
    required this.testAttemptCount,
    required this.trend,
    required this.isWeakSpot,
    required this.isStrength,
    this.averageNetRatio,
  });

  final String subject;
  final String topic;
  final String masteryLevel; // NotStarted/Weak/Developing/Proficient/Mastered
  final double masteryScore; // 0-100
  final int totalStudyMinutes;
  final int testAttemptCount;
  final double? averageNetRatio;
  final String trend; // Improving/Stable/Declining
  final bool isWeakSpot;
  final bool isStrength;
}

class ProgressOverview {
  const ProgressOverview({
    required this.masteredCount,
    required this.proficientCount,
    required this.developingCount,
    required this.weakCount,
    required this.notStartedCount,
    required this.activeGoalCount,
    required this.weakSpots,
    required this.strengths,
  });

  final int masteredCount;
  final int proficientCount;
  final int developingCount;
  final int weakCount;
  final int notStartedCount;
  final int activeGoalCount;
  final List<TopicMastery> weakSpots;
  final List<TopicMastery> strengths;

  int get trackedTopics =>
      masteredCount + proficientCount + developingCount + weakCount;
}

/// M10-1: Bir dönemin (hafta/ay) gelişim fotoğrafı.
/// [trend] serideki bir önceki dönemle farktan sunucuda hesaplanır — istemci yeniden hesaplamaz.
class ProgressSnapshot {
  const ProgressSnapshot({
    required this.subject,
    required this.period,
    required this.periodStart,
    required this.masteryScore,
    required this.studyMinutes,
    required this.testCount,
    required this.averageNet,
    required this.trend,
  });

  final String subject;
  final String period; // Weekly/Monthly
  final DateTime periodStart;
  final int masteryScore; // 0-100
  final int studyMinutes;
  final int testCount;
  final double averageNet; // 0-1 net oranı
  final String trend; // Improving/Stable/Declining
}

abstract interface class ProgressRepository {
  Future<ProgressOverview> getOverview(String studentId);

  /// M10-2: Öğretmen, **bağlı olduğu** öğrencinin özetini kendi rotasından okur.
  Future<ProgressOverview> getTeacherStudentOverview({
    required String teacherUserId,
    required String studentId,
  });

  /// M10-2: Veli, **onaylı** bağı olan çocuğunun özetini kendi rotasından okur.
  /// Öğrenci paylaşımı kapattıysa sunucu 403 değil **boş özet** döner.
  Future<ProgressOverview> getParentChildOverview({
    required String parentUserId,
    required String studentId,
  });

  Future<List<TopicMastery>> listMastery(String studentId, {String? subject});

  /// M10-1: Snapshot serisi. Uç **yalnız öğrencinin kendisine** (ve Admin'e) açıktır;
  /// öğretmen/veli kapsamında çağrılmaz.
  Future<List<ProgressSnapshot>> listSnapshots(
    String studentId, {
    String? subject,
    String? period,
    DateTime? from,
    DateTime? to,
  });
}
