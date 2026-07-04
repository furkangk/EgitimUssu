import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/parent/data/models/parent_models.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';

class ParentRepositoryImpl implements ParentRepository {
  ParentRepositoryImpl({required ApiClient apiClient, required AppConfig config})
    : _apiClient = apiClient,
      _config = config;

  final ApiClient _apiClient;
  final AppConfig _config;

  bool get _mock => _config.isMockFallbackEnabled('parent');

  @override
  Future<ParentProfile> getProfile(String userId) async {
    if (_mock) return _mockProfile(userId);
    try {
      final response = await _apiClient.get('/api/parents/profiles/$userId');
      return ParentMappers.profile(response);
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<ParentProfile> ensureProfile({
    required String userId,
    required String fullName,
    String? contactPhone,
    String? contactEmail,
  }) async {
    if (_mock) return _mockProfile(userId, fullName: fullName);
    try {
      final response = await _apiClient.post(
        '/api/parents/profiles',
        data: <String, dynamic>{
          'userId': userId,
          'fullName': fullName,
          'contactPhone': contactPhone,
          'contactEmail': contactEmail,
        },
      );
      return ParentMappers.profile(response);
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<ParentProfile> updateNotificationPreferences({
    required String parentUserId,
    required ParentNotificationPreferences preferences,
  }) async {
    if (_mock) {
      return _mockProfile(parentUserId).copyWithPrefs(preferences);
    }
    try {
      final response = await _apiClient.put(
        '/api/parents/$parentUserId/notification-preferences',
        data: <String, dynamic>{
          'missedAssignment': preferences.missedAssignment,
          'weeklyProgressSummary': preferences.weeklyProgressSummary,
          'lessonReminders': preferences.lessonReminders,
          'testResults': preferences.testResults,
          'payments': preferences.payments,
          'channel': preferences.channel,
        },
      );
      return ParentMappers.profile(response);
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<List<ChildLink>> listChildren(String parentUserId) async {
    if (_mock) return _mockChildren();
    try {
      final response = await _apiClient.getList(
        '/api/parents/$parentUserId/children',
      );
      return response
          .whereType<Map<String, dynamic>>()
          .map(ParentMappers.childLink)
          .toList();
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<ChildLink> requestChildLink({
    required String parentUserId,
    required String studentId,
    String? relationship,
    String? childDisplayName,
    bool isPrimaryContact = true,
  }) async {
    if (_mock) {
      return ChildLink(
        id: 'mock-link-${DateTime.now().millisecondsSinceEpoch}',
        parentUserId: parentUserId,
        studentId: studentId,
        childDisplayName: childDisplayName,
        relationship: relationship,
        status: 'Pending',
        isPrimaryContact: isPrimaryContact,
        requestedOnUtc: DateTime.now().toUtc(),
      );
    }
    try {
      final response = await _apiClient.post(
        '/api/parents/children/link',
        data: <String, dynamic>{
          'parentUserId': parentUserId,
          'studentId': studentId,
          'relationship': relationship,
          'childDisplayName': childDisplayName,
          'inviteCode': null,
          'isPrimaryContact': isPrimaryContact,
        },
      );
      return ParentMappers.childLink(response);
    } on ApiException {
      rethrow;
    }
  }

  @override
  Future<ChildDashboard> getChildDashboard({
    required String parentUserId,
    required String studentId,
  }) async {
    if (_mock) return _mockDashboard(studentId);
    try {
      final response = await _apiClient.get(
        '/api/parents/$parentUserId/children/$studentId/dashboard',
      );
      return ParentMappers.dashboard(response);
    } on ApiException {
      rethrow;
    }
  }

  // --------------------------------------------------------------------------
  // Mock veri (dev'de backend hazır olmadan veli panelini gösterebilmek için)
  // --------------------------------------------------------------------------

  ParentProfile _mockProfile(String userId, {String? fullName}) {
    return ParentProfile(
      id: 'mock-parent-profile',
      userId: userId,
      fullName: fullName ?? 'Demo Veli',
      contactPhone: '0555 111 22 33',
      contactEmail: 'veli@example.com',
      isActive: true,
      preferences: ParentNotificationPreferences.fallback,
    );
  }

  List<ChildLink> _mockChildren() {
    final now = DateTime.now().toUtc();
    return <ChildLink>[
      ChildLink(
        id: 'mock-link-1',
        parentUserId: 'mock-parent-user',
        studentId: 'mock-student-1',
        childDisplayName: 'Elif Yılmaz',
        relationship: 'Anne',
        status: 'Approved',
        isPrimaryContact: true,
        requestedOnUtc: now.subtract(const Duration(days: 20)),
        linkedOnUtc: now.subtract(const Duration(days: 19)),
        progress: ChildProgressSummary(
          completedLessonCount: 12,
          openAssignmentCount: 2,
          weeklyStudyMinutes: 320,
          lastLessonCompletedAtUtc: now.subtract(const Duration(days: 1)),
        ),
      ),
      ChildLink(
        id: 'mock-link-2',
        parentUserId: 'mock-parent-user',
        studentId: 'mock-student-2',
        childDisplayName: 'Kaan Yılmaz',
        relationship: 'Anne',
        status: 'Pending',
        isPrimaryContact: false,
        requestedOnUtc: now.subtract(const Duration(days: 2)),
      ),
    ];
  }

  ChildDashboard _mockDashboard(String studentId) {
    final now = DateTime.now().toUtc();
    final isSecond = studentId == 'mock-student-2';
    return ChildDashboard(
      studentId: studentId,
      childDisplayName: isSecond ? 'Kaan Yılmaz' : 'Elif Yılmaz',
      linkStatus: 'Approved',
      updatedOnUtc: now,
      study: StudySummary(
        weeklyStudyMinutes: isSecond ? 140 : 320,
        streakDays: isSecond ? 3 : 9,
        hasData: true,
        weeklyBreakdownMinutes: isSecond
            ? const <int>[20, 30, 0, 25, 15, 30, 20]
            : const <int>[45, 60, 30, 75, 20, 50, 40],
      ),
      lessons: LessonSummary(
        completedLessonCount: isSecond ? 4 : 12,
        plannedLessonCount: isSecond ? 6 : 14,
        lastLessonCompletedAtUtc: now.subtract(const Duration(days: 1)),
      ),
      assignments: AssignmentSummary(
        totalCount: isSecond ? 8 : 18,
        openCount: isSecond ? 3 : 2,
        completedCount: isSecond ? 5 : 16,
      ),
      payments: PaymentSummary(
        currency: 'TRY',
        expectedTotal: isSecond ? 2000 : 4500,
        collectedTotal: isSecond ? 1000 : 4000,
        outstandingTotal: isSecond ? 1000 : 500,
        lastUpdatedAtUtc: now.subtract(const Duration(days: 3)),
      ),
    );
  }
}

extension _ProfilePrefsCopy on ParentProfile {
  ParentProfile copyWithPrefs(ParentNotificationPreferences prefs) {
    return ParentProfile(
      id: id,
      userId: userId,
      fullName: fullName,
      contactPhone: contactPhone,
      contactEmail: contactEmail,
      isActive: isActive,
      preferences: prefs,
    );
  }
}
