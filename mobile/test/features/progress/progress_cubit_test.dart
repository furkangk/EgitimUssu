import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/progress/domain/progress_contracts.dart';
import 'package:egitim_ussu_mobile/features/progress/presentation/cubit/progress_cubit.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_progress_repository.dart';

/// P08 Task 5 (M10): Gelişim ekranının durum makinesi.
/// Kritik ayrım — snapshot serisi **yalnız öğrencinin kendi ucunda** vardır
/// (`/students/{id}/snapshots` öğrenci + Admin ile sınırlı); öğretmen/veli
/// kapsamında seri boş kalır, ekran çizgi grafiğini gizler.
void main() {
  group('ProgressCubit', () {
    test('kendi gelisimi: ozet, konular ve snapshot serisi dolar', () async {
      final repository = FakeProgressRepository(
        overview: _overview(),
        mastery: <TopicMastery>[_mastery()],
        snapshots: <ProgressSnapshot>[_snapshot(8, 60), _snapshot(1, 70)],
      );
      final cubit = ProgressCubit(repository);

      await cubit.loadSelf('student-1');

      expect(cubit.state.status, ProgressStatus.loaded);
      expect(cubit.state.overview?.masteredCount, 3);
      expect(cubit.state.mastery, hasLength(1));
      expect(cubit.state.snapshots, hasLength(2));
      expect(cubit.state.scope, ProgressScope.self);
      expect(repository.snapshotCalls.single, 'student-1');
      await cubit.close();
    });

    test('snapshot serisi ders bazinda ve tarihe gore siralanir', () async {
      final repository = FakeProgressRepository(
        overview: _overview(),
        snapshots: <ProgressSnapshot>[
          _snapshot(8, 70, subject: 'Fizik'),
          _snapshot(1, 55),
          _snapshot(8, 62),
        ],
      );
      final cubit = ProgressCubit(repository);

      await cubit.loadSelf('student-1');

      final series = cubit.state.snapshotsBySubject;
      expect(series.keys, containsAll(<String>['Matematik', 'Fizik']));
      expect(
        series['Matematik']!.map((s) => s.masteryScore).toList(),
        <int>[55, 62],
      );
      await cubit.close();
    });

    test('hata failure durumuna dusurur', () async {
      final repository = FakeProgressRepository(
        error: const ApiException(message: 'Gelisim alinamadi.'),
      );
      final cubit = ProgressCubit(repository);

      await cubit.loadSelf('student-1');

      expect(cubit.state.status, ProgressStatus.failure);
      expect(cubit.state.errorMessage, 'Gelisim alinamadi.');
      await cubit.close();
    });

    test('ogretmen kapsami ogretmen ucunu cagirir, snapshot istemez', () async {
      final repository = FakeProgressRepository(overview: _overview());
      final cubit = ProgressCubit(repository);

      await cubit.loadForTeacher(
        teacherUserId: 'teacher-1',
        studentId: 'student-1',
      );

      expect(cubit.state.scope, ProgressScope.teacher);
      expect(repository.overviewCalls.single, 'teacher:teacher-1:student-1');
      expect(repository.snapshotCalls, isEmpty);
      expect(cubit.state.snapshots, isEmpty);
      await cubit.close();
    });

    test('veli kapsami veli ucunu cagirir', () async {
      final repository = FakeProgressRepository(overview: _overview());
      final cubit = ProgressCubit(repository);

      await cubit.loadForParent(
        parentUserId: 'parent-1',
        studentId: 'student-1',
      );

      expect(cubit.state.scope, ProgressScope.parent);
      expect(repository.overviewCalls.single, 'parent:parent-1:student-1');
      await cubit.close();
    });

    test('paylasim kapaliysa bos ozet gelir; hata degil bos durumdur', () async {
      final repository = FakeProgressRepository();
      final cubit = ProgressCubit(repository);

      await cubit.loadForParent(
        parentUserId: 'parent-1',
        studentId: 'student-1',
      );

      expect(cubit.state.status, ProgressStatus.loaded);
      expect(cubit.state.isEmpty, isTrue);
      await cubit.close();
    });
  });
}

ProgressOverview _overview() => const ProgressOverview(
      masteredCount: 3,
      proficientCount: 2,
      developingCount: 1,
      weakCount: 1,
      notStartedCount: 0,
      activeGoalCount: 2,
      weakSpots: <TopicMastery>[],
      strengths: <TopicMastery>[],
    );

TopicMastery _mastery() => const TopicMastery(
      subject: 'Matematik',
      topic: 'Türev',
      masteryLevel: 'Proficient',
      masteryScore: 72,
      totalStudyMinutes: 240,
      testAttemptCount: 4,
      trend: 'Improving',
      isWeakSpot: false,
      isStrength: true,
    );

ProgressSnapshot _snapshot(
  int month,
  int score, {
  String subject = 'Matematik',
  int year = 2026,
}) =>
    ProgressSnapshot(
      subject: subject,
      period: 'Monthly',
      periodStart: DateTime.utc(year, month, 1),
      masteryScore: score,
      studyMinutes: 300,
      testCount: 5,
      averageNet: 0.62,
      trend: 'Improving',
    );
