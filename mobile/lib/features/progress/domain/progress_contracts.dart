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

abstract interface class ProgressRepository {
  Future<ProgressOverview> getOverview(String studentId);
  Future<List<TopicMastery>> listMastery(String studentId, {String? subject});
}
