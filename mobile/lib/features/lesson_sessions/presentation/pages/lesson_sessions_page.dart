import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_detail_page.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_state.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_cubit.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
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
  static const _navy = Color(0xFF062B52);
  static const _blue = Color(0xFF3D8BFF);
  static const _emerald = Color(0xFF20B486);
  static const _amber = Color(0xFFFFB84D);
  static const _red = Color(0xFFFF6B6B);
  static const _teal = Color(0xFF20A4A9);
  static const _text = Color(0xFF10233D);
  static const _slate = Color(0xFF7A8494);
  static const _background = Color(0xFFF4F8FC);
  static const _border = Color(0xFFE5EAF0);
  static const _tabBackground = Color(0xFFF3F5F8);
  static const _divider = Color(0xFFE5EEF7);

  static const _tabs = <String>['Yaklasan', 'Gecmis', 'Iptal Edilen'];

  static const _upcomingLessons = <_LessonCardData>[
    _LessonCardData(
      student: 'Zeynep Demir',
      subject: 'Matematik',
      timeRange: '10:00 - 11:00',
      levelAndMode: '12. Sinif - Yuz yuze',
      date: _LessonDate(2025, 5, 20),
      accent: _blue,
    ),
    _LessonCardData(
      student: 'Ali Yilmaz',
      subject: 'Fizik',
      timeRange: '13:30 - 14:30',
      levelAndMode: '11. Sinif - Online',
      date: _LessonDate(2025, 5, 20),
      accent: _amber,
    ),
    _LessonCardData(
      student: 'Merve Kaya',
      subject: 'Geometri',
      timeRange: '09:30 - 10:30',
      levelAndMode: '10. Sinif - Yuz yuze',
      date: _LessonDate(2025, 5, 21),
      accent: _teal,
    ),
    _LessonCardData(
      student: 'Ece Aydin',
      subject: 'Kimya',
      timeRange: '16:00 - 17:00',
      levelAndMode: '12. Sinif - Online',
      date: _LessonDate(2025, 5, 21),
      accent: _emerald,
    ),
    _LessonCardData(
      student: 'Asli Kara',
      subject: 'Biyoloji',
      timeRange: '18:30 - 19:30',
      levelAndMode: '11. Sinif - Online',
      date: _LessonDate(2025, 5, 21),
      accent: _blue,
    ),
  ];

  static const _pastLessons = <_LessonCardData>[
    _LessonCardData(
      student: 'Can Su',
      subject: 'Biyoloji',
      timeRange: '18:00 - 19:00',
      levelAndMode: '11. Sinif - Online',
      date: _LessonDate(2025, 5, 18),
      accent: _emerald,
    ),
    _LessonCardData(
      student: 'Derin Koc',
      subject: 'Matematik',
      timeRange: '14:00 - 15:00',
      levelAndMode: '12. Sinif - Yuz yuze',
      date: _LessonDate(2025, 5, 17),
      accent: _blue,
    ),
    _LessonCardData(
      student: 'Mina Aras',
      subject: 'Kimya',
      timeRange: '16:30 - 17:30',
      levelAndMode: '10. Sinif - Online',
      date: _LessonDate(2025, 5, 16),
      accent: _teal,
    ),
  ];

  static const _cancelledLessons = <_LessonCardData>[
    _LessonCardData(
      student: 'Ayse Nur',
      subject: 'Turkce',
      timeRange: '11:00 - 12:00',
      levelAndMode: '9. Sinif - Online',
      date: _LessonDate(2025, 5, 16),
      accent: _red,
      detail: 'Iptal nedeni: Ogrenci katilamadi',
    ),
    _LessonCardData(
      student: 'Mehmet Kaya',
      subject: 'Fizik',
      timeRange: '17:30 - 18:30',
      levelAndMode: '10. Sinif - Yuz yuze',
      date: _LessonDate(2025, 5, 15),
      accent: _red,
      detail: 'Iptal nedeni: Program cakismasi',
    ),
  ];

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

    return Scaffold(
      backgroundColor: _background,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
          child: Column(
            children: <Widget>[
              Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      teacherName,
                      style: Theme.of(context).textTheme.headlineSmall
                          ?.copyWith(color: _text, fontWeight: FontWeight.w800),
                    ),
                  ),
                  const _NotificationButton(border: _border, accent: _emerald),
                ],
              ),
              const SizedBox(height: 12),
              _EgittimUssuTabBar(
                tabs: _tabs,
                selectedIndex: _selectedTab,
                onChanged: (index) => setState(() => _selectedTab = index),
              ),
              const SizedBox(height: 16),
              Expanded(
                child: AnimatedSwitcher(
                  duration: const Duration(milliseconds: 180),
                  switchInCurve: Curves.easeOut,
                  switchOutCurve: Curves.easeOut,
                  child: KeyedSubtree(
                    key: ValueKey<int>(_selectedTab),
                    child: _buildContent(),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: _navy,
        foregroundColor: Colors.white,
        onPressed: () => _showCreateLessonSheet(
          context: context,
          teacherUserId: teacherUserId,
        ),
        icon: const Icon(Icons.add_rounded),
        label: const Text('Ders Ekle'),
      ),
      bottomNavigationBar: Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          border: Border(top: BorderSide(color: _divider)),
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
    );
  }

  Widget _buildContent() {
    switch (_selectedTab) {
      case 0:
        return const _DateGroupedLessonsView(lessons: _upcomingLessons);
      case 1:
        return const _LessonListView(lessons: _pastLessons);
      case 2:
        return const _LessonListView(lessons: _cancelledLessons);
      default:
        return const SizedBox.shrink();
    }
  }

  Future<void> _showCreateLessonSheet({
    required BuildContext context,
    required String teacherUserId,
  }) async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (sheetContext) {
        return MultiBlocProvider(
          providers: <BlocProvider<dynamic>>[
            BlocProvider<StudentsCubit>(
              create: (_) => StudentsCubit.create()..load(teacherUserId),
            ),
            BlocProvider<SchedulingCubit>(
              create: (_) => SchedulingCubit.create(),
            ),
          ],
          child: _CreateLessonSheet(teacherUserId: teacherUserId),
        );
      },
    );
  }
}

class _NotificationButton extends StatelessWidget {
  const _NotificationButton({required this.border, required this.accent});

  final Color border;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return Stack(
      clipBehavior: Clip.none,
      children: <Widget>[
        Container(
          width: 46,
          height: 46,
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: border),
          ),
          child: const Icon(
            Icons.notifications_none_rounded,
            color: _LessonSessionsPageState._text,
          ),
        ),
        Positioned(
          top: -3,
          right: -3,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
            decoration: BoxDecoration(
              color: accent,
              borderRadius: BorderRadius.circular(999),
            ),
            child: Text(
              '2',
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: Colors.white,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ),
      ],
    );
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
        color: _LessonSessionsPageState._tabBackground,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: _LessonSessionsPageState._border),
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
                  color: isSelected
                      ? _LessonSessionsPageState._navy
                      : Colors.transparent,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  tabs[index],
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: isSelected ? FontWeight.w600 : FontWeight.w500,
                    color: isSelected
                        ? Colors.white
                        : _LessonSessionsPageState._slate,
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
                  color: _LessonSessionsPageState._text,
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
        modeLabel: lesson.levelAndMode.contains('Online')
            ? 'Online'
            : 'Yuz yuze',
        accent: lesson.accent,
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
        modeLabel: lesson.levelAndMode.contains('Online')
            ? 'Online'
            : 'Yuz yuze',
        accent: lesson.accent,
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
          border: Border.all(color: _LessonSessionsPageState._border),
          boxShadow: const <BoxShadow>[
            BoxShadow(
              color: Color(0x12082B4F),
              blurRadius: 24,
              offset: Offset(0, 10),
            ),
          ],
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
                      color: _LessonSessionsPageState._text,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    data.subject,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: _LessonSessionsPageState._navy,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    showDate
                        ? '${data.date.formattedLabel} - ${data.timeRange}'
                        : data.timeRange,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _LessonSessionsPageState._slate,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    data.detail ?? data.levelAndMode,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _LessonSessionsPageState._slate,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            Icon(
              Icons.chevron_right_rounded,
              color: _LessonSessionsPageState._slate.withValues(alpha: 0.9),
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
                color: selected
                    ? _LessonSessionsPageState._navy
                    : _LessonSessionsPageState._slate,
              ),
              const SizedBox(height: 4),
              FittedBox(
                fit: BoxFit.scaleDown,
                child: Text(
                  label,
                  maxLines: 1,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: selected
                        ? _LessonSessionsPageState._navy
                        : _LessonSessionsPageState._slate,
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

enum _LessonCreateMode { single, recurring }

enum _LessonFormatOption { faceToFace, online }

enum _RecurrenceFrequency { daily, weekly, monthly, custom }

enum _WeekdayChoice {
  monday,
  tuesday,
  wednesday,
  thursday,
  friday,
  saturday,
  sunday,
}

class _CreateLessonSheet extends StatefulWidget {
  const _CreateLessonSheet({required this.teacherUserId});

  final String teacherUserId;

  @override
  State<_CreateLessonSheet> createState() => _CreateLessonSheetState();
}

class _CreateLessonSheetState extends State<_CreateLessonSheet> {
  static const _sheetText = Color(0xFF10233D);
  static const _sheetSlate = Color(0xFF7A8494);
  static const _sheetBorder = Color(0xFFE5EAF0);
  static const _sheetNavy = Color(0xFF062B52);
  static const _sheetBackground = Color(0xFFF8FAFC);

  final _formKey = GlobalKey<FormState>();
  final _notesController = TextEditingController();

  _LessonCreateMode _mode = _LessonCreateMode.single;
  _LessonFormatOption _format = _LessonFormatOption.faceToFace;
  _RecurrenceFrequency _recurrenceFrequency = _RecurrenceFrequency.weekly;
  StudentProfile? _selectedStudent;
  String? _selectedSubject;
  DateTime? _selectedDate;
  DateTime? _recurrenceEndDate;
  TimeOfDay? _startTime;
  TimeOfDay? _endTime;
  bool _repeatLesson = true;
  final Set<_WeekdayChoice> _selectedWeekdays = <_WeekdayChoice>{
    _WeekdayChoice.monday,
  };

  @override
  void dispose() {
    _notesController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return BlocConsumer<SchedulingCubit, SchedulingState>(
      listener: (context, schedulingState) {
        if (schedulingState.successMessage != null) {
          Navigator.of(context).pop();
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(schedulingState.successMessage!)),
          );
        }
      },
      builder: (context, schedulingState) {
        return BlocBuilder<StudentsCubit, StudentsState>(
          builder: (context, studentsState) {
            final students = studentsState.students;
            final selectedStudent =
                _selectedStudent != null &&
                    students.any(
                      (student) => student.id == _selectedStudent!.id,
                    )
                ? students.firstWhere(
                    (student) => student.id == _selectedStudent!.id,
                  )
                : (students.isNotEmpty ? students.first : null);

            if (_selectedStudent?.id != selectedStudent?.id) {
              WidgetsBinding.instance.addPostFrameCallback((_) {
                if (!mounted) {
                  return;
                }
                setState(() {
                  _selectedStudent = selectedStudent;
                  final subjects = _subjectsForStudent(selectedStudent);
                  if (!subjects.contains(_selectedSubject)) {
                    _selectedSubject = subjects.isNotEmpty
                        ? subjects.first
                        : null;
                  }
                });
              });
            }

            final subjects = _subjectsForStudent(selectedStudent);

            return SafeArea(
              child: Padding(
                padding: EdgeInsets.only(
                  left: 20,
                  right: 20,
                  top: 16,
                  bottom: MediaQuery.of(context).viewInsets.bottom + 20,
                ),
                child: SingleChildScrollView(
                  child: Form(
                    key: _formKey,
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Center(
                          child: Container(
                            width: 48,
                            height: 5,
                            decoration: BoxDecoration(
                              color: _sheetBorder,
                              borderRadius: BorderRadius.circular(999),
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),
                        Row(
                          children: <Widget>[
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: <Widget>[
                                  Text(
                                    'Ders Ekle',
                                    style: Theme.of(context)
                                        .textTheme
                                        .titleLarge
                                        ?.copyWith(
                                          color: _sheetText,
                                          fontWeight: FontWeight.w800,
                                        ),
                                  ),
                                  const SizedBox(height: 6),
                                  Text(
                                    'Ders detaylarini girerek yeni plan olustur.',
                                    style: Theme.of(context)
                                        .textTheme
                                        .bodyMedium
                                        ?.copyWith(color: _sheetSlate),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(width: 12),
                            InkWell(
                              borderRadius: BorderRadius.circular(14),
                              onTap: () => Navigator.of(context).pop(),
                              child: Container(
                                width: 40,
                                height: 40,
                                decoration: BoxDecoration(
                                  color: _sheetBackground,
                                  borderRadius: BorderRadius.circular(14),
                                  border: Border.all(color: _sheetBorder),
                                ),
                                child: const Icon(
                                  Icons.close_rounded,
                                  color: _sheetText,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 20),
                        _SheetLabel(text: 'Ders tipi'),
                        const SizedBox(height: 8),
                        _WideSegmentedControl<_LessonCreateMode>(
                          values: const <_LessonCreateMode>[
                            _LessonCreateMode.single,
                            _LessonCreateMode.recurring,
                          ],
                          selectedValue: _mode,
                          labelBuilder: (value) =>
                              value == _LessonCreateMode.single
                              ? 'Tek Ders'
                              : 'Tekrarli Ders',
                          onChanged: (value) {
                            setState(() {
                              _mode = value;
                              if (value == _LessonCreateMode.single) {
                                _repeatLesson = false;
                                _recurrenceEndDate = null;
                                _selectedWeekdays
                                  ..clear()
                                  ..add(_weekdayFromDate(_selectedDate));
                              } else {
                                _repeatLesson = true;
                              }
                            });
                          },
                        ),
                        const SizedBox(height: 16),
                        if (studentsState.errorMessage != null) ...<Widget>[
                          ErrorStateView(message: studentsState.errorMessage!),
                          const SizedBox(height: 12),
                        ],
                        if (schedulingState.errorMessage != null) ...<Widget>[
                          ErrorStateView(
                            message: schedulingState.errorMessage!,
                          ),
                          const SizedBox(height: 12),
                        ],
                        if (studentsState.isLoading && students.isEmpty)
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 16),
                            child: LoadingStateView(
                              message: 'Ogrenciler yukleniyor...',
                            ),
                          )
                        else ...<Widget>[
                          _SheetLabel(text: 'Ogrenci secimi'),
                          const SizedBox(height: 8),
                          DropdownButtonFormField<StudentProfile>(
                            initialValue: selectedStudent,
                            decoration: _inputDecoration('Ogrenci sec'),
                            items: students
                                .map(
                                  (student) => DropdownMenuItem<StudentProfile>(
                                    value: student,
                                    child: Text(student.fullName),
                                  ),
                                )
                                .toList(),
                            onChanged: (value) {
                              setState(() {
                                _selectedStudent = value;
                                final nextSubjects = _subjectsForStudent(value);
                                _selectedSubject = nextSubjects.isNotEmpty
                                    ? nextSubjects.first
                                    : null;
                              });
                            },
                            validator: (value) => value == null
                                ? 'Ogrenci secimi zorunlu.'
                                : null,
                          ),
                          const SizedBox(height: 16),
                          _SheetLabel(text: 'Ders secimi'),
                          const SizedBox(height: 8),
                          DropdownButtonFormField<String>(
                            initialValue: subjects.contains(_selectedSubject)
                                ? _selectedSubject
                                : null,
                            decoration: _inputDecoration('Ders sec'),
                            items: subjects
                                .map(
                                  (subject) => DropdownMenuItem<String>(
                                    value: subject,
                                    child: Text(subject),
                                  ),
                                )
                                .toList(),
                            onChanged: (value) {
                              setState(() => _selectedSubject = value);
                            },
                            validator: (value) =>
                                value == null ? 'Ders secimi zorunlu.' : null,
                          ),
                          const SizedBox(height: 16),
                          _SheetLabel(
                            text: _mode == _LessonCreateMode.recurring
                                ? 'Baslangic tarihi'
                                : 'Tarih secimi',
                          ),
                          const SizedBox(height: 8),
                          InkWell(
                            borderRadius: BorderRadius.circular(18),
                            onTap: _pickDate,
                            child: InputDecorator(
                              decoration: _inputDecoration('Tarih sec'),
                              child: Text(
                                _selectedDate == null
                                    ? 'Tarih sec'
                                    : _formatDate(_selectedDate!),
                                style: TextStyle(
                                  color: _selectedDate == null
                                      ? _sheetSlate
                                      : _sheetText,
                                  fontWeight: FontWeight.w500,
                                ),
                              ),
                            ),
                          ),
                          const SizedBox(height: 16),
                          if (_mode == _LessonCreateMode.recurring) ...<Widget>[
                            SwitchListTile.adaptive(
                              value: _repeatLesson,
                              contentPadding: EdgeInsets.zero,
                              activeTrackColor: _sheetNavy.withValues(
                                alpha: 0.24,
                              ),
                              activeThumbColor: _sheetNavy,
                              title: Text(
                                'Tekrar etsin',
                                style: Theme.of(context).textTheme.titleSmall
                                    ?.copyWith(
                                      color: _sheetText,
                                      fontWeight: FontWeight.w700,
                                    ),
                              ),
                              subtitle: Text(
                                'Ayni dersi belirlenen duzende otomatik olustur.',
                                style: Theme.of(context).textTheme.bodySmall
                                    ?.copyWith(color: _sheetSlate),
                              ),
                              onChanged: (value) {
                                setState(() => _repeatLesson = value);
                              },
                            ),
                            if (_repeatLesson) ...<Widget>[
                              _SheetLabel(text: 'Tekrar sikligi'),
                              const SizedBox(height: 8),
                              _WideSegmentedControl<_RecurrenceFrequency>(
                                values: const <_RecurrenceFrequency>[
                                  _RecurrenceFrequency.daily,
                                  _RecurrenceFrequency.weekly,
                                  _RecurrenceFrequency.monthly,
                                  _RecurrenceFrequency.custom,
                                ],
                                selectedValue: _recurrenceFrequency,
                                labelBuilder: _frequencyLabel,
                                onChanged: (value) {
                                  setState(() => _recurrenceFrequency = value);
                                },
                                fontSize: 11,
                              ),
                              const SizedBox(height: 16),
                              _SheetLabel(text: 'Bitis tarihi'),
                              const SizedBox(height: 8),
                              InkWell(
                                borderRadius: BorderRadius.circular(18),
                                onTap: _pickRecurrenceEndDate,
                                child: InputDecorator(
                                  decoration: _inputDecoration(
                                    'Bitis tarihi sec',
                                  ),
                                  child: Text(
                                    _recurrenceEndDate == null
                                        ? 'Bitis tarihi sec'
                                        : _formatDate(_recurrenceEndDate!),
                                    style: TextStyle(
                                      color: _recurrenceEndDate == null
                                          ? _sheetSlate
                                          : _sheetText,
                                      fontWeight: FontWeight.w500,
                                    ),
                                  ),
                                ),
                              ),
                              const SizedBox(height: 16),
                              _SheetLabel(text: 'Hangi gunler'),
                              const SizedBox(height: 8),
                              Wrap(
                                spacing: 8,
                                runSpacing: 8,
                                children: _WeekdayChoice.values.map((day) {
                                  final selected = _selectedWeekdays.contains(
                                    day,
                                  );
                                  return OutlinedButton(
                                    onPressed: () => _toggleWeekday(day),
                                    style: OutlinedButton.styleFrom(
                                      backgroundColor: selected
                                          ? _sheetNavy
                                          : Colors.white,
                                      foregroundColor: selected
                                          ? Colors.white
                                          : _sheetSlate,
                                      side: BorderSide(
                                        color: selected
                                            ? _sheetNavy
                                            : _sheetBorder,
                                      ),
                                      shape: RoundedRectangleBorder(
                                        borderRadius: BorderRadius.circular(
                                          999,
                                        ),
                                      ),
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 14,
                                        vertical: 10,
                                      ),
                                    ),
                                    child: Text(_weekdayShortLabel(day)),
                                  );
                                }).toList(),
                              ),
                              const SizedBox(height: 16),
                            ],
                          ],
                          Row(
                            children: <Widget>[
                              Expanded(
                                child: _PickerField(
                                  label: 'Baslangic saati',
                                  value: _startTime == null
                                      ? 'Saat sec'
                                      : _formatTime(_startTime!),
                                  onTap: () => _pickTime(isStart: true),
                                ),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: _PickerField(
                                  label: 'Bitis saati',
                                  value: _endTime == null
                                      ? 'Saat sec'
                                      : _formatTime(_endTime!),
                                  onTap: () => _pickTime(isStart: false),
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: 16),
                          _SheetLabel(text: 'Ders sekli'),
                          const SizedBox(height: 8),
                          _WideSegmentedControl<_LessonFormatOption>(
                            values: const <_LessonFormatOption>[
                              _LessonFormatOption.faceToFace,
                              _LessonFormatOption.online,
                            ],
                            selectedValue: _format,
                            labelBuilder: (value) =>
                                value == _LessonFormatOption.faceToFace
                                ? 'Yuz yuze'
                                : 'Online',
                            onChanged: (value) {
                              setState(() => _format = value);
                            },
                          ),
                          const SizedBox(height: 16),
                          _SheetLabel(text: 'Not'),
                          const SizedBox(height: 8),
                          TextFormField(
                            controller: _notesController,
                            maxLines: 3,
                            maxLength: 180,
                            decoration: _inputDecoration(
                              'Ders notu ekleyebilirsin (istege bagli)',
                            ),
                          ),
                          const SizedBox(height: 20),
                          SizedBox(
                            width: double.infinity,
                            child: FilledButton(
                              style: FilledButton.styleFrom(
                                backgroundColor: _sheetNavy,
                                foregroundColor: Colors.white,
                                padding: const EdgeInsets.symmetric(
                                  vertical: 14,
                                ),
                                shape: RoundedRectangleBorder(
                                  borderRadius: BorderRadius.circular(16),
                                ),
                              ),
                              onPressed: schedulingState.isSaving
                                  ? null
                                  : () => _submit(context),
                              child: schedulingState.isSaving
                                  ? const SizedBox(
                                      height: 20,
                                      width: 20,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                        valueColor:
                                            AlwaysStoppedAnimation<Color>(
                                              Colors.white,
                                            ),
                                      ),
                                    )
                                  : const Text('Dersi Kaydet'),
                            ),
                          ),
                          const SizedBox(height: 10),
                          Center(
                            child: TextButton(
                              onPressed: () => Navigator.of(context).pop(),
                              child: const Text('Formu kapat'),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
            );
          },
        );
      },
    );
  }

  List<String> _subjectsForStudent(StudentProfile? student) {
    if (student == null) {
      return const <String>[];
    }
    final subjects = student.subjects
        .map((item) => item.subject.trim())
        .where((item) {
          return item.isNotEmpty;
        })
        .toSet()
        .toList();
    if (subjects.isEmpty) {
      return const <String>['Matematik'];
    }
    return subjects;
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final initialDate = _selectedDate ?? now;
    final picked = await showDatePicker(
      context: context,
      initialDate: initialDate,
      firstDate: now.subtract(const Duration(days: 30)),
      lastDate: now.add(const Duration(days: 365)),
    );
    if (picked != null) {
      setState(() {
        _selectedDate = picked;
        if (_mode == _LessonCreateMode.recurring &&
            _selectedWeekdays.length <= 1) {
          _selectedWeekdays
            ..clear()
            ..add(_weekdayFromDate(picked));
        }
      });
    }
  }

  Future<void> _pickRecurrenceEndDate() async {
    final anchorDate = _selectedDate ?? DateTime.now();
    final initialDate =
        _recurrenceEndDate ?? anchorDate.add(const Duration(days: 30));
    final picked = await showDatePicker(
      context: context,
      initialDate: initialDate,
      firstDate: anchorDate,
      lastDate: anchorDate.add(const Duration(days: 365 * 2)),
    );
    if (picked != null) {
      setState(() => _recurrenceEndDate = picked);
    }
  }

  Future<void> _pickTime({required bool isStart}) async {
    final initialTime = isStart
        ? (_startTime ?? const TimeOfDay(hour: 10, minute: 0))
        : (_endTime ?? const TimeOfDay(hour: 11, minute: 0));
    final picked = await showTimePicker(
      context: context,
      initialTime: initialTime,
    );
    if (picked != null) {
      setState(() {
        if (isStart) {
          _startTime = picked;
        } else {
          _endTime = picked;
        }
      });
    }
  }

  Future<void> _submit(BuildContext context) async {
    final valid = _formKey.currentState?.validate() ?? false;
    if (!valid) {
      return;
    }
    if (_selectedDate == null) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Tarih secimi zorunlu.')));
      return;
    }
    if (_startTime == null || _endTime == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Baslangic ve bitis saati secilmeli.')),
      );
      return;
    }

    final startLocal = DateTime(
      _selectedDate!.year,
      _selectedDate!.month,
      _selectedDate!.day,
      _startTime!.hour,
      _startTime!.minute,
    );
    final endLocal = DateTime(
      _selectedDate!.year,
      _selectedDate!.month,
      _selectedDate!.day,
      _endTime!.hour,
      _endTime!.minute,
    );

    if (!endLocal.isAfter(startLocal)) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Bitis saati baslangic saatinden sonra olmali.'),
        ),
      );
      return;
    }

    if (_mode == _LessonCreateMode.recurring && _repeatLesson) {
      if (_recurrenceEndDate == null) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Tekrarli ders icin bitis tarihi sec.')),
        );
        return;
      }
      if (_recurrenceFrequency == _RecurrenceFrequency.weekly ||
          _recurrenceFrequency == _RecurrenceFrequency.custom) {
        if (_selectedWeekdays.isEmpty) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('En az bir gun secmelisin.')),
          );
          return;
        }
      }
    }

    final lesson = LessonSchedule(
      id: '',
      teacherUserId: widget.teacherUserId,
      studentId: _selectedStudent!.id,
      subject: _selectedSubject!,
      lessonFormat: _format == _LessonFormatOption.online
          ? 'Online'
          : 'FaceToFace',
      startAtUtc: startLocal.toUtc(),
      endAtUtc: endLocal.toUtc(),
      timeZone: 'Europe/Istanbul',
      recurrenceRule: _buildRecurrenceRule(),
      reminderOffsetMinutes: 60,
      locationLabel: _format == _LessonFormatOption.online
          ? 'Online'
          : 'Yuz yuze',
      notes: _notesController.text.trim().isEmpty
          ? null
          : _notesController.text.trim(),
    );

    await context.read<SchedulingCubit>().createLesson(lesson);
  }

  String? _buildRecurrenceRule() {
    if (_mode != _LessonCreateMode.recurring || !_repeatLesson) {
      return null;
    }

    final untilDate = _recurrenceEndDate;
    final until = untilDate == null
        ? ''
        : ';UNTIL=${DateTime(untilDate.year, untilDate.month, untilDate.day, 23, 59).toUtc().toIso8601String().replaceAll('-', '').replaceAll(':', '').split('.').first}Z';

    return switch (_recurrenceFrequency) {
      _RecurrenceFrequency.daily => 'FREQ=DAILY$until',
      _RecurrenceFrequency.weekly =>
        'FREQ=WEEKLY;BYDAY=${_selectedWeekdays.map(_weekdayRuleCode).join(',')}$until',
      _RecurrenceFrequency.monthly => 'FREQ=MONTHLY$until',
      _RecurrenceFrequency.custom =>
        'FREQ=WEEKLY;INTERVAL=2;BYDAY=${_selectedWeekdays.map(_weekdayRuleCode).join(',')}$until',
    };
  }

  void _toggleWeekday(_WeekdayChoice day) {
    setState(() {
      if (_selectedWeekdays.contains(day)) {
        if (_selectedWeekdays.length > 1) {
          _selectedWeekdays.remove(day);
        }
      } else {
        _selectedWeekdays.add(day);
      }
    });
  }

  String _frequencyLabel(_RecurrenceFrequency value) {
    return switch (value) {
      _RecurrenceFrequency.daily => 'Gunluk',
      _RecurrenceFrequency.weekly => 'Haftalik',
      _RecurrenceFrequency.monthly => 'Aylik',
      _RecurrenceFrequency.custom => 'Ozel',
    };
  }

  String _weekdayShortLabel(_WeekdayChoice value) {
    return switch (value) {
      _WeekdayChoice.monday => 'Pzt',
      _WeekdayChoice.tuesday => 'Sali',
      _WeekdayChoice.wednesday => 'Car',
      _WeekdayChoice.thursday => 'Per',
      _WeekdayChoice.friday => 'Cum',
      _WeekdayChoice.saturday => 'Cmt',
      _WeekdayChoice.sunday => 'Paz',
    };
  }

  _WeekdayChoice _weekdayFromDate(DateTime? date) {
    final weekday = (date ?? DateTime.now()).weekday;
    return _WeekdayChoice.values[weekday - 1];
  }

  String _weekdayRuleCode(_WeekdayChoice value) {
    return switch (value) {
      _WeekdayChoice.monday => 'MO',
      _WeekdayChoice.tuesday => 'TU',
      _WeekdayChoice.wednesday => 'WE',
      _WeekdayChoice.thursday => 'TH',
      _WeekdayChoice.friday => 'FR',
      _WeekdayChoice.saturday => 'SA',
      _WeekdayChoice.sunday => 'SU',
    };
  }

  InputDecoration _inputDecoration(String hintText) {
    return InputDecoration(
      hintText: hintText,
      filled: true,
      fillColor: Colors.white,
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(18),
        borderSide: const BorderSide(color: _sheetBorder),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(18),
        borderSide: const BorderSide(color: _sheetNavy),
      ),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(18),
        borderSide: const BorderSide(color: _sheetBorder),
      ),
    );
  }

  String _formatDate(DateTime date) {
    const months = <String>[
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
    const weekdays = <String>[
      'Pazartesi',
      'Sali',
      'Carsamba',
      'Persembe',
      'Cuma',
      'Cumartesi',
      'Pazar',
    ];
    return '${date.day} ${months[date.month]} ${date.year} ${weekdays[date.weekday - 1]}';
  }

  String _formatTime(TimeOfDay time) {
    final hour = time.hour.toString().padLeft(2, '0');
    final minute = time.minute.toString().padLeft(2, '0');
    return '$hour:$minute';
  }
}

class _SheetLabel extends StatelessWidget {
  const _SheetLabel({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: Theme.of(context).textTheme.labelLarge?.copyWith(
        color: _CreateLessonSheetState._sheetText,
        fontWeight: FontWeight.w700,
      ),
    );
  }
}

class _WideSegmentedControl<T> extends StatelessWidget {
  const _WideSegmentedControl({
    required this.values,
    required this.selectedValue,
    required this.labelBuilder,
    required this.onChanged,
    this.fontSize = 12,
  });

  final List<T> values;
  final T selectedValue;
  final String Function(T value) labelBuilder;
  final ValueChanged<T> onChanged;
  final double fontSize;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      height: 46,
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: _CreateLessonSheetState._sheetBackground,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: _CreateLessonSheetState._sheetBorder),
      ),
      child: Row(
        children: values.map((value) {
          final isSelected = value == selectedValue;
          return Expanded(
            child: GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: () => onChanged(value),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                curve: Curves.easeOut,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: isSelected
                      ? _CreateLessonSheetState._sheetNavy
                      : Colors.transparent,
                  borderRadius: BorderRadius.circular(10),
                ),
                padding: const EdgeInsets.symmetric(horizontal: 8),
                child: Text(
                  labelBuilder(value),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: fontSize,
                    fontWeight: isSelected ? FontWeight.w600 : FontWeight.w500,
                    color: isSelected
                        ? Colors.white
                        : _CreateLessonSheetState._sheetSlate,
                  ),
                ),
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}

class _PickerField extends StatelessWidget {
  const _PickerField({
    required this.label,
    required this.value,
    required this.onTap,
  });

  final String label;
  final String value;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        _SheetLabel(text: label),
        const SizedBox(height: 8),
        InkWell(
          borderRadius: BorderRadius.circular(18),
          onTap: onTap,
          child: InputDecorator(
            decoration: InputDecoration(
              hintText: value,
              filled: true,
              fillColor: Colors.white,
              contentPadding: const EdgeInsets.symmetric(
                horizontal: 16,
                vertical: 16,
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(18),
                borderSide: const BorderSide(
                  color: _CreateLessonSheetState._sheetBorder,
                ),
              ),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(18),
                borderSide: const BorderSide(
                  color: _CreateLessonSheetState._sheetBorder,
                ),
              ),
            ),
            child: Text(
              value,
              style: TextStyle(
                color: value == 'Saat sec'
                    ? _CreateLessonSheetState._sheetSlate
                    : _CreateLessonSheetState._sheetText,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
        ),
      ],
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
  });

  final String student;
  final String subject;
  final String timeRange;
  final String levelAndMode;
  final _LessonDate date;
  final Color accent;
  final String? detail;
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
