// M08 Bireysel Çalışma (Study) — mobil domain sözleşmeleri.
// Backend: `/api/study`. Öğrenci kendi StudentId'sine ait verilere erişir.

class StudySession {
  const StudySession({
    required this.id,
    required this.studentId,
    required this.subject,
    required this.status,
    required this.source,
    required this.effectiveMinutes,
    required this.breakMinutes,
    required this.startedAtUtc,
    this.topic,
    this.endedAtUtc,
    this.personalNote,
  });

  final String id;
  final String studentId;
  final String subject;
  final String? topic;
  final String status; // Running / Paused / Completed / Discarded
  final String source; // Stopwatch / Manual
  final int effectiveMinutes;
  final int breakMinutes;
  final DateTime startedAtUtc;
  final DateTime? endedAtUtc;
  final String? personalNote;

  bool get isRunning => status == 'Running';
  bool get isPaused => status == 'Paused';
  bool get isActive => isRunning || isPaused;
}

class TestResult {
  const TestResult({
    required this.id,
    required this.subject,
    required this.testType,
    required this.totalQuestions,
    required this.correct,
    required this.wrong,
    required this.blank,
    required this.net,
    required this.takenOnUtc,
    this.topic,
    this.testName,
    this.durationMinutes,
  });

  final String id;
  final String subject;
  final String? topic;
  final String? testName;
  final String testType;
  final int totalQuestions;
  final int correct;
  final int wrong;
  final int blank;
  final double net;
  final int? durationMinutes;
  final DateTime takenOnUtc;
}

class StudyGoal {
  const StudyGoal({
    required this.dailyGoalMinutes,
    this.weeklyGoalMinutes,
    this.targetNet,
    this.subject,
    this.isActive = true,
  });

  final int dailyGoalMinutes;
  final int? weeklyGoalMinutes;
  final double? targetNet;
  final String? subject;
  final bool isActive;
}

class StudyStreak {
  const StudyStreak({
    required this.currentStreakDays,
    required this.longestStreakDays,
    required this.totalStudyDays,
    required this.studiedToday,
    required this.todayEffectiveMinutes,
  });

  final int currentStreakDays;
  final int longestStreakDays;
  final int totalStudyDays;
  final bool studiedToday;
  final int todayEffectiveMinutes;
}

class StudyAchievement {
  const StudyAchievement({
    required this.code,
    required this.title,
    required this.description,
    required this.category,
    required this.threshold,
    required this.earned,
    required this.currentValue,
    this.iconKey,
    this.earnedOnUtc,
  });

  final String code;
  final String title;
  final String description;
  final String category;
  final int threshold;
  final bool earned;
  final int currentValue;
  final String? iconKey;
  final DateTime? earnedOnUtc;

  double get progress =>
      threshold <= 0 ? 1 : (currentValue / threshold).clamp(0, 1).toDouble();
}

class SubjectMinutes {
  const SubjectMinutes({
    required this.subject,
    required this.effectiveMinutes,
    required this.sessionCount,
  });

  final String subject;
  final int effectiveMinutes;
  final int sessionCount;
}

class DayMinutes {
  const DayMinutes({
    required this.date,
    required this.effectiveMinutes,
    required this.sessionCount,
  });

  final DateTime date;
  final int effectiveMinutes;
  final int sessionCount;
}

class WeeklySummary {
  const WeeklySummary({
    required this.weekStartDate,
    required this.totalEffectiveMinutes,
    required this.totalBreakMinutes,
    required this.sessionCount,
    required this.perSubject,
    required this.perDay,
  });

  final DateTime weekStartDate;
  final int totalEffectiveMinutes;
  final int totalBreakMinutes;
  final int sessionCount;
  final List<SubjectMinutes> perSubject;
  final List<DayMinutes> perDay;
}

class StudySharing {
  const StudySharing({
    required this.shareStudyWithParent,
    required this.shareTestsWithParent,
    required this.shareStudyWithTeacher,
    required this.shareTestsWithTeacher,
  });

  final bool shareStudyWithParent;
  final bool shareTestsWithParent;
  final bool shareStudyWithTeacher;
  final bool shareTestsWithTeacher;

  static const StudySharing none = StudySharing(
    shareStudyWithParent: false,
    shareTestsWithParent: false,
    shareStudyWithTeacher: false,
    shareTestsWithTeacher: false,
  );
}

class StudyDashboard {
  const StudyDashboard({
    required this.studentId,
    required this.todayEffectiveMinutes,
    required this.todayGoalMinutes,
    required this.todayGoalMet,
    required this.weekEffectiveMinutes,
    required this.currentStreakDays,
    required this.longestStreakDays,
    required this.recentSessions,
    required this.recentAchievements,
    this.activeGoal,
    this.lastTest,
  });

  final String studentId;
  final int todayEffectiveMinutes;
  final int todayGoalMinutes;
  final bool todayGoalMet;
  final int weekEffectiveMinutes;
  final int currentStreakDays;
  final int longestStreakDays;
  final StudyGoal? activeGoal;
  final TestResult? lastTest;
  final List<StudySession> recentSessions;
  final List<StudyAchievement> recentAchievements;
}

/// Katalog konusu (örn. Matematik → Türev). [subjectId] bağlı olduğu dersi işaret eder.
class TopicCatalog {
  const TopicCatalog({
    required this.id,
    required this.subjectId,
    required this.name,
    required this.orderIndex,
    this.isActive = true,
  });

  final String id;
  final String subjectId;
  final String name;
  final int orderIndex;
  final bool isActive;
}

/// Öğrencinin tanımladığı katalog dersi ve konuları. Kronometre, deneme ve takvim
/// bu katalogdan tutarlı ders/konu adları alır.
class SubjectCatalog {
  const SubjectCatalog({
    required this.id,
    required this.studentId,
    required this.name,
    required this.topics,
    this.colorHex,
    this.isActive = true,
  });

  final String id;
  final String studentId;
  final String name;
  final String? colorHex;
  final bool isActive;
  final List<TopicCatalog> topics;
}

/// Öğrencinin kendi ders notu (öğretmen `LessonNote`'undan ayrı).
class StudyNote {
  const StudyNote({
    required this.id,
    required this.title,
    required this.body,
    required this.updatedOnUtc,
    this.subject,
    this.topic,
    this.attachmentUrl,
  });

  final String id;
  final String title;
  final String body;
  final String? subject;
  final String? topic;
  final String? attachmentUrl;
  final DateTime updatedOnUtc;
}

abstract interface class StudyRepository {
  Future<StudyDashboard> getDashboard(String studentId);

  // Öğrenci ders notları
  Future<List<StudyNote>> listNotes(String studentId);
  Future<StudyNote> createNote(
    String studentId, {
    required String title,
    required String body,
    String? subject,
    String? topic,
  });
  Future<StudyNote> updateNote(
    String noteId, {
    required String title,
    required String body,
    String? subject,
    String? topic,
  });
  Future<void> deleteNote(String noteId);

  // Ders/konu kataloğu
  Future<List<SubjectCatalog>> listSubjects(String studentId);
  Future<SubjectCatalog> createSubject(
    String studentId, {
    required String name,
    String? colorHex,
  });
  Future<SubjectCatalog> updateSubject(
    String subjectId, {
    required String name,
    String? colorHex,
    required bool isActive,
  });
  Future<void> deleteSubject(String subjectId);
  Future<TopicCatalog> addTopic(String subjectId, {required String name});
  Future<TopicCatalog> updateTopic(
    String topicId, {
    required String name,
    required int orderIndex,
    required bool isActive,
  });
  Future<void> deleteTopic(String topicId);

  Future<StudyStreak> getStreak(String studentId);
  Future<StudyGoal?> getGoals(String studentId);
  Future<StudyGoal> updateGoals(
    String studentId, {
    required int dailyGoalMinutes,
    int? weeklyGoalMinutes,
    double? targetNet,
    String? subject,
  });

  Future<List<StudySession>> listSessions(String studentId);
  Future<StudySession> startSession(
    String studentId, {
    required String subject,
    String? topic,
  });
  Future<StudySession> pauseSession(String sessionId);
  Future<StudySession> resumeSession(String sessionId);
  Future<StudySession> completeSession(String sessionId, {String? personalNote});
  Future<void> discardSession(String sessionId);
  Future<StudySession> createManualSession(
    String studentId, {
    required String subject,
    String? topic,
    required int effectiveMinutes,
    required DateTime studiedOnUtc,
    String? personalNote,
  });

  Future<List<TestResult>> listTests(String studentId);
  Future<TestResult> recordTest(
    String studentId, {
    required String subject,
    String? topic,
    required String testType,
    String? testName,
    required int totalQuestions,
    required int correct,
    required int wrong,
    required int blank,
    int? penaltyDivisor,
    int? durationMinutes,
    required DateTime takenOnUtc,
  });

  Future<List<StudyAchievement>> getAchievements(String studentId);
  Future<WeeklySummary> getWeeklySummary(String studentId);
  Future<StudySharing> getSharing(String studentId);
  Future<StudySharing> updateSharing(
    String studentId, {
    required bool shareStudyWithParent,
    required bool shareTestsWithParent,
    required bool shareStudyWithTeacher,
    required bool shareTestsWithTeacher,
  });
}
