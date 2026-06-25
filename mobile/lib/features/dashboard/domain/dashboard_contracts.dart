class TeacherDashboardSummary {
  const TeacherDashboardSummary({
    required this.todayLessons,
    required this.pendingAssignmentsCount,
    required this.pendingAssignments,
    required this.overduePaymentsCount,
    required this.overduePaymentsCurrency,
    required this.overduePaymentsTotal,
    required this.overduePayments,
  });

  final List<DashboardTodayLesson> todayLessons;
  final int pendingAssignmentsCount;
  final List<DashboardPendingAssignment> pendingAssignments;
  final int overduePaymentsCount;
  final String overduePaymentsCurrency;
  final double overduePaymentsTotal;
  final List<DashboardOverduePayment> overduePayments;

  static const TeacherDashboardSummary empty = TeacherDashboardSummary(
    todayLessons: [],
    pendingAssignmentsCount: 0,
    pendingAssignments: [],
    overduePaymentsCount: 0,
    overduePaymentsCurrency: 'TRY',
    overduePaymentsTotal: 0,
    overduePayments: [],
  );
}

class DashboardTodayLesson {
  const DashboardTodayLesson({
    required this.id,
    required this.studentId,
    required this.subject,
    required this.lessonFormat,
    required this.startAtUtc,
    required this.endAtUtc,
    this.locationLabel,
  });

  final String id;
  final String studentId;
  final String subject;
  final String lessonFormat;
  final DateTime startAtUtc;
  final DateTime endAtUtc;
  final String? locationLabel;

  bool get isOnline => lessonFormat.toLowerCase().contains('online');
}

class DashboardPendingAssignment {
  const DashboardPendingAssignment({
    required this.id,
    required this.studentId,
    required this.title,
    this.dueDateUtc,
  });

  final String id;
  final String studentId;
  final String title;
  final DateTime? dueDateUtc;
}

class DashboardOverduePayment {
  const DashboardOverduePayment({
    required this.id,
    required this.studentId,
    required this.description,
    required this.currency,
    required this.outstandingAmount,
    required this.dueDateUtc,
  });

  final String id;
  final String studentId;
  final String description;
  final String currency;
  final double outstandingAmount;
  final DateTime dueDateUtc;
}

abstract interface class DashboardRepository {
  Future<TeacherDashboardSummary> getSummary(String teacherUserId);
}
