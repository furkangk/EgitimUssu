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
      upcomingLessons: const <DashboardUpcomingLesson>[
        DashboardUpcomingLesson(
          title: 'Matematik - 9. Sinif',
          studentName: 'Zeynep Demir',
          timeRange: '15:30 - 16:30',
          modeLabel: 'Online',
          isOnline: true,
        ),
        DashboardUpcomingLesson(
          title: 'Fizik - 11. Sinif',
          studentName: 'Ali Yilmaz',
          timeRange: '17:00 - 18:30',
          modeLabel: 'Yuz yuze',
          isOnline: false,
        ),
      ],
      recentActivities: const <DashboardActivity>[
        DashboardActivity(
          studentName: 'Ali Yilmaz',
          description: 'Odev teslim edildi',
          timeLabel: 'Bugun 10:30',
          accent: Color(0xFF20B486),
        ),
        DashboardActivity(
          studentName: 'Zeynep Demir',
          description: 'Odeme eklendi',
          timeLabel: 'Bugun 09:10',
          accent: Color(0xFFFFB84D),
        ),
        DashboardActivity(
          studentName: 'Mehmet Kaya',
          description: 'Yeni ders planlandi',
          timeLabel: 'Dun 18:40',
          accent: Color(0xFF3D8BFF),
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
  });

  final String title;
  final String studentName;
  final String timeRange;
  final String modeLabel;
  final bool isOnline;

  @override
  List<Object?> get props => <Object?>[
    title,
    studentName,
    timeRange,
    modeLabel,
    isOnline,
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
