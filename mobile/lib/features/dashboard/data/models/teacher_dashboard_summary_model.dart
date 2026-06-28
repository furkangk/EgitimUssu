import 'package:egitim_ussu_mobile/features/dashboard/domain/dashboard_contracts.dart';

class TeacherDashboardSummaryModel extends TeacherDashboardSummary {
  const TeacherDashboardSummaryModel({
    required super.todayLessons,
    required super.pendingAssignmentsCount,
    required super.pendingAssignments,
    required super.overduePaymentsCount,
    required super.overduePaymentsCurrency,
    required super.overduePaymentsTotal,
    required super.overduePayments,
  });

  factory TeacherDashboardSummaryModel.fromJson(Map<String, dynamic> json) {
    return TeacherDashboardSummaryModel(
      todayLessons: (json['todayLessons'] as List<dynamic>? ?? [])
          .whereType<Map<String, dynamic>>()
          .map(DashboardTodayLessonModel.fromJson)
          .toList(),
      pendingAssignmentsCount:
          (json['pendingAssignmentsCount'] as num?)?.toInt() ?? 0,
      pendingAssignments: (json['pendingAssignments'] as List<dynamic>? ?? [])
          .whereType<Map<String, dynamic>>()
          .map(DashboardPendingAssignmentModel.fromJson)
          .toList(),
      overduePaymentsCount:
          (json['overduePaymentsCount'] as num?)?.toInt() ?? 0,
      overduePaymentsCurrency:
          json['overduePaymentsCurrency'] as String? ?? 'TRY',
      overduePaymentsTotal:
          (json['overduePaymentsTotal'] as num?)?.toDouble() ?? 0.0,
      overduePayments: (json['overduePayments'] as List<dynamic>? ?? [])
          .whereType<Map<String, dynamic>>()
          .map(DashboardOverduePaymentModel.fromJson)
          .toList(),
    );
  }
}

class DashboardTodayLessonModel extends DashboardTodayLesson {
  const DashboardTodayLessonModel({
    required super.id,
    required super.studentId,
    required super.subject,
    required super.lessonFormat,
    required super.startAtUtc,
    required super.endAtUtc,
    super.status,
    super.locationLabel,
    super.meetingUrl,
  });

  factory DashboardTodayLessonModel.fromJson(Map<String, dynamic> json) {
    return DashboardTodayLessonModel(
      id: json['id'] as String,
      studentId: json['studentId'] as String,
      subject: json['subject'] as String,
      lessonFormat: json['lessonFormat'] as String? ?? '',
      startAtUtc: DateTime.parse(json['startAtUtc'] as String),
      endAtUtc: DateTime.parse(json['endAtUtc'] as String),
      status: json['status'] as String? ?? 'Planned',
      locationLabel: json['locationLabel'] as String?,
      meetingUrl: json['meetingUrl'] as String?,
    );
  }
}

class DashboardPendingAssignmentModel extends DashboardPendingAssignment {
  const DashboardPendingAssignmentModel({
    required super.id,
    required super.studentId,
    required super.title,
    super.dueDateUtc,
  });

  factory DashboardPendingAssignmentModel.fromJson(Map<String, dynamic> json) {
    final dueDateRaw = json['dueDateUtc'] as String?;
    return DashboardPendingAssignmentModel(
      id: json['id'] as String,
      studentId: json['studentId'] as String,
      title: json['title'] as String,
      dueDateUtc: dueDateRaw != null ? DateTime.parse(dueDateRaw) : null,
    );
  }
}

class DashboardOverduePaymentModel extends DashboardOverduePayment {
  const DashboardOverduePaymentModel({
    required super.id,
    required super.studentId,
    required super.description,
    required super.currency,
    required super.outstandingAmount,
    required super.dueDateUtc,
  });

  factory DashboardOverduePaymentModel.fromJson(Map<String, dynamic> json) {
    return DashboardOverduePaymentModel(
      id: json['id'] as String,
      studentId: json['studentId'] as String,
      description: json['description'] as String? ?? '',
      currency: json['currency'] as String? ?? 'TRY',
      outstandingAmount: (json['outstandingAmount'] as num?)?.toDouble() ?? 0.0,
      dueDateUtc: DateTime.parse(json['dueDateUtc'] as String),
    );
  }
}
