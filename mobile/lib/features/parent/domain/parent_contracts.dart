// Veli (Parent) modülü domain sözleşmeleri: entity'ler + repository arayüzü.
// Backend: `/api/parents` (M09). Bu rol büyük ölçüde okuyan/birleştiren bir read-model rolüdür.

class ParentNotificationPreferences {
  const ParentNotificationPreferences({
    required this.missedAssignment,
    required this.weeklyProgressSummary,
    required this.lessonReminders,
    required this.testResults,
    required this.payments,
    required this.channel,
  });

  final bool missedAssignment;
  final bool weeklyProgressSummary;
  final bool lessonReminders;
  final bool testResults;
  final bool payments;
  final String channel; // Push | Email | Both

  ParentNotificationPreferences copyWith({
    bool? missedAssignment,
    bool? weeklyProgressSummary,
    bool? lessonReminders,
    bool? testResults,
    bool? payments,
    String? channel,
  }) {
    return ParentNotificationPreferences(
      missedAssignment: missedAssignment ?? this.missedAssignment,
      weeklyProgressSummary: weeklyProgressSummary ?? this.weeklyProgressSummary,
      lessonReminders: lessonReminders ?? this.lessonReminders,
      testResults: testResults ?? this.testResults,
      payments: payments ?? this.payments,
      channel: channel ?? this.channel,
    );
  }

  static const ParentNotificationPreferences fallback =
      ParentNotificationPreferences(
        missedAssignment: true,
        weeklyProgressSummary: true,
        lessonReminders: false,
        testResults: true,
        payments: false,
        channel: 'Push',
      );
}

class ParentProfile {
  const ParentProfile({
    required this.id,
    required this.userId,
    required this.fullName,
    required this.preferences,
    required this.isActive,
    this.contactPhone,
    this.contactEmail,
  });

  final String id;
  final String userId;
  final String fullName;
  final String? contactPhone;
  final String? contactEmail;
  final ParentNotificationPreferences preferences;
  final bool isActive;
}

/// Veli–çocuk bağı: Pending → Approved / Rejected / Revoked.
class ChildLink {
  const ChildLink({
    required this.id,
    required this.parentUserId,
    required this.studentId,
    required this.status,
    required this.isPrimaryContact,
    required this.requestedOnUtc,
    this.childDisplayName,
    this.relationship,
    this.linkedOnUtc,
    this.progress,
  });

  final String id;
  final String parentUserId;
  final String studentId;
  final String? childDisplayName;
  final String? relationship;
  final String status; // Pending | Approved | Rejected | Revoked
  final bool isPrimaryContact;
  final DateTime requestedOnUtc;
  final DateTime? linkedOnUtc;
  final ChildProgressSummary? progress;

  bool get isApproved => status == 'Approved';
  bool get isPending => status == 'Pending';

  String get displayName =>
      (childDisplayName != null && childDisplayName!.trim().isNotEmpty)
      ? childDisplayName!
      : 'Öğrenci';
}

class ChildProgressSummary {
  const ChildProgressSummary({
    required this.completedLessonCount,
    required this.openAssignmentCount,
    required this.weeklyStudyMinutes,
    this.lastLessonCompletedAtUtc,
  });

  final int completedLessonCount;
  final int openAssignmentCount;
  final int weeklyStudyMinutes;
  final DateTime? lastLessonCompletedAtUtc;
}

class ChildDashboard {
  const ChildDashboard({
    required this.studentId,
    required this.linkStatus,
    required this.study,
    required this.lessons,
    required this.assignments,
    required this.payments,
    this.childDisplayName,
    this.updatedOnUtc,
  });

  final String studentId;
  final String? childDisplayName;
  final String linkStatus;
  final StudySummary study;
  final LessonSummary lessons;
  final AssignmentSummary assignments;
  final PaymentSummary payments;
  final DateTime? updatedOnUtc;
}

class StudySummary {
  const StudySummary({
    required this.weeklyStudyMinutes,
    required this.streakDays,
    required this.hasData,
    this.weeklyBreakdownMinutes = const <int>[],
  });

  final int weeklyStudyMinutes;
  final int streakDays;
  final bool hasData;

  /// Pzt→Paz 7 günlük dakika dağılımı (grafik için; API vermezse boş).
  final List<int> weeklyBreakdownMinutes;
}

class LessonSummary {
  const LessonSummary({
    required this.completedLessonCount,
    required this.plannedLessonCount,
    this.lastLessonCompletedAtUtc,
  });

  final int completedLessonCount;
  final int plannedLessonCount;
  final DateTime? lastLessonCompletedAtUtc;
}

class AssignmentSummary {
  const AssignmentSummary({
    required this.totalCount,
    required this.openCount,
    required this.completedCount,
  });

  final int totalCount;
  final int openCount;
  final int completedCount;
}

class PaymentSummary {
  const PaymentSummary({
    required this.currency,
    required this.expectedTotal,
    required this.collectedTotal,
    required this.outstandingTotal,
    this.lastUpdatedAtUtc,
  });

  final String currency;
  final double expectedTotal;
  final double collectedTotal;
  final double outstandingTotal;
  final DateTime? lastUpdatedAtUtc;
}

abstract interface class ParentRepository {
  Future<ParentProfile> getProfile(String userId);

  Future<ParentProfile> ensureProfile({
    required String userId,
    required String fullName,
    String? contactPhone,
    String? contactEmail,
  });

  Future<ParentProfile> updateNotificationPreferences({
    required String parentUserId,
    required ParentNotificationPreferences preferences,
  });

  Future<List<ChildLink>> listChildren(String parentUserId);

  Future<ChildLink> requestChildLink({
    required String parentUserId,
    required String studentId,
    String? relationship,
    String? childDisplayName,
    bool isPrimaryContact,
  });

  Future<ChildDashboard> getChildDashboard({
    required String parentUserId,
    required String studentId,
  });
}
