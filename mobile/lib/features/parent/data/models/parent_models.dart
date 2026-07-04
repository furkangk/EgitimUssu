import 'package:egitim_ussu_mobile/features/parent/domain/parent_contracts.dart';

/// `/api/parents` JSON yanıtlarını domain entity'lerine çeviren yardımcılar.
class ParentMappers {
  static DateTime? _dateOrNull(dynamic value) {
    if (value == null) return null;
    return DateTime.tryParse(value.toString());
  }

  static double _toDouble(dynamic value) =>
      value == null ? 0 : (value as num).toDouble();

  static int _toInt(dynamic value) => value == null ? 0 : (value as num).toInt();

  static ParentNotificationPreferences preferences(Map<String, dynamic> json) {
    return ParentNotificationPreferences(
      missedAssignment: json['missedAssignment'] as bool? ?? false,
      weeklyProgressSummary: json['weeklyProgressSummary'] as bool? ?? false,
      lessonReminders: json['lessonReminders'] as bool? ?? false,
      testResults: json['testResults'] as bool? ?? false,
      payments: json['payments'] as bool? ?? false,
      channel: json['channel'] as String? ?? 'Push',
    );
  }

  static ParentProfile profile(Map<String, dynamic> json) {
    final prefs = json['preferences'];
    return ParentProfile(
      id: json['id'] as String,
      userId: json['userId'] as String,
      fullName: json['fullName'] as String? ?? '',
      contactPhone: json['contactPhone'] as String?,
      contactEmail: json['contactEmail'] as String?,
      isActive: json['isActive'] as bool? ?? true,
      preferences: prefs is Map<String, dynamic>
          ? preferences(prefs)
          : ParentNotificationPreferences.fallback,
    );
  }

  static ChildProgressSummary? progress(dynamic json) {
    if (json is! Map<String, dynamic>) return null;
    return ChildProgressSummary(
      completedLessonCount: _toInt(json['completedLessonCount']),
      openAssignmentCount: _toInt(json['openAssignmentCount']),
      weeklyStudyMinutes: _toInt(json['weeklyStudyMinutes']),
      lastLessonCompletedAtUtc: _dateOrNull(json['lastLessonCompletedAtUtc']),
    );
  }

  static ChildLink childLink(Map<String, dynamic> json) {
    return ChildLink(
      id: json['id'] as String,
      parentUserId: json['parentUserId'] as String,
      studentId: json['studentId'] as String,
      childDisplayName: json['childDisplayName'] as String?,
      relationship: json['relationship'] as String?,
      status: json['status'] as String? ?? 'Pending',
      isPrimaryContact: json['isPrimaryContact'] as bool? ?? false,
      requestedOnUtc:
          _dateOrNull(json['requestedOnUtc']) ?? DateTime.now().toUtc(),
      linkedOnUtc: _dateOrNull(json['linkedOnUtc']),
      progress: progress(json['progress']),
    );
  }

  static ChildDashboard dashboard(Map<String, dynamic> json) {
    final study = json['study'] as Map<String, dynamic>? ?? const {};
    final lessons = json['lessons'] as Map<String, dynamic>? ?? const {};
    final assignments =
        json['assignments'] as Map<String, dynamic>? ?? const {};
    final payments = json['payments'] as Map<String, dynamic>? ?? const {};
    return ChildDashboard(
      studentId: json['studentId'] as String,
      childDisplayName: json['childDisplayName'] as String?,
      linkStatus: json['linkStatus'] as String? ?? 'Approved',
      updatedOnUtc: _dateOrNull(json['updatedOnUtc']),
      study: StudySummary(
        weeklyStudyMinutes: _toInt(study['weeklyStudyMinutes']),
        streakDays: _toInt(study['streakDays']),
        hasData: study['hasData'] as bool? ?? false,
      ),
      lessons: LessonSummary(
        completedLessonCount: _toInt(lessons['completedLessonCount']),
        plannedLessonCount: _toInt(lessons['plannedLessonCount']),
        lastLessonCompletedAtUtc: _dateOrNull(
          lessons['lastLessonCompletedAtUtc'],
        ),
      ),
      assignments: AssignmentSummary(
        totalCount: _toInt(assignments['totalCount']),
        openCount: _toInt(assignments['openCount']),
        completedCount: _toInt(assignments['completedCount']),
      ),
      payments: PaymentSummary(
        currency: payments['currency'] as String? ?? 'TRY',
        expectedTotal: _toDouble(payments['expectedTotal']),
        collectedTotal: _toDouble(payments['collectedTotal']),
        outstandingTotal: _toDouble(payments['outstandingTotal']),
        lastUpdatedAtUtc: _dateOrNull(payments['lastUpdatedAtUtc']),
      ),
    );
  }
}
