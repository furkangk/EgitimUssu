import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';

ChildLink _child(String studentId, {required String status, String? name}) =>
    ChildLink(
      id: 'link-$studentId',
      parentUserId: 'veli-1',
      studentId: studentId,
      status: status,
      isPrimaryContact: false,
      requestedOnUtc: DateTime.utc(2026, 9, 1),
      childDisplayName: name,
    );

class FakeParentRepository implements ParentRepository {
  FakeParentRepository({required this.children, this.failure});

  final List<ChildLink> children;
  final Object? failure;
  final List<String> dashboardRequests = <String>[];
  String? ensuredFullName;

  @override
  Future<ParentProfile> ensureProfile({
    required String userId,
    required String fullName,
    String? contactPhone,
    String? contactEmail,
  }) async {
    if (failure != null) throw failure!;
    ensuredFullName = fullName;
    return _profile(userId, fullName);
  }

  @override
  Future<ParentProfile> getProfile(String userId) async =>
      _profile(userId, 'Veli');

  @override
  Future<List<ChildLink>> listChildren(String parentUserId) async => children;

  @override
  Future<ChildDashboard> getChildDashboard({
    required String parentUserId,
    required String studentId,
  }) async {
    dashboardRequests.add(studentId);
    return ChildDashboard(
      studentId: studentId,
      linkStatus: 'Approved',
      study: const StudySummary(
        weeklyStudyMinutes: 120,
        streakDays: 3,
        hasData: true,
      ),
      lessons: const LessonSummary(
        completedLessonCount: 4,
        plannedLessonCount: 6,
      ),
      assignments: const AssignmentSummary(
        totalCount: 5,
        openCount: 2,
        completedCount: 3,
      ),
      payments: const PaymentSummary(
        currency: 'TRY',
        expectedTotal: 1500,
        collectedTotal: 1000,
        outstandingTotal: 500,
      ),
    );
  }

  @override
  Future<ChildLink> requestChildLink({
    required String parentUserId,
    required String studentId,
    String? relationship,
    String? childDisplayName,
    bool isPrimaryContact = false,
  }) async => _child(studentId, status: 'Pending');

  @override
  Future<ParentProfile> updateNotificationPreferences({
    required String parentUserId,
    required ParentNotificationPreferences preferences,
  }) async => _profile(parentUserId, 'Veli');

  ParentProfile _profile(String userId, String fullName) => ParentProfile(
    id: 'profil-1',
    userId: userId,
    fullName: fullName,
    preferences: const ParentNotificationPreferences(
      missedAssignment: true,
      weeklyProgressSummary: true,
      lessonReminders: true,
      testResults: true,
      payments: true,
      channel: 'Push',
    ),
    isActive: true,
  );
}
