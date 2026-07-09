import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';

/// `/api/study` JSON yanıtlarını domain nesnelerine dönüştüren yardımcılar.
class StudyMappers {
  static int _int(dynamic v) => v is num ? v.toInt() : int.tryParse('$v') ?? 0;

  static double _double(dynamic v) =>
      v is num ? v.toDouble() : double.tryParse('$v') ?? 0;

  static DateTime _date(dynamic v) =>
      DateTime.tryParse('$v')?.toUtc() ?? DateTime.now().toUtc();

  static DateTime? _dateOrNull(dynamic v) =>
      v == null ? null : DateTime.tryParse('$v')?.toUtc();

  static StudyNote note(Map<String, dynamic> j) => StudyNote(
        id: '${j['id']}',
        title: '${j['title']}',
        body: '${j['body']}',
        subject: j['subject'] as String?,
        topic: j['topic'] as String?,
        attachmentUrl: j['attachmentUrl'] as String?,
        updatedOnUtc: _date(j['updatedOnUtc']),
      );

  static TopicCatalog topicCatalog(Map<String, dynamic> j) => TopicCatalog(
        id: '${j['id']}',
        subjectId: '${j['subjectId']}',
        name: '${j['name']}',
        orderIndex: _int(j['orderIndex']),
        isActive: j['isActive'] as bool? ?? true,
      );

  static SubjectCatalog subjectCatalog(Map<String, dynamic> j) => SubjectCatalog(
        id: '${j['id']}',
        studentId: '${j['studentId']}',
        name: '${j['name']}',
        colorHex: j['colorHex'] as String?,
        isActive: j['isActive'] as bool? ?? true,
        topics: (j['topics'] as List<dynamic>? ?? <dynamic>[])
            .whereType<Map<String, dynamic>>()
            .map(topicCatalog)
            .toList(),
      );

  static StudySession session(Map<String, dynamic> j) => StudySession(
        id: '${j['id']}',
        studentId: '${j['studentId']}',
        subject: '${j['subject']}',
        topic: j['topic'] as String?,
        status: '${j['status']}',
        source: '${j['source']}',
        effectiveMinutes: _int(j['effectiveMinutes']),
        breakMinutes: _int(j['breakMinutes']),
        startedAtUtc: _date(j['startedAtUtc']),
        endedAtUtc: _dateOrNull(j['endedAtUtc']),
        personalNote: j['personalNote'] as String?,
      );

  static TestResult test(Map<String, dynamic> j) => TestResult(
        id: '${j['id']}',
        subject: '${j['subject']}',
        topic: j['topic'] as String?,
        testName: j['testName'] as String?,
        testType: '${j['testType']}',
        totalQuestions: _int(j['totalQuestions']),
        correct: _int(j['correct']),
        wrong: _int(j['wrong']),
        blank: _int(j['blank']),
        net: _double(j['net']),
        durationMinutes: j['durationMinutes'] == null
            ? null
            : _int(j['durationMinutes']),
        takenOnUtc: _date(j['takenOnUtc']),
      );

  static StudyGoal? goal(Map<String, dynamic>? j) {
    if (j == null) return null;
    return StudyGoal(
      dailyGoalMinutes: _int(j['dailyGoalMinutes']),
      weeklyGoalMinutes:
          j['weeklyGoalMinutes'] == null ? null : _int(j['weeklyGoalMinutes']),
      targetNet: j['targetNet'] == null ? null : _double(j['targetNet']),
      subject: j['subject'] as String?,
      isActive: j['isActive'] as bool? ?? true,
    );
  }

  static StudyStreak streak(Map<String, dynamic> j) => StudyStreak(
        currentStreakDays: _int(j['currentStreakDays']),
        longestStreakDays: _int(j['longestStreakDays']),
        totalStudyDays: _int(j['totalStudyDays']),
        studiedToday: j['studiedToday'] as bool? ?? false,
        todayEffectiveMinutes: _int(j['todayEffectiveMinutes']),
      );

  static StudyAchievement achievement(Map<String, dynamic> j) => StudyAchievement(
        code: '${j['code']}',
        title: '${j['title']}',
        description: '${j['description']}',
        category: '${j['category']}',
        threshold: _int(j['threshold']),
        earned: j['earned'] as bool? ?? false,
        currentValue: _int(j['currentValue']),
        iconKey: j['iconKey'] as String?,
        earnedOnUtc: _dateOrNull(j['earnedOnUtc']),
      );

  static WeeklySummary weekly(Map<String, dynamic> j) => WeeklySummary(
        weekStartDate: _date(j['weekStartDate']),
        totalEffectiveMinutes: _int(j['totalEffectiveMinutes']),
        totalBreakMinutes: _int(j['totalBreakMinutes']),
        sessionCount: _int(j['sessionCount']),
        perSubject: (j['perSubject'] as List<dynamic>? ?? <dynamic>[])
            .whereType<Map<String, dynamic>>()
            .map((s) => SubjectMinutes(
                  subject: '${s['subject']}',
                  effectiveMinutes: _int(s['effectiveMinutes']),
                  sessionCount: _int(s['sessionCount']),
                ))
            .toList(),
        perDay: (j['perDay'] as List<dynamic>? ?? <dynamic>[])
            .whereType<Map<String, dynamic>>()
            .map((d) => DayMinutes(
                  date: _date(d['date']),
                  effectiveMinutes: _int(d['effectiveMinutes']),
                  sessionCount: _int(d['sessionCount']),
                ))
            .toList(),
      );

  static StudySharing sharing(Map<String, dynamic> j) => StudySharing(
        shareStudyWithParent: j['shareStudyWithParent'] as bool? ?? false,
        shareTestsWithParent: j['shareTestsWithParent'] as bool? ?? false,
        shareStudyWithTeacher: j['shareStudyWithTeacher'] as bool? ?? false,
        shareTestsWithTeacher: j['shareTestsWithTeacher'] as bool? ?? false,
      );

  static StudyDashboard dashboard(Map<String, dynamic> j) {
    final lastTest = j['lastTest'];
    return StudyDashboard(
      studentId: '${j['studentId']}',
      todayEffectiveMinutes: _int(j['todayEffectiveMinutes']),
      todayGoalMinutes: _int(j['todayGoalMinutes']),
      todayGoalMet: j['todayGoalMet'] as bool? ?? false,
      weekEffectiveMinutes: _int(j['weekEffectiveMinutes']),
      currentStreakDays: _int(j['currentStreakDays']),
      longestStreakDays: _int(j['longestStreakDays']),
      activeGoal: goal(j['activeGoal'] as Map<String, dynamic>?),
      lastTest: lastTest is Map<String, dynamic> ? test(lastTest) : null,
      recentSessions: (j['recentSessions'] as List<dynamic>? ?? <dynamic>[])
          .whereType<Map<String, dynamic>>()
          .map(session)
          .toList(),
      recentAchievements:
          (j['recentAchievements'] as List<dynamic>? ?? <dynamic>[])
              .whereType<Map<String, dynamic>>()
              .map(achievement)
              .toList(),
    );
  }
}
