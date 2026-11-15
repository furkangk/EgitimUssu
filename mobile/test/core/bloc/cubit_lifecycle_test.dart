import 'dart:async';
import 'dart:typed_data';

import 'package:egitim_ussu_mobile/core/bloc/safe_emit.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/lesson_resources_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/email_verification_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/password_reset_cubit.dart';
import 'package:egitim_ussu_mobile/features/progress/domain/progress_contracts.dart';
import 'package:egitim_ussu_mobile/features/progress/presentation/cubit/progress_cubit.dart';
import 'package:egitim_ussu_mobile/features/reporting/domain/reporting_contracts.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/student_report_cubit.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/cubit/teacher_reports_cubit.dart';
import 'package:egitim_ussu_mobile/features/settings/domain/settings_contracts.dart';
import 'package:egitim_ussu_mobile/features/settings/presentation/cubit/settings_cubit.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';

import '../../helpers/fake_auth_repository.dart';
import '../../helpers/fake_progress_repository.dart';
import '../../helpers/fake_reporting_repository.dart';

void main() {
  test('safeEmit publishes while open and ignores after close', () async {
    final cubit = _Counter();
    cubit.safeEmit(1);
    expect(cubit.state, 1);
    await cubit.close();
    cubit.safeEmit(2);
    expect(cubit.state, 1);
  });
  for (final failure in [false, true]) {
    for (final feature in [
      'passwordRequest',
      'passwordConfirm',
      'emailRequest',
      'emailConfirm',
      'studentReport',
      'teacherReports',
      'progress',
      'resources',
      'settings',
    ]) {
      test('$feature late response failure=$failure after close', () async {
        final gate = Completer<void>();
        final auth = _Auth(gate.future);
        final reports = _Reports(gate.future);
        late Cubit<Object?> cubit;
        late Future<void> operation;
        switch (feature) {
          case 'passwordRequest':
            final c = PasswordResetCubit(auth);
            cubit = c;
            operation = c.requestReset('test@example.com');
          case 'passwordConfirm':
            final c = PasswordResetCubit(auth);
            cubit = c;
            operation = c.confirmReset(
              email: 'test@example.com',
              token: 'code',
              newPassword: 'secret',
            );
          case 'emailRequest':
            final c = EmailVerificationCubit(auth);
            cubit = c;
            operation = c.resendCode('test@example.com');
          case 'emailConfirm':
            final c = EmailVerificationCubit(auth);
            cubit = c;
            operation = c.verify(email: 'test@example.com', token: 'code');
          case 'studentReport':
            final c = StudentReportCubit(reports, FakeReportFileOpener());
            cubit = c;
            operation = c.load('student');
          case 'teacherReports':
            final c = TeacherReportsCubit(reports);
            cubit = c;
            operation = c.load('teacher');
          case 'progress':
            final c = ProgressCubit(_Progress(gate.future));
            cubit = c;
            operation = c.loadSelf('student');
          case 'resources':
            final c = LessonResourcesCubit(_Resources(gate.future));
            cubit = c;
            operation = c.load(studentId: 'student');
          default:
            final c = SettingsCubit(_Settings(gate.future));
            cubit = c;
            operation = c.load('user');
        }
        final before = cubit.state;
        final assertion = expectLater(operation, completes);
        await cubit.close();
        if (failure) {
          gate.completeError(const ApiException(message: 'late error'));
        } else {
          gate.complete();
        }
        await assertion;
        expect(cubit.state, same(before));
      });
    }
  }
  test('leaving report while download pending never opens a file', () async {
    final gate = Completer<void>();
    final repo = _Download(gate.future);
    final opener = FakeReportFileOpener();
    final cubit = StudentReportCubit(repo, opener);
    await cubit.load('student');
    final pending = cubit.generateAndOpenReport();
    await repo.started.future;
    await cubit.close();
    final assertion = expectLater(pending, completes);
    gate.complete();
    await assertion;
    expect(opener.openedFileNames, isEmpty);
  });
}

class _Counter extends Cubit<int> with SafeEmit<int> {
  _Counter() : super(0);
}

class _Auth extends FakeAuthRepository {
  _Auth(this.gate);
  final Future<void> gate;
  @override
  Future<void> requestPasswordReset({required String email}) => gate;
  @override
  Future<void> confirmPasswordReset({
    required String email,
    required String token,
    required String newPassword,
  }) => gate;
  @override
  Future<void> requestEmailVerification({required String email}) => gate;
  @override
  Future<void> confirmEmailVerification({
    required String email,
    required String token,
  }) => gate;
}

class _Reports extends FakeReportingRepository {
  _Reports(this.gate);
  final Future<void> gate;
  @override
  Future<List<StudentStudyWeek>> studentStudyAnalysis({
    required String studentId,
    DateTime? from,
    DateTime? to,
  }) async {
    await gate;
    return [];
  }

  @override
  Future<TeacherMonthlyReport> teacherMonthly({
    required String teacherUserId,
    int? year,
    int? month,
  }) async {
    await gate;
    return super.teacherMonthly(teacherUserId: teacherUserId);
  }
}

class _Download extends FakeReportingRepository {
  _Download(this.gate);
  final Future<void> gate;
  final started = Completer<void>();
  @override
  Future<Uint8List?> downloadReport(String reportId) async {
    started.complete();
    await gate;
    return Uint8List.fromList([37, 80, 68, 70]);
  }
}

class _Progress extends FakeProgressRepository {
  _Progress(this.gate);
  final Future<void> gate;
  @override
  Future<ProgressOverview> getOverview(String studentId) async {
    await gate;
    return FakeProgressRepository.empty;
  }
}

class _Resources implements AssignmentRepository {
  _Resources(this.gate);
  final Future<void> gate;
  @override
  Future<List<LessonResource>> listLessonResources({
    String? teacherUserId,
    String? studentId,
    String? lessonSessionId,
  }) async {
    await gate;
    return [];
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

class _Settings implements SettingsRepository {
  _Settings(this.gate);
  final Future<void> gate;
  @override
  Future<UserSettings> load(String userId) async {
    await gate;
    return UserSettings(
      userId: userId,
      pushNotificationsEnabled: true,
      emailNotificationsEnabled: true,
      upcomingLessonReminderEnabled: true,
      homeworkReminderEnabled: true,
      paymentReminderEnabled: true,
      weeklySummaryEnabled: true,
      shareStudyDataWithTeacher: false,
      shareStudyDataWithParent: false,
      privacyLevel: PrivacyLevel.standard,
      sessionTerminationPolicy: SessionTerminationPolicy.keepLatest,
    );
  }

  @override
  Future<UserSettings> save(String userId, UserSettings settings) async {
    await gate;
    return settings;
  }
}
