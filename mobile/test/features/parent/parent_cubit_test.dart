import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_state.dart';
import 'package:flutter_test/flutter_test.dart';

/// P13 Task 6 / D-21: Veli panelinin durum makinesi.
///
/// Buradaki asıl kural **onaylanmamış bağın veri göstermemesidir**: veli, çocuğunu
/// kendiliğinden ekleyip verisine erişemez. Cubit bunu "yalnızca onaylı çocuk seçilir"
/// kuralıyla uygular; onaysız bağ varken panel açılmaz.
void main() {
  group('ParentCubit.load', () {
    test('yalnızca onaylı çocuğu seçer ve panelini yükler', () async {
      final repository = _FakeParentRepository(
        children: <ChildLink>[
          _child('bekleyen', status: 'Pending'),
          _child('onayli', status: 'Approved'),
        ],
      );
      final cubit = ParentCubit(repository);

      await cubit.load('veli-1', fullName: 'Ayşe Yılmaz');

      expect(cubit.state.status, ParentStatus.loaded);
      expect(cubit.state.selectedStudentId, 'onayli');
      expect(cubit.state.dashboard?.studentId, 'onayli');
      expect(repository.dashboardRequests, <String>['onayli']);
      await cubit.close();
    });

    test('onaylı çocuk yoksa panel açılmaz', () async {
      final repository = _FakeParentRepository(
        children: <ChildLink>[_child('bekleyen', status: 'Pending')],
      );
      final cubit = ParentCubit(repository);

      await cubit.load('veli-1');

      expect(cubit.state.status, ParentStatus.loaded);
      expect(cubit.state.selectedStudentId, isNull);
      expect(cubit.state.dashboard, isNull);
      // Onaysız bağ için panel isteği hiç gitmemeli.
      expect(repository.dashboardRequests, isEmpty);
      await cubit.close();
    });

    test('ad boşsa profil "Veli" adıyla açılır', () async {
      final repository = _FakeParentRepository(children: <ChildLink>[]);
      final cubit = ParentCubit(repository);

      await cubit.load('veli-1', fullName: '   ');

      expect(repository.ensuredFullName, 'Veli');
      await cubit.close();
    });

    test('API hatası kullanıcıya gösterilir, durum error olur', () async {
      final repository = _FakeParentRepository(
        children: <ChildLink>[],
        failure: const ApiException(message: 'Veli profili açılamadı.'),
      );
      final cubit = ParentCubit(repository);

      await cubit.load('veli-1');

      expect(cubit.state.status, ParentStatus.error);
      expect(cubit.state.errorMessage, 'Veli profili açılamadı.');
      await cubit.close();
    });
  });

  group('ParentState', () {
    test('approvedChildren yalnızca onaylı bağları döndürür', () {
      final state = ParentState(
        children: <ChildLink>[
          _child('a', status: 'Approved'),
          _child('b', status: 'Pending'),
          _child('c', status: 'Revoked'),
        ],
      );

      expect(state.approvedChildren.map((c) => c.studentId), <String>['a']);
    });

    test('adı olmayan çocuk "Öğrenci" olarak görünür', () {
      expect(_child('a', status: 'Approved').displayName, 'Öğrenci');
      expect(
        _child('a', status: 'Approved', name: 'Zeynep').displayName,
        'Zeynep',
      );
    });
  });
}

ChildLink _child(String studentId, {required String status, String? name}) => ChildLink(
  id: 'link-$studentId',
  parentUserId: 'veli-1',
  studentId: studentId,
  status: status,
  isPrimaryContact: false,
  requestedOnUtc: DateTime.utc(2026, 9, 1),
  childDisplayName: name,
);

class _FakeParentRepository implements ParentRepository {
  _FakeParentRepository({required this.children, this.failure});

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
  Future<ParentProfile> getProfile(String userId) async => _profile(userId, 'Veli');

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
      study: const StudySummary(weeklyStudyMinutes: 120, streakDays: 3, hasData: true),
      lessons: const LessonSummary(completedLessonCount: 4, plannedLessonCount: 6),
      assignments: const AssignmentSummary(totalCount: 5, openCount: 2, completedCount: 3),
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
