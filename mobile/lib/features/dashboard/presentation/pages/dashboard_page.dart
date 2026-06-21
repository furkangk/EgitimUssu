import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/dashboard/presentation/cubit/dashboard_cubit.dart';
import 'package:egitim_ussu_mobile/features/dashboard/presentation/cubit/dashboard_state.dart';
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

  static const navy = Color(0xFF082B4F);
  static const navySurface = Color(0xFF10365E);
  static const sky = Color(0xFFE9F4FF);
  static const skyBorder = Color(0xFFD7E7F8);
  static const amber = Color(0xFFFFB84D);
  static const emerald = Color(0xFF20B486);
  static const slate = Color(0xFF6B7A90);
  static const text = Color(0xFF10233D);
  static const background = Color(0xFFF4F8FC);
  static const card = Color(0xFFFFFFFF);
  static const divider = Color(0xFFE5EEF7);

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
    const staticUpcomingLessons = <DashboardUpcomingLesson>[
      DashboardUpcomingLesson(
        title: 'Matematik - Trigonometri',
        studentName: 'Zeynep Demir',
        timeRange: '15:30 - 16:30',
        modeLabel: 'Online',
        isOnline: true,
      ),
      DashboardUpcomingLesson(
        title: 'Fizik - Kuvvet ve Hareket',
        studentName: 'Ali Yilmaz',
        timeRange: '17:00 - 18:30',
        modeLabel: 'Yuz yuze',
        isOnline: false,
      ),
      DashboardUpcomingLesson(
        title: 'Geometri - Problem Cozumu',
        studentName: 'Merve Kaya',
        timeRange: '19:00 - 20:00',
        modeLabel: 'Online',
        isOnline: true,
      ),
    ];

    const staticActivities = <DashboardActivity>[
      DashboardActivity(
        studentName: 'Ali Yilmaz',
        description: 'Odev teslim edildi',
        timeLabel: 'Bugun 10:30',
        accent: TeacherPanelView.emerald,
      ),
      DashboardActivity(
        studentName: 'Zeynep Demir',
        description: 'Ders notu eklendi',
        timeLabel: 'Bugun 09:20',
        accent: Color(0xFF3D8BFF),
      ),
      DashboardActivity(
        studentName: 'Merve Kaya',
        description: 'Odeme eklendi',
        timeLabel: 'Dun 18:40',
        accent: TeacherPanelView.amber,
      ),
    ];

    final body = ListView(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 110),
      children: <Widget>[
        _TeacherHeader(teacherName: teacherName),
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
              label: 'Ders planla',
              accent: const Color(0xFF3D8BFF),
              onTap: () => context.push('/lesson-sessions?create=1'),
            ),
            _QuickAction(
              icon: Icons.post_add_rounded,
              label: 'Odev ver',
              accent: emerald,
              onTap: () => context.push('/assignments/new'),
            ),
            _QuickAction(
              icon: Icons.edit_note_rounded,
              label: 'Not ekle',
              accent: const Color(0xFF3D8BFF),
              onTap: () => context.push('/lesson-notes/new'),
            ),
            _QuickAction(
              icon: Icons.payments_rounded,
              label: 'Odeme ekle',
              accent: amber,
              onTap: () => context.push('/payments/new'),
            ),
          ],
        ),
        const SizedBox(height: 24),
        _SectionHeader(
          title: 'Yaklasan dersler',
          actionLabel: 'Tumunu gor',
          onTap: onOpenSchedule,
        ),
        const SizedBox(height: 12),
        const _UpcomingLessonsSection(lessons: staticUpcomingLessons),
        const SizedBox(height: 24),
        _SectionHeader(title: 'Son aktiviteler'),
        const SizedBox(height: 12),
        const _ActivitySection(activities: staticActivities),
      ],
    );

    return Scaffold(
      backgroundColor: background,
      body: SafeArea(
        child: onRefresh == null
            ? body
            : RefreshIndicator(color: navy, onRefresh: onRefresh!, child: body),
      ),
      bottomNavigationBar: _TeacherBottomNav(
        onLessonsTap: onOpenLessons,
        onStudentsTap: onOpenStudents,
        onCalendarTap: onOpenSchedule,
        onMoreTap: onOpenProfile,
        onFinanceTap: onOpenPayments,
      ),
    );
  }
}

class _TeacherHeader extends StatelessWidget {
  const _TeacherHeader({required this.teacherName});

  final String teacherName;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            teacherName,
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
              color: TeacherPanelView.text,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        _HeaderIconButton(
          icon: Icons.notifications_none_rounded,
          badgeText: '2',
          onTap: () {},
        ),
      ],
    );
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
              color: TeacherPanelView.navy,
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      _SoftIcon(
                        icon: Icons.local_fire_department_rounded,
                        color: TeacherPanelView.amber,
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
              color: TeacherPanelView.card,
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    children: <Widget>[
                      _SoftIcon(
                        icon: Icons.schedule_rounded,
                        color: TeacherPanelView.navy,
                        background: TeacherPanelView.sky,
                        size: 38,
                        iconSize: 19,
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'Bugunun Dersleri',
                          style: Theme.of(context).textTheme.titleSmall
                              ?.copyWith(
                                color: TeacherPanelView.text,
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
                              color: TeacherPanelView.text,
                              fontWeight: FontWeight.w800,
                            ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Toplam $todayDurationLabel',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: TeacherPanelView.slate,
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
                color: TeacherPanelView.text,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _UpcomingLessonsSection extends StatelessWidget {
  const _UpcomingLessonsSection({required this.lessons});

  final List<DashboardUpcomingLesson> lessons;

  @override
  Widget build(BuildContext context) {
    if (lessons.isEmpty) {
      return const _EmptyPanel(
        icon: Icons.event_busy_outlined,
        title: 'Yaklasan ders bulunmuyor',
        subtitle: 'Yeni planlanan dersler burada gorunecek.',
      );
    }

    return SizedBox(
      height: 166,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemCount: lessons.length,
        separatorBuilder: (_, _) => const SizedBox(width: 12),
        itemBuilder: (context, index) {
          final lesson = lessons[index];
          return SizedBox(
            width: 274,
            child: _DashboardCard(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      _AvatarBadge(
                        name: lesson.studentName,
                        accent: lesson.isOnline
                            ? TeacherPanelView.emerald
                            : TeacherPanelView.amber,
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: <Widget>[
                            Text(
                              lesson.title,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: Theme.of(context).textTheme.titleMedium
                                  ?.copyWith(
                                    color: TeacherPanelView.text,
                                    fontWeight: FontWeight.w800,
                                  ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              lesson.studentName,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: Theme.of(context).textTheme.bodyMedium
                                  ?.copyWith(color: TeacherPanelView.slate),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  const Divider(color: TeacherPanelView.divider, height: 1),
                  const SizedBox(height: 8),
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: <Widget>[
                      Expanded(
                        child: Text(
                          lesson.timeRange,
                          style: Theme.of(context).textTheme.bodyMedium
                              ?.copyWith(
                                color: TeacherPanelView.text,
                                fontWeight: FontWeight.w700,
                              ),
                        ),
                      ),
                      Text(
                        lesson.modeLabel,
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          color: lesson.isOnline
                              ? TeacherPanelView.emerald
                              : TeacherPanelView.amber,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          );
        },
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

    return Column(
      children: activities
          .map(
            (activity) => Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: _DashboardCard(
                padding: const EdgeInsets.all(14),
                child: Row(
                  children: <Widget>[
                    _AvatarBadge(
                      name: activity.studentName,
                      accent: activity.accent,
                      radius: 24,
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Text.rich(
                            TextSpan(
                              children: <InlineSpan>[
                                TextSpan(
                                  text: activity.studentName,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.w800,
                                  ),
                                ),
                                TextSpan(text: ' - ${activity.description}'),
                              ],
                            ),
                            style: Theme.of(context).textTheme.bodyLarge
                                ?.copyWith(color: TeacherPanelView.text),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            activity.timeLabel,
                            style: Theme.of(context).textTheme.bodySmall
                                ?.copyWith(color: TeacherPanelView.slate),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    Icon(
                      Icons.chevron_right_rounded,
                      color: TeacherPanelView.slate,
                    ),
                  ],
                ),
              ),
            ),
          )
          .toList(),
    );
  }
}

class _TeacherBottomNav extends StatelessWidget {
  const _TeacherBottomNav({
    this.onLessonsTap,
    this.onStudentsTap,
    this.onCalendarTap,
    this.onMoreTap,
    this.onFinanceTap,
  });

  final VoidCallback? onLessonsTap;
  final VoidCallback? onStudentsTap;
  final VoidCallback? onCalendarTap;
  final VoidCallback? onMoreTap;
  final VoidCallback? onFinanceTap;

  @override
  Widget build(BuildContext context) {
    final items = <_BottomNavItem>[
      const _BottomNavItem(Icons.home_rounded, 'Ana sayfa', true),
      _BottomNavItem(Icons.menu_book_rounded, 'Dersler', false, onLessonsTap),
      _BottomNavItem(Icons.groups_rounded, 'Ogrenciler', false, onStudentsTap),
      _BottomNavItem(
        Icons.calendar_month_rounded,
        'Takvim',
        false,
        onCalendarTap,
      ),
      _BottomNavItem(
        Icons.account_balance_wallet_rounded,
        'Finans',
        false,
        onFinanceTap,
      ),
      _BottomNavItem(Icons.widgets_rounded, 'Diger', false, onMoreTap),
    ];

    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: TeacherPanelView.divider)),
      ),
      padding: EdgeInsets.fromLTRB(
        10,
        8,
        10,
        MediaQuery.of(context).padding.bottom + 8,
      ),
      child: Row(
        children: items
            .map(
              (item) => Expanded(
                child: InkWell(
                  borderRadius: BorderRadius.circular(18),
                  onTap: item.onTap,
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        Icon(
                          item.icon,
                          color: item.selected
                              ? TeacherPanelView.navy
                              : TeacherPanelView.slate,
                        ),
                        const SizedBox(height: 4),
                        FittedBox(
                          fit: BoxFit.scaleDown,
                          child: Text(
                            item.label,
                            maxLines: 1,
                            style: Theme.of(context).textTheme.labelMedium
                                ?.copyWith(
                                  color: item.selected
                                      ? TeacherPanelView.navy
                                      : TeacherPanelView.slate,
                                  fontWeight: item.selected
                                      ? FontWeight.w800
                                      : FontWeight.w600,
                                  fontSize: 11,
                                ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            )
            .toList(),
      ),
    );
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
              color: TeacherPanelView.text,
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
                color: TeacherPanelView.navy,
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
              ? TeacherPanelView.skyBorder
              : Colors.white.withValues(alpha: 0.08),
        ),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x12082B4F),
            blurRadius: 24,
            offset: Offset(0, 10),
          ),
        ],
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

class _AvatarBadge extends StatelessWidget {
  const _AvatarBadge({
    required this.name,
    required this.accent,
    this.radius = 28,
  });

  final String name;
  final Color accent;
  final double radius;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: radius * 2,
      height: radius * 2,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            accent.withValues(alpha: 0.86),
            accent.withValues(alpha: 0.58),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        shape: BoxShape.circle,
      ),
      alignment: Alignment.center,
      child: Text(
        _initials(name),
        style: Theme.of(context).textTheme.titleSmall?.copyWith(
          color: Colors.white,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }

  String _initials(String value) {
    final parts = value.trim().split(RegExp(r'\s+'));
    if (parts.isEmpty) {
      return '??';
    }
    if (parts.length == 1) {
      return parts.first.substring(0, 1).toUpperCase();
    }
    return (parts.first.substring(0, 1) + parts.last.substring(0, 1))
        .toUpperCase();
  }
}

class _HeaderIconButton extends StatelessWidget {
  const _HeaderIconButton({required this.icon, this.badgeText, this.onTap});

  final IconData icon;
  final String? badgeText;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Stack(
        clipBehavior: Clip.none,
        children: <Widget>[
          Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: TeacherPanelView.skyBorder),
            ),
            child: Icon(icon, color: TeacherPanelView.text),
          ),
          if (badgeText != null)
            Positioned(
              top: -3,
              right: -3,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                decoration: BoxDecoration(
                  color: TeacherPanelView.emerald,
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  badgeText!,
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ),
        ],
      ),
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
            color: TeacherPanelView.navy,
            background: TeacherPanelView.sky,
          ),
          const SizedBox(height: 12),
          Text(
            title,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: TeacherPanelView.text,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            subtitle,
            textAlign: TextAlign.center,
            style: Theme.of(
              context,
            ).textTheme.bodyMedium?.copyWith(color: TeacherPanelView.slate),
          ),
        ],
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

class _BottomNavItem {
  const _BottomNavItem(this.icon, this.label, this.selected, [this.onTap]);

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback? onTap;
}
