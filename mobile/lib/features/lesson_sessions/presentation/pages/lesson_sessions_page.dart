import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_detail_page.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_state.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/widgets/lesson_form_sheet.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_cubit.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class LessonSessionsPage extends StatefulWidget {
  const LessonSessionsPage({super.key, this.openCreateOnStart = false});

  final bool openCreateOnStart;

  @override
  State<LessonSessionsPage> createState() => _LessonSessionsPageState();
}

class _LessonSessionsPageState extends State<LessonSessionsPage> {
  static const _tabs = <String>['Yaklasan', 'Gecmis', 'Iptal Edilen'];

  int _selectedTab = 0;
  bool _didOpenInitialCreate = false;

  @override
  Widget build(BuildContext context) {
    final session = context.select((AuthCubit cubit) => cubit.state.session);
    final teacherUserId = session?.userId ?? '';
    final teacherName = session?.fullName.trim().isNotEmpty == true
        ? session!.fullName
        : 'Demo ogretmen';

    if (widget.openCreateOnStart && !_didOpenInitialCreate) {
      _didOpenInitialCreate = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) {
          return;
        }
        _showCreateLessonSheet(context: context, teacherUserId: teacherUserId);
      });
    }

    return MultiBlocProvider(
      providers: [
        BlocProvider<SchedulingCubit>(
          create: (_) =>
              SchedulingCubit.create()..loadForCalendar(teacherUserId),
        ),
        BlocProvider<StudentsCubit>(
          create: (_) => StudentsCubit.create()..load(teacherUserId),
        ),
      ],
      child: Builder(
        builder: (ctx) => Scaffold(
          backgroundColor: AppColors.background,
          body: SafeArea(
            child: Padding(
              padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
              child: Column(
                children: <Widget>[
                  AppPageHeader(title: teacherName, subtitle: 'Dersler'),
                  const SizedBox(height: 12),
                  _EgittimUssuTabBar(
                    tabs: _tabs,
                    selectedIndex: _selectedTab,
                    onChanged: (index) => setState(() => _selectedTab = index),
                  ),
                  const SizedBox(height: 16),
                  Expanded(
                    child: BlocBuilder<SchedulingCubit, SchedulingState>(
                      builder: (context, schedulingState) =>
                          BlocBuilder<StudentsCubit, StudentsState>(
                            builder: (context, studentsState) =>
                                AnimatedSwitcher(
                                  duration: const Duration(milliseconds: 180),
                                  switchInCurve: Curves.easeOut,
                                  switchOutCurve: Curves.easeOut,
                                  child: KeyedSubtree(
                                    key: ValueKey<int>(_selectedTab),
                                    child: _buildContent(
                                      schedulingState,
                                      studentsState,
                                    ),
                                  ),
                                ),
                          ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          floatingActionButton: FloatingActionButton.extended(
            backgroundColor: AppColors.primary,
            foregroundColor: Colors.white,
            onPressed: () => _showCreateLessonSheet(
              context: ctx,
              teacherUserId: teacherUserId,
            ),
            icon: const Icon(Icons.add_rounded),
            label: const Text('Ders Ekle'),
          ),
          bottomNavigationBar: Container(
            decoration: const BoxDecoration(
              color: Colors.white,
              border: Border(top: BorderSide(color: AppColors.divider)),
            ),
            padding: EdgeInsets.fromLTRB(
              10,
              8,
              10,
              MediaQuery.of(context).padding.bottom + 8,
            ),
            child: Row(
              children: <Widget>[
                _BottomNavItem(
                  icon: Icons.home_rounded,
                  label: 'Ana sayfa',
                  selected: false,
                  onTap: () => context.go('/dashboard'),
                ),
                const _BottomNavItem(
                  icon: Icons.menu_book_rounded,
                  label: 'Dersler',
                  selected: true,
                ),
                _BottomNavItem(
                  icon: Icons.groups_rounded,
                  label: 'Ogrenciler',
                  selected: false,
                  onTap: () => context.go('/students'),
                ),
                _BottomNavItem(
                  icon: Icons.calendar_month_rounded,
                  label: 'Takvim',
                  selected: false,
                  onTap: () => context.go('/scheduling'),
                ),
                _BottomNavItem(
                  icon: Icons.account_balance_wallet_rounded,
                  label: 'Finans',
                  selected: false,
                  onTap: () => context.go('/payments'),
                ),
                _BottomNavItem(
                  icon: Icons.widgets_rounded,
                  label: 'Diger',
                  selected: false,
                  onTap: () => context.go('/more'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildContent(
    SchedulingState schedulingState,
    StudentsState studentsState,
  ) {
    if (schedulingState.isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    final now = DateTime.now().toUtc();
    final namesById = <String, String>{
      for (final s in studentsState.students) s.id: s.fullName,
    };

    final upcoming =
        schedulingState.lessons
            .where((l) => l.startAtUtc.isAfter(now) && l.status != 'Cancelled')
            .toList()
          ..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));

    final past =
        schedulingState.lessons
            .where((l) => !l.startAtUtc.isAfter(now) && l.status != 'Cancelled')
            .toList()
          ..sort((a, b) => b.startAtUtc.compareTo(a.startAtUtc));

    final cancelled =
        schedulingState.lessons.where((l) => l.status == 'Cancelled').toList()
          ..sort((a, b) => b.startAtUtc.compareTo(a.startAtUtc));

    switch (_selectedTab) {
      case 0:
        return _DateGroupedLessonsView(
          lessons: upcoming.map((l) => _toCardData(l, namesById)).toList(),
        );
      case 1:
        return _LessonListView(
          lessons: past.map((l) => _toCardData(l, namesById)).toList(),
        );
      case 2:
        return _LessonListView(
          lessons: cancelled
              .map((l) => _toCardData(l, namesById, detail: 'Iptal edildi'))
              .toList(),
        );
      default:
        return const SizedBox.shrink();
    }
  }

  _LessonCardData _toCardData(
    LessonSchedule l,
    Map<String, String> namesById, {
    String? detail,
  }) {
    final studentName = namesById[l.studentId] ?? 'Ogrenci';
    final isOnline = l.lessonFormat.toLowerCase().contains('online');
    final start = l.startAtUtc.toLocal();
    final end = l.endAtUtc.toLocal();
    final fmt = (DateTime dt) =>
        '${dt.hour.toString().padLeft(2, '0')}:${dt.minute.toString().padLeft(2, '0')}';
    final modeStr = isOnline ? 'Online' : 'Yuz yuze';
    final levelAndMode = (l.locationLabel != null && l.locationLabel != modeStr)
        ? '$modeStr · ${l.locationLabel}'
        : modeStr;
    return _LessonCardData(
      student: studentName,
      subject: l.subject,
      timeRange: '${fmt(start)} - ${fmt(end)}',
      levelAndMode: levelAndMode,
      date: _LessonDate(start.year, start.month, start.day),
      accent: _accentForStudentId(l.studentId),
      detail: detail,
      isOnline: isOnline,
      meetingUrl: l.meetingUrl,
      lesson: l,
    );
  }

  static Color _accentForStudentId(String studentId) {
    const colors = <Color>[
      AppColors.accentBlue,
      AppColors.accentGreen,
      AppColors.amber,
      AppColors.accentTeal,
      AppColors.accentRed,
    ];
    return colors[studentId.hashCode.abs() % colors.length];
  }

  Future<void> _showCreateLessonSheet({
    required BuildContext context,
    required String teacherUserId,
  }) async {
    final schedulingCubit = context.read<SchedulingCubit>();
    final students = context.read<StudentsCubit>().state.students;
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (sheetContext) {
        return BlocProvider<SchedulingCubit>.value(
          value: schedulingCubit,
          child: LessonFormSheet(
            teacherUserId: teacherUserId,
            students: students,
            existingLessons: schedulingCubit.state.lessons,
          ),
        );
      },
    );
    if (mounted) {
      schedulingCubit.loadForCalendar(teacherUserId);
    }
  }
}

class _EgittimUssuTabBar extends StatelessWidget {
  const _EgittimUssuTabBar({
    required this.tabs,
    required this.selectedIndex,
    required this.onChanged,
  });

  final List<String> tabs;
  final int selectedIndex;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 38,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: AppColors.tabBackground,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: List<Widget>.generate(tabs.length, (index) {
          final isSelected = index == selectedIndex;
          return Expanded(
            child: GestureDetector(
              onTap: () => onChanged(index),
              behavior: HitTestBehavior.opaque,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                curve: Curves.easeOut,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: isSelected ? AppColors.primary : Colors.transparent,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  tabs[index],
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: isSelected ? FontWeight.w600 : FontWeight.w500,
                    color: isSelected ? Colors.white : AppColors.textSecondary,
                  ),
                ),
              ),
            ),
          );
        }),
      ),
    );
  }
}

class _DateGroupedLessonsView extends StatelessWidget {
  const _DateGroupedLessonsView({required this.lessons});

  final List<_LessonCardData> lessons;

  @override
  Widget build(BuildContext context) {
    final grouped = <_LessonDate, List<_LessonCardData>>{};
    for (final lesson in lessons) {
      grouped.putIfAbsent(lesson.date, () => <_LessonCardData>[]).add(lesson);
    }

    final dates = grouped.keys.toList()
      ..sort((a, b) => a.asDateTime.compareTo(b.asDateTime));

    return ListView.builder(
      padding: const EdgeInsets.only(bottom: 120),
      itemCount: dates.length,
      itemBuilder: (context, index) {
        final date = dates[index];
        final items = grouped[date]!;
        return Padding(
          padding: EdgeInsets.only(bottom: index == dates.length - 1 ? 0 : 20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                date.formattedLabel,
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 12),
              ...List<Widget>.generate(items.length, (itemIndex) {
                final lesson = items[itemIndex];
                return Padding(
                  padding: EdgeInsets.only(
                    bottom: itemIndex == items.length - 1 ? 0 : 12,
                  ),
                  child: _LessonTimelineCard(
                    data: lesson,
                    onTap: () => _openDetail(context, lesson),
                  ),
                );
              }),
            ],
          ),
        );
      },
    );
  }

  void _openDetail(BuildContext context, _LessonCardData lesson) {
    context.push(
      '/lesson-sessions/detail',
      extra: LessonDetailPayload(
        studentName: lesson.student,
        subject: lesson.subject,
        dateLabel: lesson.date.formattedLabel,
        timeLabel: lesson.timeRange,
        modeLabel: lesson.isOnline ? 'Online' : 'Yuz yuze',
        accent: lesson.accent,
        meetingUrl: lesson.meetingUrl,
        lessonId: lesson.lesson?.id,
        lessonStatus: lesson.lesson?.status,
        lesson: lesson.lesson,
      ),
    );
  }
}

class _LessonListView extends StatelessWidget {
  const _LessonListView({required this.lessons});

  final List<_LessonCardData> lessons;

  @override
  Widget build(BuildContext context) {
    return ListView.separated(
      padding: const EdgeInsets.only(bottom: 120),
      itemCount: lessons.length,
      separatorBuilder: (_, __) => const SizedBox(height: 12),
      itemBuilder: (context, index) {
        return _LessonTimelineCard(
          data: lessons[index],
          showDate: true,
          onTap: () => _openDetail(context, lessons[index]),
        );
      },
    );
  }

  void _openDetail(BuildContext context, _LessonCardData lesson) {
    context.push(
      '/lesson-sessions/detail',
      extra: LessonDetailPayload(
        studentName: lesson.student,
        subject: lesson.subject,
        dateLabel: lesson.date.formattedLabel,
        timeLabel: lesson.timeRange,
        modeLabel: lesson.isOnline ? 'Online' : 'Yuz yuze',
        accent: lesson.accent,
        meetingUrl: lesson.meetingUrl,
        lessonId: lesson.lesson?.id,
        lessonStatus: lesson.lesson?.status,
        lesson: lesson.lesson,
      ),
    );
  }
}

class _LessonTimelineCard extends StatelessWidget {
  const _LessonTimelineCard({
    required this.data,
    this.showDate = false,
    this.onTap,
  });

  final _LessonCardData data;
  final bool showDate;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(22),
          border: Border.all(color: AppColors.border),
          boxShadow: AppShadows.soft,
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            _AvatarCircle(label: data.student, accent: data.accent),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    data.student,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    data.subject,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: AppColors.primary,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    showDate
                        ? '${data.date.formattedLabel} - ${data.timeRange}'
                        : data.timeRange,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    data.detail ?? data.levelAndMode,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            Icon(
              Icons.chevron_right_rounded,
              color: AppColors.textSecondary.withValues(alpha: 0.9),
              size: 28,
            ),
          ],
        ),
      ),
    );
  }
}

class _AvatarCircle extends StatelessWidget {
  const _AvatarCircle({required this.label, required this.accent});

  final String label;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    final parts = label.trim().split(RegExp(r'\s+'));
    final initials = parts.length > 1
        ? '${parts.first[0]}${parts.last[0]}'.toUpperCase()
        : label.substring(0, 1).toUpperCase();
    return Container(
      width: 52,
      height: 52,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            accent.withValues(alpha: 0.92),
            accent.withValues(alpha: 0.62),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        shape: BoxShape.circle,
      ),
      alignment: Alignment.center,
      child: Text(
        initials,
        style: Theme.of(context).textTheme.titleSmall?.copyWith(
          color: Colors.white,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class _BottomNavItem extends StatelessWidget {
  const _BottomNavItem({
    required this.icon,
    required this.label,
    required this.selected,
    this.onTap,
  });

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: InkWell(
        borderRadius: BorderRadius.circular(18),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 8),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Icon(
                icon,
                color: selected ? AppColors.primary : AppColors.textSecondary,
              ),
              const SizedBox(height: 4),
              FittedBox(
                fit: BoxFit.scaleDown,
                child: Text(
                  label,
                  maxLines: 1,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: selected
                        ? AppColors.primary
                        : AppColors.textSecondary,
                    fontWeight: selected ? FontWeight.w800 : FontWeight.w600,
                    fontSize: 11,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _LessonCardData {
  const _LessonCardData({
    required this.student,
    required this.subject,
    required this.timeRange,
    required this.levelAndMode,
    required this.date,
    required this.accent,
    this.detail,
    this.isOnline = false,
    this.meetingUrl,
    this.lesson,
  });

  final String student;
  final String subject;
  final String timeRange;
  final String levelAndMode;
  final _LessonDate date;
  final Color accent;
  final String? detail;
  final bool isOnline;
  final String? meetingUrl;
  final LessonSchedule? lesson;
}

class _LessonDate {
  const _LessonDate(this.year, this.month, this.day);

  final int year;
  final int month;
  final int day;

  static const _months = <String>[
    '',
    'Ocak',
    'Subat',
    'Mart',
    'Nisan',
    'Mayis',
    'Haziran',
    'Temmuz',
    'Agustos',
    'Eylul',
    'Ekim',
    'Kasim',
    'Aralik',
  ];

  static const _weekdays = <String>[
    'Pazartesi',
    'Sali',
    'Carsamba',
    'Persembe',
    'Cuma',
    'Cumartesi',
    'Pazar',
  ];

  DateTime get asDateTime => DateTime(year, month, day);

  String get formattedLabel {
    final date = asDateTime;
    final weekday = _weekdays[date.weekday - 1];
    final monthName = _months[month];
    return '$day $monthName $year $weekday';
  }

  @override
  bool operator ==(Object other) {
    return other is _LessonDate &&
        other.year == year &&
        other.month == month &&
        other.day == day;
  }

  @override
  int get hashCode => Object.hash(year, month, day);
}
