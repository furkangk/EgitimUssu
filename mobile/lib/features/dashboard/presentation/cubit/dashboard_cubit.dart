import 'dart:math';

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/dashboard/domain/dashboard_contracts.dart';
import 'package:egitim_ussu_mobile/features/dashboard/presentation/cubit/dashboard_state.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

class DashboardCubit extends Cubit<DashboardState> {
  DashboardCubit({
    required StudentRepository studentRepository,
    required SchedulingRepository schedulingRepository,
    required PaymentRepository paymentRepository,
    required DashboardRepository dashboardRepository,
  }) : _studentRepository = studentRepository,
       _schedulingRepository = schedulingRepository,
       _paymentRepository = paymentRepository,
       _dashboardRepository = dashboardRepository,
       super(const DashboardState());

  final StudentRepository _studentRepository;
  final SchedulingRepository _schedulingRepository;
  final PaymentRepository _paymentRepository;
  final DashboardRepository _dashboardRepository;

  factory DashboardCubit.create() {
    return DashboardCubit(
      studentRepository: injector<StudentRepository>(),
      schedulingRepository: injector<SchedulingRepository>(),
      paymentRepository: injector<PaymentRepository>(),
      dashboardRepository: injector<DashboardRepository>(),
    );
  }

  Future<void> load(String teacherUserId) async {
    if (isClosed) return;
    emit(state.copyWith(isLoading: true, clearError: true));
    try {
      // Start all network calls in parallel.
      final studentsFuture = _studentRepository.listByTeacher(teacherUserId);
      final lessonsFuture = _schedulingRepository.listTeacherLessons(
        teacherUserId: teacherUserId,
      );
      final paymentSummaryFuture = _paymentRepository.getSummary(teacherUserId);
      final paymentRecordsFuture = _paymentRepository.listTeacherRecords(
        teacherUserId,
      );
      final dashboardSummaryFuture = _dashboardRepository.getSummary(
        teacherUserId,
      );

      final students = await studentsFuture;
      if (isClosed) return;
      final lessons = await lessonsFuture;
      if (isClosed) return;
      final paymentSummary = await paymentSummaryFuture;
      if (isClosed) return;
      final paymentRecords = await paymentRecordsFuture;
      if (isClosed) return;
      final dashboardSummary = await dashboardSummaryFuture;
      if (isClosed) return;

      final now = DateTime.now();
      final todayScheduled = lessons.where((l) => _isSameDay(l, now)).toList()
        ..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));
      final todayLessonCount = todayScheduled.length;
      final todayLessonDuration = todayScheduled.fold(
        Duration.zero,
        (total, lesson) =>
            total +
            lesson.endAtUtc.toLocal().difference(lesson.startAtUtc.toLocal()),
      );
      final todayLessons = todayScheduled
          .map(
            (l) => DashboardTodayLesson(
              id: l.id,
              studentId: l.studentId,
              subject: l.subject,
              lessonFormat: l.lessonFormat,
              startAtUtc: l.startAtUtc,
              endAtUtc: l.endAtUtc,
              status: l.status,
              locationLabel: l.locationLabel,
              meetingUrl: l.meetingUrl,
              lesson: l,
            ),
          )
          .toList();
      final streakCount = _calculateStreak(lessons, now);
      final upcomingLessons = _buildUpcomingLessons(
        lessons: lessons,
        students: students,
        now: now,
      );
      final firstCurrency = paymentSummary.currencySummaries.isEmpty
          ? null
          : paymentSummary.currencySummaries.first;
      final activities = _buildActivities(
        lessons: lessons,
        students: students,
        paymentRecords: paymentRecords,
        now: now,
      );

      emit(
        state.copyWith(
          isLoading: false,
          streakCount: streakCount,
          studentCount: students.length,
          todayLessonCount: todayLessonCount,
          upcomingLessonCount: upcomingLessons.length,
          todayLessonDuration: todayLessonDuration,
          outstandingAmount: firstCurrency?.outstandingAmountTotal ?? 0,
          overdueAmount: firstCurrency?.overdueAmountTotal ?? 0,
          overdueCount: firstCurrency?.overdueCount ?? 0,
          outstandingCurrency: firstCurrency?.currency ?? 'TRY',
          upcomingLessons: upcomingLessons,
          recentActivities: activities,
          students: students,
          lessons: lessons,
          paymentRecords: paymentRecords,
          todayLessons: todayLessons,
          pendingAssignments: dashboardSummary.pendingAssignments,
          overduePayments: dashboardSummary.overduePayments,
          clearError: true,
        ),
      );
    } on ApiException catch (error) {
      if (isClosed) return;
      emit(state.copyWith(isLoading: false, errorMessage: error.message));
    }
  }

  bool _isSameDay(LessonSchedule lesson, DateTime now) {
    final localDate = lesson.startAtUtc.toLocal();
    return localDate.year == now.year &&
        localDate.month == now.month &&
        localDate.day == now.day;
  }

  int _calculateStreak(List<LessonSchedule> lessons, DateTime now) {
    final days =
        lessons
            .map((lesson) => _dateOnly(lesson.startAtUtc.toLocal()))
            .where((date) => !date.isAfter(_dateOnly(now)))
            .toSet()
            .toList()
          ..sort((a, b) => b.compareTo(a));

    var streak = 0;
    var cursor = _dateOnly(now);
    if (days.isNotEmpty && !_sameDate(days.first, cursor)) {
      cursor = cursor.subtract(const Duration(days: 1));
    }

    for (final date in days) {
      if (_sameDate(date, cursor)) {
        streak += 1;
        cursor = cursor.subtract(const Duration(days: 1));
      }
    }
    return streak;
  }

  List<DashboardUpcomingLesson> _buildUpcomingLessons({
    required List<LessonSchedule> lessons,
    required List<StudentProfile> students,
    required DateTime now,
  }) {
    final namesById = <String, String>{
      for (final student in students) student.id: student.fullName,
    };
    final upcoming =
        lessons
            .where((lesson) => lesson.startAtUtc.toLocal().isAfter(now))
            .toList()
          ..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));

    return upcoming.take(6).map((lesson) {
      final start = lesson.startAtUtc.toLocal();
      final end = lesson.endAtUtc.toLocal();
      final studentName = namesById[lesson.studentId] ?? 'Ogrenci';
      final isOnline = lesson.lessonFormat.toLowerCase().contains('online');
      return DashboardUpcomingLesson(
        title: lesson.subject,
        studentName: studentName,
        timeRange: '${_formatTime(start)} - ${_formatTime(end)}',
        modeLabel: isOnline ? 'Online' : 'Yuz yuze',
        isOnline: isOnline,
        startAtUtc: lesson.startAtUtc,
        status: lesson.status,
        meetingUrl: lesson.meetingUrl,
        lesson: lesson,
      );
    }).toList();
  }

  List<DashboardActivity> _buildActivities({
    required List<LessonSchedule> lessons,
    required List<StudentProfile> students,
    required List<PaymentRecord> paymentRecords,
    required DateTime now,
  }) {
    final namesById = <String, String>{
      for (final student in students) student.id: student.fullName,
    };
    final activities = <_TimedActivity>[];

    for (final lesson in lessons) {
      final lessonTime = lesson.startAtUtc.toLocal();
      if (lessonTime.isAfter(now)) {
        continue;
      }

      final studentName = namesById[lesson.studentId] ?? 'Ogrenci';
      activities.add(
        _TimedActivity(
          time: lessonTime,
          activity: DashboardActivity(
            studentName: studentName,
            description: 'Ders planlandi',
            timeLabel: _relativeTimeLabel(lessonTime, now),
            accent: AppColors.accentBlue,
          ),
        ),
      );
    }

    for (final payment in paymentRecords) {
      final paymentTime =
          payment.collectedOnUtc?.toLocal() ?? payment.dueDateUtc?.toLocal();
      if (paymentTime == null) {
        continue;
      }
      final studentName = namesById[payment.studentId] ?? 'Ogrenci';
      activities.add(
        _TimedActivity(
          time: paymentTime,
          activity: DashboardActivity(
            studentName: studentName,
            description: payment.collectedAmount > 0
                ? 'Odeme alindi'
                : 'Odeme eklendi',
            timeLabel: _relativeTimeLabel(paymentTime, now),
            accent: payment.collectedAmount > 0
                ? AppColors.accentGreen
                : AppColors.amber,
          ),
        ),
      );
    }

    activities.sort((a, b) => b.time.compareTo(a.time));
    final topActivities = activities
        .take(4)
        .map((entry) => entry.activity)
        .toList();

    if (topActivities.isNotEmpty) {
      return topActivities;
    }

    final fallbackCount = min(students.length, 3);
    return List<DashboardActivity>.generate(fallbackCount, (index) {
      final student = students[index];
      return DashboardActivity(
        studentName: student.fullName,
        description: 'Odev teslim edildi',
        timeLabel: index == 0 ? 'Bugun 10:30' : 'Dun 18:40',
        accent: index.isEven ? AppColors.accentGreen : AppColors.accentBlue,
      );
    });
  }

  DateTime _dateOnly(DateTime dateTime) =>
      DateTime(dateTime.year, dateTime.month, dateTime.day);

  bool _sameDate(DateTime left, DateTime right) =>
      left.year == right.year &&
      left.month == right.month &&
      left.day == right.day;

  String _formatTime(DateTime dateTime) {
    final hour = dateTime.hour.toString().padLeft(2, '0');
    final minute = dateTime.minute.toString().padLeft(2, '0');
    return '$hour:$minute';
  }

  String _relativeTimeLabel(DateTime dateTime, DateTime now) {
    final date = _dateOnly(dateTime);
    final today = _dateOnly(now);
    final yesterday = today.subtract(const Duration(days: 1));
    final prefix = _sameDate(date, today)
        ? 'Bugun'
        : _sameDate(date, yesterday)
        ? 'Dun'
        : '${date.day.toString().padLeft(2, '0')}.${date.month.toString().padLeft(2, '0')}';
    return '$prefix ${_formatTime(dateTime)}';
  }
}

class _TimedActivity {
  const _TimedActivity({required this.time, required this.activity});

  final DateTime time;
  final DashboardActivity activity;
}
