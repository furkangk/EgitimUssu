import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/features/study/data/models/study_models.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';

class StudyRepositoryImpl implements StudyRepository {
  StudyRepositoryImpl({
    required ApiClient apiClient,
    required AppConfig config,
  })  : _apiClient = apiClient,
        _config = config;

  final ApiClient _apiClient;
  final AppConfig _config;

  static const _base = '/api/study';

  bool get _mock => _config.isMockFallbackEnabled('study');

  // ---- Öğrenci ders notları ----

  final List<StudyNote> _mockNotes = <StudyNote>[];

  @override
  Future<List<StudyNote>> listNotes(String studentId) async {
    if (_mock) return List<StudyNote>.unmodifiable(_mockNotes);
    final r = await _apiClient.getList('$_base/students/$studentId/notes');
    return r.whereType<Map<String, dynamic>>().map(StudyMappers.note).toList();
  }

  @override
  Future<StudyNote> createNote(
    String studentId, {
    required String title,
    required String body,
    String? subject,
    String? topic,
  }) async {
    if (_mock) {
      final note = StudyNote(
        id: 'mock-note-${_now.microsecondsSinceEpoch}',
        title: title,
        body: body,
        subject: subject,
        topic: topic,
        updatedOnUtc: _now,
      );
      _mockNotes.insert(0, note);
      return note;
    }
    final r = await _apiClient.post(
      '$_base/students/$studentId/notes',
      data: <String, dynamic>{
        'title': title,
        'body': body,
        'subject': subject,
        'topic': topic,
        'attachmentUrl': null,
      },
    );
    return StudyMappers.note(r);
  }

  @override
  Future<StudyNote> updateNote(
    String noteId, {
    required String title,
    required String body,
    String? subject,
    String? topic,
  }) async {
    if (_mock) {
      final note = StudyNote(
        id: noteId,
        title: title,
        body: body,
        subject: subject,
        topic: topic,
        updatedOnUtc: _now,
      );
      final idx = _mockNotes.indexWhere((n) => n.id == noteId);
      if (idx >= 0) _mockNotes[idx] = note;
      return note;
    }
    final r = await _apiClient.put(
      '$_base/notes/$noteId',
      data: <String, dynamic>{
        'title': title,
        'body': body,
        'subject': subject,
        'topic': topic,
        'attachmentUrl': null,
      },
    );
    return StudyMappers.note(r);
  }

  @override
  Future<void> deleteNote(String noteId) async {
    if (_mock) {
      _mockNotes.removeWhere((n) => n.id == noteId);
      return;
    }
    await _apiClient.delete('$_base/notes/$noteId');
  }

  // ---- Ders/konu kataloğu ----

  final List<SubjectCatalog> _mockCatalog = <SubjectCatalog>[];

  @override
  Future<List<SubjectCatalog>> listSubjects(String studentId) async {
    if (_mock) return List<SubjectCatalog>.unmodifiable(_mockCatalog);
    final r = await _apiClient.getList('$_base/students/$studentId/subjects');
    return r
        .whereType<Map<String, dynamic>>()
        .map(StudyMappers.subjectCatalog)
        .toList();
  }

  @override
  Future<SubjectCatalog> createSubject(
    String studentId, {
    required String name,
    String? colorHex,
  }) async {
    if (_mock) {
      final subject = SubjectCatalog(
        id: 'mock-subject-${_now.microsecondsSinceEpoch}',
        studentId: studentId,
        name: name,
        colorHex: colorHex,
        topics: const <TopicCatalog>[],
      );
      _mockCatalog.add(subject);
      return subject;
    }
    final r = await _apiClient.post(
      '$_base/students/$studentId/subjects',
      data: <String, dynamic>{'name': name, 'colorHex': colorHex},
    );
    return StudyMappers.subjectCatalog(r);
  }

  @override
  Future<SubjectCatalog> updateSubject(
    String subjectId, {
    required String name,
    String? colorHex,
    required bool isActive,
  }) async {
    if (_mock) {
      final idx = _mockCatalog.indexWhere((s) => s.id == subjectId);
      final old = _mockCatalog[idx];
      final updated = SubjectCatalog(
        id: old.id,
        studentId: old.studentId,
        name: name,
        colorHex: colorHex,
        isActive: isActive,
        topics: old.topics,
      );
      _mockCatalog[idx] = updated;
      return updated;
    }
    final r = await _apiClient.put(
      '$_base/subjects/$subjectId',
      data: <String, dynamic>{
        'name': name,
        'colorHex': colorHex,
        'isActive': isActive,
      },
    );
    return StudyMappers.subjectCatalog(r);
  }

  @override
  Future<void> deleteSubject(String subjectId) async {
    if (_mock) {
      _mockCatalog.removeWhere((s) => s.id == subjectId);
      return;
    }
    await _apiClient.delete('$_base/subjects/$subjectId');
  }

  @override
  Future<TopicCatalog> addTopic(String subjectId, {required String name}) async {
    if (_mock) {
      final idx = _mockCatalog.indexWhere((s) => s.id == subjectId);
      final old = _mockCatalog[idx];
      final topic = TopicCatalog(
        id: 'mock-topic-${_now.microsecondsSinceEpoch}',
        subjectId: subjectId,
        name: name,
        orderIndex: old.topics.length,
      );
      _mockCatalog[idx] = SubjectCatalog(
        id: old.id,
        studentId: old.studentId,
        name: old.name,
        colorHex: old.colorHex,
        isActive: old.isActive,
        topics: <TopicCatalog>[...old.topics, topic],
      );
      return topic;
    }
    final r = await _apiClient.post(
      '$_base/subjects/$subjectId/topics',
      data: <String, dynamic>{'name': name},
    );
    return StudyMappers.topicCatalog(r);
  }

  @override
  Future<TopicCatalog> updateTopic(
    String topicId, {
    required String name,
    required int orderIndex,
    required bool isActive,
  }) async {
    if (_mock) {
      return TopicCatalog(
        id: topicId,
        subjectId: 'mock',
        name: name,
        orderIndex: orderIndex,
        isActive: isActive,
      );
    }
    final r = await _apiClient.put(
      '$_base/topics/$topicId',
      data: <String, dynamic>{
        'name': name,
        'orderIndex': orderIndex,
        'isActive': isActive,
      },
    );
    return StudyMappers.topicCatalog(r);
  }

  @override
  Future<void> deleteTopic(String topicId) async {
    if (_mock) {
      for (var i = 0; i < _mockCatalog.length; i++) {
        final s = _mockCatalog[i];
        if (s.topics.any((t) => t.id == topicId)) {
          _mockCatalog[i] = SubjectCatalog(
            id: s.id,
            studentId: s.studentId,
            name: s.name,
            colorHex: s.colorHex,
            isActive: s.isActive,
            topics: s.topics.where((t) => t.id != topicId).toList(),
          );
        }
      }
      return;
    }
    await _apiClient.delete('$_base/topics/$topicId');
  }

  @override
  Future<StudyDashboard> getDashboard(String studentId) async {
    if (_mock) return _mockDashboard(studentId);
    final r = await _apiClient.get('$_base/students/$studentId/dashboard');
    return StudyMappers.dashboard(r);
  }

  @override
  Future<StudyStreak> getStreak(String studentId) async {
    if (_mock) return _mockStreak();
    final r = await _apiClient.get('$_base/students/$studentId/streak');
    return StudyMappers.streak(r);
  }

  @override
  Future<StudyGoal?> getGoals(String studentId) async {
    if (_mock) return _mockGoal();
    final r = await _apiClient.get('$_base/students/$studentId/goals');
    if (r.isEmpty || r['dailyGoalMinutes'] == null) {
      return null;
    }
    return StudyMappers.goal(r);
  }

  @override
  Future<StudyGoal> updateGoals(
    String studentId, {
    required int dailyGoalMinutes,
    int? weeklyGoalMinutes,
    double? targetNet,
    String? subject,
  }) async {
    if (_mock) {
      return StudyGoal(
        dailyGoalMinutes: dailyGoalMinutes,
        weeklyGoalMinutes: weeklyGoalMinutes,
        targetNet: targetNet,
        subject: subject,
      );
    }
    final r = await _apiClient.put(
      '$_base/students/$studentId/goals',
      data: <String, dynamic>{
        'dailyGoalMinutes': dailyGoalMinutes,
        'weeklyGoalMinutes': weeklyGoalMinutes,
        'targetNet': targetNet,
        'targetScore': null,
        'subject': subject,
      },
    );
    return StudyMappers.goal(r)!;
  }

  @override
  Future<List<StudySession>> listSessions(String studentId) async {
    if (_mock) return _mockSessions(studentId);
    final r = await _apiClient.getList('$_base/students/$studentId/sessions');
    return r
        .whereType<Map<String, dynamic>>()
        .map(StudyMappers.session)
        .toList();
  }

  @override
  Future<StudySession> startSession(
    String studentId, {
    required String subject,
    String? topic,
  }) async {
    if (_mock) {
      return _mockSession(
        studentId: studentId,
        subject: subject,
        topic: topic,
        status: 'Running',
        source: 'Stopwatch',
        effectiveMinutes: 0,
      );
    }
    final r = await _apiClient.post(
      '$_base/sessions/start',
      data: <String, dynamic>{
        'studentId': studentId,
        'subject': subject,
        'topic': topic,
      },
    );
    return StudyMappers.session(r);
  }

  @override
  Future<StudySession> pauseSession(String sessionId) async {
    if (_mock) return _mockSession(id: sessionId, status: 'Paused');
    return StudyMappers.session(
        await _apiClient.post('$_base/sessions/$sessionId/pause'));
  }

  @override
  Future<StudySession> resumeSession(String sessionId) async {
    if (_mock) return _mockSession(id: sessionId, status: 'Running');
    return StudyMappers.session(
        await _apiClient.post('$_base/sessions/$sessionId/resume'));
  }

  @override
  Future<StudySession> completeSession(
    String sessionId, {
    String? personalNote,
  }) async {
    if (_mock) {
      return _mockSession(
        id: sessionId,
        status: 'Completed',
        effectiveMinutes: 45,
        personalNote: personalNote,
        endedAtUtc: DateTime.now().toUtc(),
      );
    }
    final r = await _apiClient.post(
      '$_base/sessions/$sessionId/complete',
      data: <String, dynamic>{'personalNote': personalNote},
    );
    return StudyMappers.session(r);
  }

  @override
  Future<void> discardSession(String sessionId) async {
    if (_mock) return;
    await _apiClient.post('$_base/sessions/$sessionId/discard');
  }

  @override
  Future<StudySession> createManualSession(
    String studentId, {
    required String subject,
    String? topic,
    required int effectiveMinutes,
    required DateTime studiedOnUtc,
    String? personalNote,
  }) async {
    if (_mock) {
      return _mockSession(
        studentId: studentId,
        subject: subject,
        topic: topic,
        status: 'Completed',
        source: 'Manual',
        effectiveMinutes: effectiveMinutes,
        startedAtUtc: studiedOnUtc.toUtc(),
        endedAtUtc:
            studiedOnUtc.toUtc().add(Duration(minutes: effectiveMinutes)),
        personalNote: personalNote,
      );
    }
    final r = await _apiClient.post(
      '$_base/sessions/manual',
      data: <String, dynamic>{
        'studentId': studentId,
        'subject': subject,
        'topic': topic,
        'effectiveMinutes': effectiveMinutes,
        'studiedOnUtc': studiedOnUtc.toUtc().toIso8601String(),
        'personalNote': personalNote,
      },
    );
    return StudyMappers.session(r);
  }

  @override
  Future<List<TestResult>> listTests(String studentId) async {
    if (_mock) return _mockTests();
    final r = await _apiClient.getList('$_base/students/$studentId/test-results');
    return r.whereType<Map<String, dynamic>>().map(StudyMappers.test).toList();
  }

  @override
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
  }) async {
    if (_mock) {
      final divisor = (penaltyDivisor == null || penaltyDivisor <= 0)
          ? 4
          : penaltyDivisor;
      return TestResult(
        id: 'mock-test-${DateTime.now().microsecondsSinceEpoch}',
        subject: subject,
        topic: topic,
        testName: testName,
        testType: testType,
        totalQuestions: totalQuestions,
        correct: correct,
        wrong: wrong,
        blank: blank,
        net: correct - wrong / divisor,
        durationMinutes: durationMinutes,
        takenOnUtc: takenOnUtc.toUtc(),
      );
    }
    final r = await _apiClient.post(
      '$_base/test-results',
      data: <String, dynamic>{
        'studentId': studentId,
        'subject': subject,
        'topic': topic,
        'testType': testType,
        'testName': testName,
        'totalQuestions': totalQuestions,
        'correct': correct,
        'wrong': wrong,
        'blank': blank,
        'penaltyDivisor': penaltyDivisor,
        'durationMinutes': durationMinutes,
        'takenOnUtc': takenOnUtc.toUtc().toIso8601String(),
      },
    );
    return StudyMappers.test(r);
  }

  @override
  Future<List<StudyAchievement>> getAchievements(String studentId) async {
    if (_mock) return _mockAchievements();
    final r = await _apiClient.getList('$_base/students/$studentId/achievements');
    return r
        .whereType<Map<String, dynamic>>()
        .map(StudyMappers.achievement)
        .toList();
  }

  @override
  Future<WeeklySummary> getWeeklySummary(String studentId) async {
    if (_mock) return _mockWeekly();
    final r = await _apiClient.get('$_base/students/$studentId/weekly-summary');
    return StudyMappers.weekly(r);
  }

  @override
  Future<StudySharing> getSharing(String studentId) async {
    if (_mock) return _mockSharing;
    final r = await _apiClient.get('$_base/students/$studentId/sharing');
    return StudyMappers.sharing(r);
  }

  @override
  Future<StudySharing> updateSharing(
    String studentId, {
    required bool shareStudyWithParent,
    required bool shareTestsWithParent,
    required bool shareStudyWithTeacher,
    required bool shareTestsWithTeacher,
  }) async {
    if (_mock) {
      return StudySharing(
        shareStudyWithParent: shareStudyWithParent,
        shareTestsWithParent: shareTestsWithParent,
        shareStudyWithTeacher: shareStudyWithTeacher,
        shareTestsWithTeacher: shareTestsWithTeacher,
      );
    }
    final r = await _apiClient.put(
      '$_base/students/$studentId/sharing',
      data: <String, dynamic>{
        'shareStudyWithParent': shareStudyWithParent,
        'shareTestsWithParent': shareTestsWithParent,
        'shareStudyWithTeacher': shareStudyWithTeacher,
        'shareTestsWithTeacher': shareTestsWithTeacher,
      },
    );
    return StudyMappers.sharing(r);
  }

  // ---------------------------------------------------------------------------
  // Mock veri — backend erişilemezken (development) çalışma panosunu besler.
  // ---------------------------------------------------------------------------

  DateTime get _now => DateTime.now().toUtc();

  StudyDashboard _mockDashboard(String studentId) {
    return StudyDashboard(
      studentId: studentId,
      todayEffectiveMinutes: 95,
      todayGoalMinutes: 120,
      todayGoalMet: false,
      weekEffectiveMinutes: 540,
      currentStreakDays: 4,
      longestStreakDays: 11,
      activeGoal: _mockGoal(),
      lastTest: _mockTests().first,
      recentSessions: _mockSessions(studentId).take(3).toList(),
      recentAchievements: _mockAchievements().where((a) => a.earned).toList(),
    );
  }

  StudyStreak _mockStreak() => const StudyStreak(
        currentStreakDays: 4,
        longestStreakDays: 11,
        totalStudyDays: 37,
        studiedToday: true,
        todayEffectiveMinutes: 95,
      );

  StudyGoal _mockGoal() => const StudyGoal(
        dailyGoalMinutes: 120,
        weeklyGoalMinutes: 720,
        targetNet: 85,
        subject: null,
      );

  List<StudySession> _mockSessions(String studentId) {
    final today = DateTime.utc(_now.year, _now.month, _now.day);
    return <StudySession>[
      StudySession(
        id: 'mock-session-1',
        studentId: studentId,
        subject: 'Matematik',
        topic: 'Türev',
        status: 'Completed',
        source: 'Stopwatch',
        effectiveMinutes: 55,
        breakMinutes: 10,
        startedAtUtc: today.add(const Duration(hours: 9)),
        endedAtUtc: today.add(const Duration(hours: 10, minutes: 5)),
      ),
      StudySession(
        id: 'mock-session-2',
        studentId: studentId,
        subject: 'Fizik',
        topic: 'Optik',
        status: 'Completed',
        source: 'Manual',
        effectiveMinutes: 40,
        breakMinutes: 0,
        startedAtUtc: today.subtract(const Duration(hours: 20)),
        endedAtUtc: today.subtract(const Duration(hours: 19, minutes: 20)),
      ),
      StudySession(
        id: 'mock-session-3',
        studentId: studentId,
        subject: 'Türkçe',
        topic: 'Paragraf',
        status: 'Completed',
        source: 'Stopwatch',
        effectiveMinutes: 30,
        breakMinutes: 5,
        startedAtUtc: today.subtract(const Duration(days: 1, hours: 4)),
        endedAtUtc:
            today.subtract(const Duration(days: 1, hours: 3, minutes: 25)),
      ),
    ];
  }

  StudySession _mockSession({
    String? id,
    String studentId = 'mock-student',
    String subject = 'Matematik',
    String? topic,
    required String status,
    String source = 'Stopwatch',
    int effectiveMinutes = 0,
    int breakMinutes = 0,
    DateTime? startedAtUtc,
    DateTime? endedAtUtc,
    String? personalNote,
  }) {
    return StudySession(
      id: id ?? 'mock-session-${_now.microsecondsSinceEpoch}',
      studentId: studentId,
      subject: subject,
      topic: topic,
      status: status,
      source: source,
      effectiveMinutes: effectiveMinutes,
      breakMinutes: breakMinutes,
      startedAtUtc: startedAtUtc ?? _now,
      endedAtUtc: endedAtUtc,
      personalNote: personalNote,
    );
  }

  List<TestResult> _mockTests() {
    return <TestResult>[
      TestResult(
        id: 'mock-test-1',
        subject: 'Matematik',
        topic: 'TYT Deneme',
        testName: 'TYT Genel Deneme 3',
        testType: 'Deneme',
        totalQuestions: 40,
        correct: 32,
        wrong: 4,
        blank: 4,
        net: 31,
        durationMinutes: 60,
        takenOnUtc: _now.subtract(const Duration(days: 2)),
      ),
      TestResult(
        id: 'mock-test-2',
        subject: 'Fizik',
        topic: 'Optik',
        testName: 'Konu Testi',
        testType: 'KonuTesti',
        totalQuestions: 20,
        correct: 15,
        wrong: 3,
        blank: 2,
        net: 14.25,
        durationMinutes: 30,
        takenOnUtc: _now.subtract(const Duration(days: 5)),
      ),
    ];
  }

  List<StudyAchievement> _mockAchievements() {
    return <StudyAchievement>[
      StudyAchievement(
        code: 'first_session',
        title: 'İlk Adım',
        description: 'İlk çalışma seansını tamamla',
        category: 'Başlangıç',
        threshold: 1,
        earned: true,
        currentValue: 1,
        iconKey: 'flag',
        earnedOnUtc: _now.subtract(const Duration(days: 20)),
      ),
      StudyAchievement(
        code: 'streak_7',
        title: '7 Gün Seri',
        description: '7 gün üst üste çalış',
        category: 'Seri',
        threshold: 7,
        earned: false,
        currentValue: 4,
        iconKey: 'fire',
      ),
      StudyAchievement(
        code: 'total_1000',
        title: '1000 Dakika',
        description: 'Toplam 1000 dakika çalış',
        category: 'Süre',
        threshold: 1000,
        earned: false,
        currentValue: 540,
        iconKey: 'clock',
      ),
    ];
  }

  WeeklySummary _mockWeekly() {
    final today = DateTime.utc(_now.year, _now.month, _now.day);
    final weekStart = today.subtract(Duration(days: today.weekday - 1));
    return WeeklySummary(
      weekStartDate: weekStart,
      totalEffectiveMinutes: 540,
      totalBreakMinutes: 45,
      sessionCount: 9,
      perSubject: const <SubjectMinutes>[
        SubjectMinutes(
            subject: 'Matematik', effectiveMinutes: 240, sessionCount: 4),
        SubjectMinutes(subject: 'Fizik', effectiveMinutes: 180, sessionCount: 3),
        SubjectMinutes(subject: 'Türkçe', effectiveMinutes: 120, sessionCount: 2),
      ],
      perDay: <DayMinutes>[
        for (var i = 0; i < 7; i++)
          DayMinutes(
            date: weekStart.add(Duration(days: i)),
            effectiveMinutes: const <int>[90, 60, 120, 0, 95, 75, 100][i],
            sessionCount: const <int>[2, 1, 2, 0, 1, 1, 2][i],
          ),
      ],
    );
  }

  static const StudySharing _mockSharing = StudySharing(
    shareStudyWithParent: true,
    shareTestsWithParent: true,
    shareStudyWithTeacher: true,
    shareTestsWithTeacher: false,
  );
}
