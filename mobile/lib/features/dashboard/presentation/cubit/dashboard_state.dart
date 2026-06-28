import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/dashboard/domain/dashboard_contracts.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:equatable/equatable.dart';
import 'package:flutter/material.dart';

class DashboardState extends Equatable {
  const DashboardState({
    this.isLoading = false,
    this.streakCount = 0,
    this.studentCount = 0,
    this.todayLessonCount = 0,
    this.upcomingLessonCount = 0,
    this.todayLessonDuration = Duration.zero,
    this.outstandingAmount = 0,
    this.overdueAmount = 0,
    this.overdueCount = 0,
    this.outstandingCurrency = 'TRY',
    this.upcomingLessons = const <DashboardUpcomingLesson>[],
    this.recentActivities = const <DashboardActivity>[],
    this.students = const <StudentProfile>[],
    this.lessons = const <LessonSchedule>[],
    this.paymentRecords = const <PaymentRecord>[],
    this.todayLessons = const <DashboardTodayLesson>[],
    this.pendingAssignments = const <DashboardPendingAssignment>[],
    this.overduePayments = const <DashboardOverduePayment>[],
    this.errorMessage,
  });

  final bool isLoading;
  final int streakCount;
  final int studentCount;
  final int todayLessonCount;
  final int upcomingLessonCount;
  final Duration todayLessonDuration;
  final double outstandingAmount;
  final double overdueAmount;
  final int overdueCount;
  final String outstandingCurrency;
  final List<DashboardUpcomingLesson> upcomingLessons;
  final List<DashboardActivity> recentActivities;
  final List<StudentProfile> students;
  final List<LessonSchedule> lessons;
  final List<PaymentRecord> paymentRecords;
  final List<DashboardTodayLesson> todayLessons;
  final List<DashboardPendingAssignment> pendingAssignments;
  final List<DashboardOverduePayment> overduePayments;
  final String? errorMessage;

  String get todayLessonDurationLabel {
    final hours = todayLessonDuration.inHours;
    final minutes = todayLessonDuration.inMinutes.remainder(60);
    if (hours > 0 && minutes > 0) {
      return '${hours}s ${minutes}dk';
    }
    if (hours > 0) {
      return '${hours}s';
    }
    return '${todayLessonDuration.inMinutes}dk';
  }

  factory DashboardState.preview() {
    final now = DateTime.now().toUtc();
    final today = DateTime.utc(now.year, now.month, now.day);
    return DashboardState(
      streakCount: 6,
      studentCount: 8,
      todayLessonCount: 3,
      upcomingLessonCount: 5,
      todayLessonDuration: const Duration(hours: 3, minutes: 30),
      outstandingAmount: 2500,
      overdueAmount: 850,
      overdueCount: 1,
      outstandingCurrency: 'TRY',
      upcomingLessons: <DashboardUpcomingLesson>[
        DashboardUpcomingLesson(
          title: 'Matematik - 9. Sinif',
          studentName: 'Zeynep Demir',
          timeRange: '15:30 - 16:30',
          modeLabel: 'Online',
          isOnline: true,
          startAtUtc: today.add(
            const Duration(days: 1, hours: 15, minutes: 30),
          ),
        ),
        DashboardUpcomingLesson(
          title: 'Fizik - 11. Sinif',
          studentName: 'Ali Yilmaz',
          timeRange: '17:00 - 18:30',
          modeLabel: 'Yuz yuze',
          isOnline: false,
          startAtUtc: today.add(const Duration(days: 2, hours: 17)),
        ),
      ],
      recentActivities: const <DashboardActivity>[
        DashboardActivity(
          studentName: 'Ali Yilmaz',
          description: 'Odev teslim edildi',
          timeLabel: 'Bugun 10:30',
          accent: AppColors.accentGreen,
        ),
        DashboardActivity(
          studentName: 'Zeynep Demir',
          description: 'Odeme eklendi',
          timeLabel: 'Bugun 09:10',
          accent: AppColors.amber,
        ),
        DashboardActivity(
          studentName: 'Mehmet Kaya',
          description: 'Yeni ders planlandi',
          timeLabel: 'Dun 18:40',
          accent: AppColors.accentBlue,
        ),
      ],
      todayLessons: <DashboardTodayLesson>[
        DashboardTodayLesson(
          id: 'prev-1',
          studentId: 'student-1',
          subject: 'Matematik',
          lessonFormat: 'Online',
          startAtUtc: today.add(const Duration(hours: 15, minutes: 30)),
          endAtUtc: today.add(const Duration(hours: 16, minutes: 30)),
        ),
        DashboardTodayLesson(
          id: 'prev-2',
          studentId: 'student-2',
          subject: 'Fizik',
          lessonFormat: 'InPerson',
          startAtUtc: today.add(const Duration(hours: 18)),
          endAtUtc: today.add(const Duration(hours: 19, minutes: 30)),
          locationLabel: 'Calisma odasi',
        ),
      ],
      pendingAssignments: const <DashboardPendingAssignment>[
        DashboardPendingAssignment(
          id: 'asgn-1',
          studentId: 'student-1',
          title: 'Problem foyu 1',
        ),
        DashboardPendingAssignment(
          id: 'asgn-2',
          studentId: 'student-2',
          title: 'Fizik deney raporu',
        ),
      ],
      overduePayments: <DashboardOverduePayment>[
        DashboardOverduePayment(
          id: 'pay-1',
          studentId: 'student-2',
          description: 'Mayis ayi ders ucreti',
          currency: 'TRY',
          outstandingAmount: 850,
          dueDateUtc: today.subtract(const Duration(days: 5)),
        ),
      ],
    );
  }

  DashboardState copyWith({
    bool? isLoading,
    int? streakCount,
    int? studentCount,
    int? todayLessonCount,
    int? upcomingLessonCount,
    Duration? todayLessonDuration,
    double? outstandingAmount,
    double? overdueAmount,
    int? overdueCount,
    String? outstandingCurrency,
    List<DashboardUpcomingLesson>? upcomingLessons,
    List<DashboardActivity>? recentActivities,
    List<StudentProfile>? students,
    List<LessonSchedule>? lessons,
    List<PaymentRecord>? paymentRecords,
    List<DashboardTodayLesson>? todayLessons,
    List<DashboardPendingAssignment>? pendingAssignments,
    List<DashboardOverduePayment>? overduePayments,
    String? errorMessage,
    bool clearError = false,
  }) {
    return DashboardState(
      isLoading: isLoading ?? this.isLoading,
      streakCount: streakCount ?? this.streakCount,
      studentCount: studentCount ?? this.studentCount,
      todayLessonCount: todayLessonCount ?? this.todayLessonCount,
      upcomingLessonCount: upcomingLessonCount ?? this.upcomingLessonCount,
      todayLessonDuration: todayLessonDuration ?? this.todayLessonDuration,
      outstandingAmount: outstandingAmount ?? this.outstandingAmount,
      overdueAmount: overdueAmount ?? this.overdueAmount,
      overdueCount: overdueCount ?? this.overdueCount,
      outstandingCurrency: outstandingCurrency ?? this.outstandingCurrency,
      upcomingLessons: upcomingLessons ?? this.upcomingLessons,
      recentActivities: recentActivities ?? this.recentActivities,
      students: students ?? this.students,
      lessons: lessons ?? this.lessons,
      paymentRecords: paymentRecords ?? this.paymentRecords,
      todayLessons: todayLessons ?? this.todayLessons,
      pendingAssignments: pendingAssignments ?? this.pendingAssignments,
      overduePayments: overduePayments ?? this.overduePayments,
      errorMessage: clearError ? null : errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props => <Object?>[
    isLoading,
    streakCount,
    studentCount,
    todayLessonCount,
    upcomingLessonCount,
    todayLessonDuration,
    outstandingAmount,
    overdueAmount,
    overdueCount,
    outstandingCurrency,
    upcomingLessons,
    recentActivities,
    students,
    lessons,
    paymentRecords,
    todayLessons,
    pendingAssignments,
    overduePayments,
    errorMessage,
  ];
}

class DashboardUpcomingLesson extends Equatable {
  const DashboardUpcomingLesson({
    required this.title,
    required this.studentName,
    required this.timeRange,
    required this.modeLabel,
    required this.isOnline,
    required this.startAtUtc,
    this.meetingUrl,
  });

  final String title;
  final String studentName;
  final String timeRange;
  final String modeLabel;
  final bool isOnline;
  final DateTime startAtUtc;
  final String? meetingUrl;

  @override
  List<Object?> get props => <Object?>[
    title,
    studentName,
    timeRange,
    modeLabel,
    isOnline,
    startAtUtc,
    meetingUrl,
  ];
}

class DashboardActivity extends Equatable {
  const DashboardActivity({
    required this.studentName,
    required this.description,
    required this.timeLabel,
    required this.accent,
  });

  final String studentName;
  final String description;
  final String timeLabel;
  final Color accent;

  @override
  List<Object?> get props => <Object?>[
    studentName,
    description,
    timeLabel,
    accent,
  ];
}
