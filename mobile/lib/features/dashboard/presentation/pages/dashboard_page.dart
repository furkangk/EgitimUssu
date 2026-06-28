import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/dashboard/domain/dashboard_contracts.dart';
import 'package:egitim_ussu_mobile/features/dashboard/presentation/cubit/dashboard_cubit.dart';
import 'package:egitim_ussu_mobile/features/dashboard/presentation/cubit/dashboard_state.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_detail_page.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/widgets/lesson_form_sheet.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_bottom_nav.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class DashboardPage extends StatelessWidget {
  const DashboardPage({super.key});

  @override
  Widget build(BuildContext context) {
    final session = context.select((AuthCubit cubit) => cubit.state.session);
    final name = session?.fullName.trim().isNotEmpty == true
        ? session!.fullName
        : 'Ahmet Bey';
    final teacherUserId = session?.userId ?? 'mock-teacher-user';

    return BlocProvider(
      create: (_) => DashboardCubit.create()..load(teacherUserId),
      child: BlocBuilder<DashboardCubit, DashboardState>(
        builder: (context, state) {
          return TeacherPanelView(
            teacherName: name,
            state: state,
            onRefresh: () => context.read<DashboardCubit>().load(teacherUserId),
            onOpenSchedule: () => context.go('/scheduling'),
            onOpenLessons: () => context.go('/lesson-sessions'),
            onOpenStudents: () => context.go('/students'),
            onOpenPayments: () => context.go('/payments'),
            onOpenProfile: () => context.go('/more'),
          );
        },
      ),
    );
  }
}

class TeacherPanelPreviewPage extends StatelessWidget {
  const TeacherPanelPreviewPage({super.key});

  @override
  Widget build(BuildContext context) {
    return TeacherPanelView(
      teacherName: 'Ahmet Bey',
      state: DashboardState.preview(),
    );
  }
}

class TeacherPanelView extends StatelessWidget {
  const TeacherPanelView({
    super.key,
    required this.teacherName,
    required this.state,
    this.onRefresh,
    this.onOpenSchedule,
    this.onOpenLessons,
    this.onOpenStudents,
    this.onOpenPayments,
    this.onOpenProfile,
  });

  final String teacherName;
  final DashboardState state;
  final Future<void> Function()? onRefresh;
  final VoidCallback? onOpenSchedule;
  final VoidCallback? onOpenLessons;
  final VoidCallback? onOpenStudents;
  final VoidCallback? onOpenPayments;
  final VoidCallback? onOpenProfile;

  @override
  Widget build(BuildContext context) {
    final body = ListView(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 110),
      children: <Widget>[
        AppPageHeader(title: teacherName),
        if (state.errorMessage != null) ...<Widget>[
          const SizedBox(height: 12),
          _ErrorBanner(message: state.errorMessage!),
        ],
        const SizedBox(height: 20),
        _SummaryCards(
          streakCount: state.streakCount,
          todayLessonCount: state.todayLessonCount,
          todayDurationLabel: state.todayLessonDurationLabel,
        ),
        const SizedBox(height: 24),
        _SectionHeader(title: 'Hizli islemler'),
        const SizedBox(height: 12),
        _QuickActions(
          actions: <_QuickAction>[
            _QuickAction(
              icon: Icons.add_alarm_rounded,
              label: 'Ders Ekle',
              accent: AppColors.accentBlue,
              onTap: () => _showLessonForm(context, state),
            ),
            _QuickAction(
              icon: Icons.post_add_rounded,
              label: 'Odev ver',
              accent: AppColors.accentGreen,
              onTap: () => context.push('/assignments/new'),
            ),
            _QuickAction(
              icon: Icons.edit_note_rounded,
              label: 'Not ekle',
              accent: AppColors.accentBlue,
              onTap: () => context.push('/lesson-notes/new'),
            ),
            _QuickAction(
              icon: Icons.payments_rounded,
              label: 'Odeme ekle',
              accent: AppColors.amber,
              onTap: () => context.push('/payments/new'),
            ),
          ],
        ),
        const SizedBox(height: 24),
        _SectionHeader(
          title: 'Dersler',
          actionLabel: 'Tumunu gor',
          onTap: onOpenLessons,
        ),
        const SizedBox(height: 12),
        _LessonsSection(
          todayLessons: state.todayLessons,
          upcomingLessons: state.upcomingLessons,
          isLoading: state.isLoading,
          studentNamesById: {for (final s in state.students) s.id: s.fullName},
          onViewAll: onOpenLessons,
          onRefresh: onRefresh,
        ),
        const SizedBox(height: 24),
        _PendingAssignmentsSection(
          assignments: state.pendingAssignments,
          isLoading: state.isLoading,
          studentNamesById: {for (final s in state.students) s.id: s.fullName},
        ),
        if (state.overduePayments.isNotEmpty || state.isLoading) ...[
          const SizedBox(height: 24),
          _OverduePaymentsSection(
            payments: state.overduePayments,
            isLoading: state.isLoading,
            studentNamesById: {
              for (final s in state.students) s.id: s.fullName,
            },
            onViewAll: onOpenPayments,
          ),
        ],
        const SizedBox(height: 24),
        _SectionHeader(title: 'Son aktiviteler'),
        const SizedBox(height: 12),
        _ActivitySection(activities: state.recentActivities),
      ],
    );

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: onRefresh == null
            ? body
            : RefreshIndicator(
                color: AppColors.primary,
                onRefresh: onRefresh!,
                child: body,
              ),
      ),
      bottomNavigationBar: const AppBottomNav(current: AppNavTab.home),
    );
  }
}

Future<void> _showLessonForm(BuildContext context, DashboardState state) async {
  final session = context.read<AuthCubit>().state.session;
  final teacherUserId = session?.userId ?? 'mock-teacher-user';
  final dashboardCubit = context.read<DashboardCubit>();
  await showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.white,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
    ),
    builder: (sheetContext) {
      return BlocProvider<SchedulingCubit>(
        create: (_) => SchedulingCubit.create(),
        child: LessonFormSheet(
          teacherUserId: teacherUserId,
          students: state.students,
          existingLessons: state.lessons,
        ),
      );
    },
  );
  if (context.mounted) {
    dashboardCubit.load(teacherUserId);
  }
}

class _SummaryCards extends StatelessWidget {
  const _SummaryCards({
    required this.streakCount,
    required this.todayLessonCount,
    required this.todayDurationLabel,
  });

  final int streakCount;
  final int todayLessonCount;
  final String todayDurationLabel;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: SizedBox(
            height: 142,
            child: _DashboardCard(
              color: AppColors.primary,
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      _SoftIcon(
                        icon: Icons.local_fire_department_rounded,
                        color: AppColors.amber,
                        background: Colors.white.withValues(alpha: 0.16),
                        size: 38,
                        iconSize: 19,
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'Gunluk streak',
                          style: Theme.of(context).textTheme.titleSmall
                              ?.copyWith(
                                color: Colors.white.withValues(alpha: 0.92),
                                fontWeight: FontWeight.w700,
                                fontSize: 13,
                              ),
                        ),
                      ),
                    ],
                  ),
                  const Spacer(),
                  Text(
                    '$streakCount gun',
                    style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      color: Colors.white,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: SizedBox(
            height: 142,
            child: _DashboardCard(
              color: AppColors.card,
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      _SoftIcon(
                        icon: Icons.schedule_rounded,
                        color: AppColors.primary,
                        background: AppColors.primaryLight,
                        size: 38,
                        iconSize: 19,
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'Bugunun Dersleri',
                          style: Theme.of(context).textTheme.titleSmall
                              ?.copyWith(
                                color: AppColors.textPrimary,
                                fontWeight: FontWeight.w700,
                                fontSize: 13,
                              ),
                        ),
                      ),
                    ],
                  ),
                  const Spacer(),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        '$todayLessonCount',
                        style: Theme.of(context).textTheme.headlineSmall
                            ?.copyWith(
                              color: AppColors.textPrimary,
                              fontWeight: FontWeight.w800,
                            ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Toplam $todayDurationLabel',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.textSecondary,
                          fontWeight: FontWeight.w400,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }
}

class _QuickActions extends StatelessWidget {
  const _QuickActions({required this.actions});

  final List<_QuickAction> actions;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      children: actions
          .map(
            (action) => SizedBox(
              width: (MediaQuery.of(context).size.width - 44) / 2,
              child: _QuickActionTile(action: action),
            ),
          )
          .toList(),
    );
  }
}

class _QuickActionTile extends StatelessWidget {
  const _QuickActionTile({required this.action});

  final _QuickAction action;

  @override
  Widget build(BuildContext context) {
    return _DashboardCard(
      onTap: action.onTap,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
      child: Row(
        children: <Widget>[
          _SoftIcon(
            icon: action.icon,
            color: action.accent,
            background: action.accent.withValues(alpha: 0.12),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              action.label,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _LessonCardData {
  const _LessonCardData({
    required this.dateLabel,
    required this.subject,
    required this.timeRange,
    required this.isOnline,
    required this.isToday,
    required this.startAt,
    this.status = 'Planned',
    this.studentName,
    this.meetingUrl,
    this.lesson,
  });

  final String dateLabel;
  final String subject;
  final String timeRange;
  final bool isOnline;
  final bool isToday;
  final DateTime startAt;
  final String status;
  final String? studentName;
  final String? meetingUrl;

  /// Kaynak ders planı; ders detayında tamamlama/kalıcı düzenleme için taşınır.
  final LessonSchedule? lesson;
}

class _LessonsSection extends StatelessWidget {
  const _LessonsSection({
    required this.todayLessons,
    required this.upcomingLessons,
    required this.isLoading,
    required this.studentNamesById,
    this.onViewAll,
    this.onRefresh,
  });

  final List<DashboardTodayLesson> todayLessons;
  final List<DashboardUpcomingLesson> upcomingLessons;
  final bool isLoading;
  final Map<String, String> studentNamesById;
  final VoidCallback? onViewAll;

  /// Ders detayinda tamamlama/duzenleme yapilip geri donulunce dashboard'i
  /// tazeler (detay degisiklik dondururse cagrilir).
  final Future<void> Function()? onRefresh;

  @override
  Widget build(BuildContext context) {
    if (isLoading && todayLessons.isEmpty && upcomingLessons.isEmpty) {
      return const _LoadingPanel();
    }

    final now = DateTime.now();
    final todayDate = DateTime(now.year, now.month, now.day);

    final items = <_LessonCardData>[
      ...todayLessons.map(
        (l) => _LessonCardData(
          dateLabel: 'Bugun',
          subject: l.subject,
          timeRange:
              '${_fmt(l.startAtUtc.toLocal())} - ${_fmt(l.endAtUtc.toLocal())}',
          isOnline: l.isOnline,
          isToday: true,
          startAt: l.startAtUtc,
          status: l.status,
          studentName: studentNamesById[l.studentId],
          meetingUrl: l.meetingUrl,
          lesson: l.lesson,
        ),
      ),
      ...upcomingLessons
          .where((l) {
            final day = DateTime(
              l.startAtUtc.toLocal().year,
              l.startAtUtc.toLocal().month,
              l.startAtUtc.toLocal().day,
            );
            return day != todayDate;
          })
          .map(
            (l) => _LessonCardData(
              dateLabel: _dateLabel(l.startAtUtc.toLocal(), now),
              subject: l.title,
              timeRange: l.timeRange,
              isOnline: l.isOnline,
              isToday: false,
              startAt: l.startAtUtc,
              status: l.status,
              studentName: l.studentName,
              meetingUrl: l.meetingUrl,
              lesson: l.lesson,
            ),
          ),
    ]..sort((a, b) => a.startAt.compareTo(b.startAt));

    if (items.isEmpty) {
      return const _EmptyPanel(
        icon: Icons.event_busy_outlined,
        title: 'Ders bulunmuyor',
        subtitle: 'Planlanan dersler burada gorunecek.',
      );
    }

    return SizedBox(
      height: 172,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: items.length,
        separatorBuilder: (_, __) => const SizedBox(width: 12),
        itemBuilder: (context, index) => _LessonCard(
          data: items[index],
          onTap: () => _openDetail(context, items[index]),
        ),
      ),
    );
  }

  Future<void> _openDetail(BuildContext context, _LessonCardData data) async {
    final changed = await context.push<bool>(
      '/lesson-sessions/detail',
      extra: LessonDetailPayload(
        studentName: data.studentName ?? 'Ogrenci',
        subject: data.subject,
        dateLabel: data.dateLabel,
        timeLabel: data.timeRange,
        modeLabel: data.isOnline ? 'Online' : 'Yuz yuze',
        accent: data.isOnline ? AppColors.accentGreen : AppColors.amber,
        meetingUrl: data.meetingUrl,
        lessonId: data.lesson?.id,
        lessonStatus: data.lesson?.status ?? data.status,
        lesson: data.lesson,
      ),
    );
    if (changed == true && context.mounted) {
      await onRefresh?.call();
    }
  }

  static String _fmt(DateTime dt) {
    final h = dt.hour.toString().padLeft(2, '0');
    final m = dt.minute.toString().padLeft(2, '0');
    return '$h:$m';
  }

  static bool _sameDate(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;

  static String _dateLabel(DateTime local, DateTime now) {
    final day = DateTime(local.year, local.month, local.day);
    final today = DateTime(now.year, now.month, now.day);
    final tomorrow = today.add(const Duration(days: 1));
    if (_sameDate(day, today)) return 'Bugun';
    if (_sameDate(day, tomorrow)) return 'Yarin';
    const dayNames = ['Pzt', 'Sal', 'Car', 'Per', 'Cum', 'Cmt', 'Paz'];
    if (day.difference(today).inDays <= 7) return dayNames[local.weekday - 1];
    const months = [
      'Oca',
      'Sub',
      'Mar',
      'Nis',
      'May',
      'Haz',
      'Tem',
      'Agu',
      'Eyl',
      'Eki',
      'Kas',
      'Ara',
    ];
    return '${local.day} ${months[local.month - 1]}';
  }
}

class _LessonCard extends StatelessWidget {
  const _LessonCard({required this.data, required this.onTap});

  final _LessonCardData data;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final isToday = data.isToday;
    final cardColor = isToday ? AppColors.primary : AppColors.card;
    final titleColor = isToday ? Colors.white : AppColors.textPrimary;
    final subColor = isToday
        ? Colors.white.withValues(alpha: 0.68)
        : AppColors.textSecondary;
    final modeColor = data.isOnline
        ? (isToday ? const Color(0xFF5CDFC4) : AppColors.accentGreen)
        : (isToday ? const Color(0xFFFFD27A) : AppColors.amber);
    final badgeBg = isToday
        ? Colors.white.withValues(alpha: 0.18)
        : AppColors.primaryLight;
    final badgeText = isToday ? Colors.white : AppColors.primary;
    final statusBadge = _dashboardStatusBadge(data);

    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: 172,
        decoration: BoxDecoration(
          color: cardColor,
          borderRadius: BorderRadius.circular(20),
          border: isToday ? null : Border.all(color: AppColors.skyBorder),
          boxShadow: AppShadows.soft,
        ),
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 3,
                  ),
                  decoration: BoxDecoration(
                    color: badgeBg,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    data.dateLabel,
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                      color: badgeText,
                      fontWeight: FontWeight.w700,
                      fontSize: 11,
                    ),
                  ),
                ),
                const Spacer(),
                _LessonStatusPill(badge: statusBadge),
              ],
            ),
            const Spacer(),
            Text(
              data.subject,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                color: titleColor,
                fontWeight: FontWeight.w800,
                height: 1.2,
              ),
            ),
            if (data.studentName != null) ...<Widget>[
              const SizedBox(height: 2),
              Text(
                data.studentName!,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(
                  context,
                ).textTheme.bodySmall?.copyWith(color: subColor),
              ),
            ],
            const SizedBox(height: 10),
            Row(
              children: <Widget>[
                Icon(Icons.schedule_rounded, size: 12, color: subColor),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    data.timeRange,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: subColor,
                      fontWeight: FontWeight.w600,
                      fontSize: 11,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 4),
            Row(
              children: <Widget>[
                Icon(
                  data.isOnline ? Icons.videocam_rounded : Icons.place_rounded,
                  size: 12,
                  color: modeColor,
                ),
                const SizedBox(width: 4),
                Text(
                  data.isOnline ? 'Online' : 'Yuz yuze',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: modeColor,
                    fontWeight: FontWeight.w700,
                    fontSize: 11,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// Ders kartinda gosterilecek durum rozeti tanimi (etiket + renk + ikon).
class _StatusBadge {
  const _StatusBadge(this.label, this.color, this.icon);

  final String label;
  final Color color;
  final IconData icon;
}

/// Ana sayfa ders karti icin durum rozetini cozer. Her ders icin bir rozet
/// doner: Tamamlandi / Iptal / Taslak / (planli ve saati gecmisse) Bekliyor /
/// (gelecek planli) Planlandi.
_StatusBadge _dashboardStatusBadge(_LessonCardData data) {
  switch (data.status) {
    case 'Completed':
      return const _StatusBadge(
        'Tamamlandı',
        AppColors.accentGreen,
        Icons.check_circle_rounded,
      );
    case 'Cancelled':
      return const _StatusBadge(
        'İptal',
        AppColors.accentRed,
        Icons.cancel_rounded,
      );
    case 'Draft':
      return const _StatusBadge(
        'Taslak',
        AppColors.textMuted,
        Icons.edit_note_rounded,
      );
    default:
      final started = !data.startAt.isAfter(DateTime.now().toUtc());
      return started
          ? const _StatusBadge(
              'Bekliyor',
              AppColors.amber,
              Icons.hourglass_bottom_rounded,
            )
          : const _StatusBadge(
              'Planlandı',
              AppColors.secondary,
              Icons.event_available_rounded,
            );
  }
}

class _LessonStatusPill extends StatelessWidget {
  const _LessonStatusPill({required this.badge});

  final _StatusBadge badge;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
      decoration: BoxDecoration(
        color: badge.color,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(badge.icon, size: 11, color: Colors.white),
          const SizedBox(width: 3),
          Text(
            badge.label,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w700,
              fontSize: 10,
            ),
          ),
        ],
      ),
    );
  }
}

class _ActivitySection extends StatelessWidget {
  const _ActivitySection({required this.activities});

  final List<DashboardActivity> activities;

  @override
  Widget build(BuildContext context) {
    if (activities.isEmpty) {
      return const _EmptyPanel(
        icon: Icons.history_toggle_off_rounded,
        title: 'Henuz aktivite yok',
        subtitle: 'Son hareketler bu alanda listelenecek.',
      );
    }

    return _DashboardCard(
      padding: EdgeInsets.zero,
      child: Column(
        children: [
          for (int i = 0; i < activities.length; i++) ...[
            _ActivityRow(activity: activities[i]),
            if (i < activities.length - 1)
              const Divider(
                height: 1,
                thickness: 1,
                color: AppColors.skyBorder,
                indent: 66,
                endIndent: 16,
              ),
          ],
        ],
      ),
    );
  }
}

class _ActivityRow extends StatelessWidget {
  const _ActivityRow({required this.activity});

  final DashboardActivity activity;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 13),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: activity.accent.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(12),
            ),
            alignment: Alignment.center,
            child: Icon(
              _iconFor(activity.description),
              color: activity.accent,
              size: 20,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const SizedBox(height: 1),
                Text(
                  activity.studentName,
                  style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  activity.description,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Padding(
            padding: const EdgeInsets.only(top: 3),
            child: Text(
              activity.timeLabel,
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: AppColors.textSecondary,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
        ],
      ),
    );
  }

  IconData _iconFor(String description) {
    final lower = description.toLowerCase();
    if (lower.contains('ders')) return Icons.menu_book_rounded;
    if (lower.contains('odeme') || lower.contains('ödeme')) {
      return lower.contains('alindi')
          ? Icons.check_circle_rounded
          : Icons.receipt_long_rounded;
    }
    if (lower.contains('odev') || lower.contains('ödev')) {
      return Icons.assignment_turned_in_rounded;
    }
    return Icons.notifications_rounded;
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title, this.actionLabel, this.onTap});

  final String title;
  final String? actionLabel;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            title,
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        if (actionLabel != null)
          TextButton(
            onPressed: onTap,
            child: Text(
              actionLabel!,
              style: const TextStyle(
                color: AppColors.primary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
      ],
    );
  }
}

class _DashboardCard extends StatelessWidget {
  const _DashboardCard({
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.color = Colors.white,
    this.onTap,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color color;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final content = Container(
      padding: padding,
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(
          color: color == Colors.white
              ? AppColors.skyBorder
              : Colors.white.withValues(alpha: 0.08),
        ),
        boxShadow: AppShadows.soft,
      ),
      child: child,
    );

    if (onTap == null) {
      return content;
    }

    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: onTap,
      child: content,
    );
  }
}

class _SoftIcon extends StatelessWidget {
  const _SoftIcon({
    required this.icon,
    required this.color,
    required this.background,
    this.size = 44,
    this.iconSize = 22,
  });

  final IconData icon;
  final Color color;
  final Color background;
  final double size;
  final double iconSize;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Icon(icon, color: color, size: iconSize),
    );
  }
}

class _EmptyPanel extends StatelessWidget {
  const _EmptyPanel({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  final IconData icon;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return _DashboardCard(
      child: Column(
        children: <Widget>[
          _SoftIcon(
            icon: icon,
            color: AppColors.primary,
            background: AppColors.primaryLight,
          ),
          const SizedBox(height: 12),
          Text(
            title,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            subtitle,
            textAlign: TextAlign.center,
            style: Theme.of(
              context,
            ).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
          ),
        ],
      ),
    );
  }
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: AppColors.errorSurface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.errorBorder),
      ),
      child: Row(
        children: <Widget>[
          const Icon(
            Icons.error_outline_rounded,
            color: AppColors.error,
            size: 20,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              message,
              style: Theme.of(
                context,
              ).textTheme.bodyMedium?.copyWith(color: AppColors.error),
            ),
          ),
        ],
      ),
    );
  }
}

class _PendingAssignmentsSection extends StatefulWidget {
  const _PendingAssignmentsSection({
    required this.assignments,
    required this.isLoading,
    required this.studentNamesById,
  });

  final List<DashboardPendingAssignment> assignments;
  final bool isLoading;
  final Map<String, String> studentNamesById;

  @override
  State<_PendingAssignmentsSection> createState() =>
      _PendingAssignmentsSectionState();
}

class _PendingAssignmentsSectionState
    extends State<_PendingAssignmentsSection> {
  bool _isExpanded = false;

  @override
  Widget build(BuildContext context) {
    final overdueCount = widget.assignments
        .where(
          (a) =>
              a.dueDateUtc != null &&
              a.dueDateUtc!.toLocal().isBefore(DateTime.now()),
        )
        .length;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        GestureDetector(
          onTap: () => setState(() => _isExpanded = !_isExpanded),
          behavior: HitTestBehavior.opaque,
          child: Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Bekleyen Odevler',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              if (overdueCount > 0) ...<Widget>[
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 3,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.errorSurfaceStrong,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    '$overdueCount gecikti',
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                      color: AppColors.error,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                const SizedBox(width: 8),
              ],
              if (widget.assignments.isNotEmpty)
                Text(
                  '${widget.assignments.length} odev',
                  style: const TextStyle(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w700,
                    fontSize: 14,
                  ),
                ),
              const SizedBox(width: 4),
              AnimatedRotation(
                turns: _isExpanded ? 0 : -0.25,
                duration: const Duration(milliseconds: 250),
                curve: Curves.easeInOut,
                child: const Icon(
                  Icons.keyboard_arrow_down_rounded,
                  color: AppColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
        AnimatedSize(
          duration: const Duration(milliseconds: 280),
          curve: Curves.easeInOut,
          child: _isExpanded
              ? Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    const SizedBox(height: 12),
                    if (widget.isLoading && widget.assignments.isEmpty)
                      const _LoadingPanel()
                    else if (widget.assignments.isEmpty)
                      const _EmptyPanel(
                        icon: Icons.assignment_turned_in_outlined,
                        title: 'Bekleyen odev yok',
                        subtitle: 'Tum odevler tamamlandi.',
                      )
                    else ...[
                      ...widget.assignments.map((assignment) {
                        final studentName =
                            widget.studentNamesById[assignment.studentId] ??
                            'Ogrenci';
                        return Padding(
                          padding: const EdgeInsets.only(bottom: 10),
                          child: _AssignmentCard(
                            assignment: assignment,
                            studentName: studentName,
                            onTap: () => context.push('/assignments'),
                          ),
                        );
                      }),
                      const SizedBox(height: 4),
                      _ViewAllButton(onTap: () => context.push('/assignments')),
                    ],
                  ],
                )
              : const SizedBox.shrink(),
        ),
      ],
    );
  }
}

class _AssignmentCard extends StatelessWidget {
  const _AssignmentCard({
    required this.assignment,
    required this.studentName,
    this.onTap,
  });

  final DashboardPendingAssignment assignment;
  final String studentName;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final urgency = _urgencyLevel();
    final label = _urgencyLabel();

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Container(
        clipBehavior: Clip.hardEdge,
        decoration: BoxDecoration(
          color: urgency.cardBg,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: urgency.borderColor),
          boxShadow: AppShadows.soft,
        ),
        child: IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              Container(width: 4, color: urgency.accent),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(12, 12, 10, 12),
                  child: Row(
                    children: <Widget>[
                      Container(
                        width: 38,
                        height: 38,
                        decoration: BoxDecoration(
                          color: urgency.avatarBg,
                          borderRadius: BorderRadius.circular(11),
                        ),
                        alignment: Alignment.center,
                        child: Text(
                          _initials(studentName),
                          style: TextStyle(
                            color: urgency.accent,
                            fontWeight: FontWeight.w800,
                            fontSize: 13,
                          ),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: <Widget>[
                            Text(
                              assignment.title,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: Theme.of(context).textTheme.bodyLarge
                                  ?.copyWith(
                                    color: AppColors.textPrimary,
                                    fontWeight: FontWeight.w700,
                                  ),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              studentName,
                              style: Theme.of(context).textTheme.bodySmall
                                  ?.copyWith(color: AppColors.textSecondary),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 8),
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 9,
                          vertical: 4,
                        ),
                        decoration: BoxDecoration(
                          color: urgency.badgeBg,
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          label,
                          style: TextStyle(
                            color: urgency.accent,
                            fontWeight: FontWeight.w700,
                            fontSize: 11,
                          ),
                        ),
                      ),
                      const SizedBox(width: 2),
                      Icon(
                        Icons.chevron_right_rounded,
                        color: AppColors.textSecondary,
                        size: 18,
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  _AssignmentUrgency _urgencyLevel() {
    if (assignment.dueDateUtc == null) return _AssignmentUrgency.later;
    final days = assignment.dueDateUtc!
        .toLocal()
        .difference(DateTime.now())
        .inDays;
    if (days < 0) return _AssignmentUrgency.overdue;
    if (days <= 2) return _AssignmentUrgency.urgent;
    if (days <= 7) return _AssignmentUrgency.soon;
    return _AssignmentUrgency.later;
  }

  String _urgencyLabel() {
    if (assignment.dueDateUtc == null) return 'Tarih yok';
    final due = assignment.dueDateUtc!.toLocal();
    final days = due.difference(DateTime.now()).inDays;
    if (days < 0) return 'Gecikti!';
    if (days == 0) return 'Bugun!';
    if (days == 1) return 'Yarin!';
    return '$days gun';
  }

  String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+'));
    if (parts.length >= 2) {
      return '${parts.first[0]}${parts.last[0]}'.toUpperCase();
    }
    return name.isEmpty ? '?' : name[0].toUpperCase();
  }
}

enum _AssignmentUrgency {
  overdue,
  urgent,
  soon,
  later;

  Color get accent {
    return switch (this) {
      overdue => AppColors.error,
      urgent => AppColors.warning,
      soon => AppColors.accentBlue,
      later => AppColors.accentGreen,
    };
  }

  Color get cardBg {
    return switch (this) {
      overdue => AppColors.errorSurface,
      urgent => AppColors.warningSurface,
      _ => Colors.white,
    };
  }

  Color get borderColor {
    return switch (this) {
      overdue => AppColors.errorBorder,
      urgent => AppColors.warningBorder,
      _ => AppColors.skyBorder,
    };
  }

  Color get avatarBg {
    return switch (this) {
      overdue => AppColors.errorSurfaceStrong,
      urgent => AppColors.warningSurfaceStrong,
      soon => AppColors.infoSurface,
      later => AppColors.successSurface,
    };
  }

  Color get badgeBg => avatarBg;
}

class _OverduePaymentsSection extends StatefulWidget {
  const _OverduePaymentsSection({
    required this.payments,
    required this.isLoading,
    required this.studentNamesById,
    this.onViewAll,
  });

  final List<DashboardOverduePayment> payments;
  final bool isLoading;
  final Map<String, String> studentNamesById;
  final VoidCallback? onViewAll;

  @override
  State<_OverduePaymentsSection> createState() =>
      _OverduePaymentsSectionState();
}

class _OverduePaymentsSectionState extends State<_OverduePaymentsSection> {
  bool _isExpanded = false;

  @override
  Widget build(BuildContext context) {
    final totalAmount = widget.payments.fold(
      0.0,
      (sum, p) => sum + p.outstandingAmount,
    );
    final currency = widget.payments.isNotEmpty
        ? widget.payments.first.currency
        : 'TRY';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        GestureDetector(
          onTap: () => setState(() => _isExpanded = !_isExpanded),
          behavior: HitTestBehavior.opaque,
          child: Row(
            children: [
              Expanded(
                child: Text(
                  'Geciken Odemeler',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              if (widget.payments.isNotEmpty) ...[
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 3,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.errorSurfaceStrong,
                    borderRadius: BorderRadius.circular(20),
                  ),
                  child: Text(
                    '${totalAmount.toStringAsFixed(0)} $currency',
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                      color: AppColors.error,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                const SizedBox(width: 8),
              ],
              AnimatedRotation(
                turns: _isExpanded ? 0 : -0.25,
                duration: const Duration(milliseconds: 250),
                curve: Curves.easeInOut,
                child: const Icon(
                  Icons.keyboard_arrow_down_rounded,
                  color: AppColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
        AnimatedSize(
          duration: const Duration(milliseconds: 280),
          curve: Curves.easeInOut,
          child: _isExpanded
              ? Column(
                  children: [
                    const SizedBox(height: 12),
                    if (widget.isLoading && widget.payments.isEmpty)
                      const _LoadingPanel()
                    else ...[
                      ...widget.payments.map((payment) {
                        final studentName =
                            widget.studentNamesById[payment.studentId] ??
                            'Ogrenci';
                        return Padding(
                          padding: const EdgeInsets.only(bottom: 10),
                          child: _PaymentCard(
                            payment: payment,
                            studentName: studentName,
                            onTap: widget.onViewAll,
                          ),
                        );
                      }),
                      const SizedBox(height: 4),
                      _ViewAllButton(onTap: widget.onViewAll),
                    ],
                  ],
                )
              : const SizedBox.shrink(),
        ),
      ],
    );
  }
}

class _PaymentCard extends StatelessWidget {
  const _PaymentCard({
    required this.payment,
    required this.studentName,
    this.onTap,
  });

  final DashboardOverduePayment payment;
  final String studentName;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final daysLate = DateTime.now()
        .difference(payment.dueDateUtc.toLocal())
        .inDays;
    final isVeryLate = daysLate > 7;

    final accentColor = isVeryLate ? AppColors.error : AppColors.warning;
    final cardBg = isVeryLate
        ? AppColors.errorSurface
        : AppColors.warningSurface;
    final borderColor = isVeryLate
        ? AppColors.errorBorder
        : AppColors.warningBorder;
    final avatarBg = isVeryLate
        ? AppColors.errorSurfaceStrong
        : AppColors.warningSurfaceStrong;
    final badgeLabel = daysLate <= 0 ? 'Bugun' : '$daysLate gun gecikti';

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Container(
        clipBehavior: Clip.hardEdge,
        decoration: BoxDecoration(
          color: cardBg,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: borderColor),
          boxShadow: AppShadows.soft,
        ),
        child: IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Container(width: 4, color: accentColor),
              Expanded(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(12, 12, 10, 12),
                  child: Row(
                    children: [
                      Container(
                        width: 38,
                        height: 38,
                        decoration: BoxDecoration(
                          color: avatarBg,
                          borderRadius: BorderRadius.circular(11),
                        ),
                        alignment: Alignment.center,
                        child: Text(
                          _initials(studentName),
                          style: TextStyle(
                            color: accentColor,
                            fontWeight: FontWeight.w800,
                            fontSize: 13,
                          ),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Text(
                              payment.description,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: Theme.of(context).textTheme.bodyLarge
                                  ?.copyWith(
                                    color: AppColors.textPrimary,
                                    fontWeight: FontWeight.w700,
                                  ),
                            ),
                            const SizedBox(height: 2),
                            Text(
                              studentName,
                              style: Theme.of(context).textTheme.bodySmall
                                  ?.copyWith(color: AppColors.textSecondary),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 8),
                      Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text(
                            '${payment.outstandingAmount.toStringAsFixed(0)} ${payment.currency}',
                            style: Theme.of(context).textTheme.titleMedium
                                ?.copyWith(
                                  color: accentColor,
                                  fontWeight: FontWeight.w800,
                                ),
                          ),
                          const SizedBox(height: 2),
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 7,
                              vertical: 3,
                            ),
                            decoration: BoxDecoration(
                              color: avatarBg,
                              borderRadius: BorderRadius.circular(20),
                            ),
                            child: Text(
                              badgeLabel,
                              style: TextStyle(
                                color: accentColor,
                                fontWeight: FontWeight.w700,
                                fontSize: 10,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+'));
    if (parts.length >= 2) {
      return '${parts.first[0]}${parts.last[0]}'.toUpperCase();
    }
    return name.isEmpty ? '?' : name[0].toUpperCase();
  }
}

class _ViewAllButton extends StatelessWidget {
  const _ViewAllButton({this.onTap});

  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(vertical: 13),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: AppColors.primary.withValues(alpha: 0.18)),
          boxShadow: AppShadows.soft,
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(
              'Tumunu Gor',
              style: TextStyle(
                color: AppColors.primary,
                fontWeight: FontWeight.w700,
                fontSize: 14,
              ),
            ),
            const SizedBox(width: 6),
            const Icon(
              Icons.arrow_forward_rounded,
              size: 16,
              color: AppColors.primary,
            ),
          ],
        ),
      ),
    );
  }
}

class _LoadingPanel extends StatelessWidget {
  const _LoadingPanel();

  @override
  Widget build(BuildContext context) {
    return _DashboardCard(
      padding: const EdgeInsets.all(24),
      child: Center(
        child: SizedBox(
          width: 24,
          height: 24,
          child: CircularProgressIndicator(
            strokeWidth: 2.5,
            color: AppColors.primary,
          ),
        ),
      ),
    );
  }
}

class _QuickAction {
  const _QuickAction({
    required this.icon,
    required this.label,
    required this.accent,
    this.onTap,
  });

  final IconData icon;
  final String label;
  final Color accent;
  final VoidCallback? onTap;
}
