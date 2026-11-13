import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/progress/domain/progress_contracts.dart';

/// Ortak `ProgressRepository` sahtesi (mobile_flutter.md §17.1 — sahteler tek noktada).
class FakeProgressRepository implements ProgressRepository {
  FakeProgressRepository({
    this.overview,
    this.mastery = const <TopicMastery>[],
    this.snapshots = const <ProgressSnapshot>[],
    this.error,
  });

  final ProgressOverview? overview;
  final List<TopicMastery> mastery;
  final List<ProgressSnapshot> snapshots;
  final ApiException? error;

  final List<String> overviewCalls = <String>[];
  final List<String> snapshotCalls = <String>[];

  static const ProgressOverview empty = ProgressOverview(
    masteredCount: 0,
    proficientCount: 0,
    developingCount: 0,
    weakCount: 0,
    notStartedCount: 0,
    activeGoalCount: 0,
    weakSpots: <TopicMastery>[],
    strengths: <TopicMastery>[],
  );

  @override
  Future<ProgressOverview> getOverview(String studentId) async {
    overviewCalls.add('self:$studentId');
    if (error != null) throw error!;
    return overview ?? empty;
  }

  @override
  Future<ProgressOverview> getTeacherStudentOverview({
    required String teacherUserId,
    required String studentId,
  }) async {
    overviewCalls.add('teacher:$teacherUserId:$studentId');
    if (error != null) throw error!;
    return overview ?? empty;
  }

  @override
  Future<ProgressOverview> getParentChildOverview({
    required String parentUserId,
    required String studentId,
  }) async {
    overviewCalls.add('parent:$parentUserId:$studentId');
    if (error != null) throw error!;
    return overview ?? empty;
  }

  @override
  Future<List<TopicMastery>> listMastery(
    String studentId, {
    String? subject,
  }) async {
    if (error != null) throw error!;
    return mastery;
  }

  @override
  Future<List<ProgressSnapshot>> listSnapshots(
    String studentId, {
    String? subject,
    String? period,
    DateTime? from,
    DateTime? to,
  }) async {
    snapshotCalls.add(studentId);
    if (error != null) throw error!;
    return snapshots;
  }
}
