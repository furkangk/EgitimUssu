import 'package:egitim_ussu_mobile/features/dashboard/domain/dashboard_contracts.dart';
import 'package:egitim_ussu_mobile/features/dashboard/presentation/cubit/dashboard_cubit.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('aggregates phase 1 counts and payment summary', () async {
    final now = DateTime.now().toUtc();
    final cubit = DashboardCubit(
      studentRepository: _FakeStudentRepository(),
      schedulingRepository: _FakeSchedulingRepository(now),
      paymentRepository: _FakePaymentRepository(),
      dashboardRepository: _FakeDashboardRepository(),
    );

    await cubit.load('teacher-1');

    expect(cubit.state.studentCount, 2);
    expect(cubit.state.todayLessonCount, 1);
    expect(cubit.state.upcomingLessonCount, 2);
    expect(cubit.state.outstandingAmount, 1250);
    expect(cubit.state.overdueAmount, 500);
    expect(cubit.state.overdueCount, 1);
    expect(cubit.state.outstandingCurrency, 'TRY');

    await cubit.close();
  });
}

class _FakeStudentRepository implements StudentRepository {
  @override
  Future<StudentProfile> createStudent(StudentProfile studentProfile) {
    throw UnimplementedError();
  }

  @override
  Future<StudentProfile> getStudent(String studentId) {
    throw UnimplementedError();
  }

  @override
  Future<StudentProfile> updateStudent(StudentProfile studentProfile) {
    throw UnimplementedError();
  }

  @override
  Future<StudentProfile?> getByUser(String userId) async => null;

  @override
  Future<StudentProfile> createSelfProfile({
    required String userId,
    required String fullName,
    required String gradeLevel,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<List<StudentProfile>> listByTeacher(String teacherUserId) async {
    return const <StudentProfile>[
      StudentProfile(
        id: 'student-1',
        fullName: 'Ayse Demir',
        gradeLevel: '8',
        origin: 'TeacherManaged',
        subjects: <StudentSubjectTarget>[],
      ),
      StudentProfile(
        id: 'student-2',
        fullName: 'Mert Kaya',
        gradeLevel: '11',
        origin: 'TeacherManaged',
        subjects: <StudentSubjectTarget>[],
      ),
    ];
  }
}

class _FakeSchedulingRepository implements SchedulingRepository {
  _FakeSchedulingRepository(this.now);

  final DateTime now;

  @override
  Future<LessonSchedule> cancelLesson({
    required String lessonId,
    String? cancellationNote,
  }) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> completeLesson({required String lessonId}) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> createLesson(LessonSchedule lessonSchedule) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> updateLesson(LessonSchedule lessonSchedule) {
    throw UnimplementedError();
  }

  @override
  Future<LessonSchedule> getLesson(String lessonId) {
    throw UnimplementedError();
  }

  @override
  Future<List<LessonSchedule>> listTeacherLessons({
    required String teacherUserId,
    DateTime? startAtUtc,
    DateTime? endAtUtc,
  }) async {
    final todayUpcoming = now.add(const Duration(minutes: 5));
    return <LessonSchedule>[
      _lesson('lesson-today', todayUpcoming),
      _lesson('lesson-upcoming', now.add(const Duration(days: 1))),
      _lesson('lesson-past', now.subtract(const Duration(days: 1))),
    ];
  }

  LessonSchedule _lesson(String id, DateTime start) {
    return LessonSchedule(
      id: id,
      teacherUserId: 'teacher-1',
      studentId: 'student-1',
      subject: 'Matematik',
      lessonFormat: 'Online',
      startAtUtc: start,
      endAtUtc: start.add(const Duration(hours: 1)),
      timeZone: 'Europe/Istanbul',
    );
  }
}

class _FakeDashboardRepository implements DashboardRepository {
  @override
  Future<TeacherDashboardSummary> getSummary(String teacherUserId) async {
    return TeacherDashboardSummary.empty;
  }
}

class _FakePaymentRepository implements PaymentRepository {
  @override
  Future<PaymentRecord> createRecord(PaymentRecord paymentRecord) {
    throw UnimplementedError();
  }

  @override
  Future<PaymentSummary> getSummary(String teacherUserId) async {
    return const PaymentSummary(
      totalRecords: 3,
      currencySummaries: <PaymentCurrencySummary>[
        PaymentCurrencySummary(
          currency: 'TRY',
          pendingCount: 2,
          partialCount: 0,
          paidCount: 1,
          overdueCount: 1,
          cancelledCount: 0,
          expectedAmountTotal: 2000,
          collectedAmountTotal: 750,
          outstandingAmountTotal: 1250,
          overdueAmountTotal: 500,
        ),
      ],
    );
  }

  @override
  Future<List<PaymentRecord>> listTeacherRecords(String teacherUserId) async {
    return <PaymentRecord>[
      PaymentRecord(
        id: 'payment-1',
        teacherUserId: teacherUserId,
        studentId: 'student-1',
        description: 'Matematik ders ucreti',
        currency: 'TRY',
        expectedAmount: 750,
        collectedAmount: 750,
        outstandingAmount: 0,
        status: 'Collected',
        collectedOnUtc: DateTime.now().toUtc(),
      ),
    ];
  }

  @override
  Future<PaymentRecord> updateRecord(PaymentRecord paymentRecord) {
    throw UnimplementedError();
  }

  @override
  Future<PaymentPage> searchRecords(
    String teacherUserId, {
    required PaymentFilters filters,
    required int skip,
    required int take,
  }) {
    throw UnimplementedError();
  }
}
