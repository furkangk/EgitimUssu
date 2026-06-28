import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_state.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/widgets/lesson_form_sheet.dart';
import 'package:egitim_ussu_mobile/features/students/presentation/cubit/students_cubit.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:syncfusion_flutter_calendar/calendar.dart';

enum _CalendarView { day, week, month }

enum _CalendarEventType { lesson, unavailable, assignment, payment }

class SchedulingPage extends StatefulWidget {
  const SchedulingPage({super.key});

  @override
  State<SchedulingPage> createState() => _SchedulingPageState();
}

class _SchedulingPageState extends State<SchedulingPage> {
  late final SchedulingCubit _schedulingCubit;
  late final StudentsCubit _studentsCubit;
  bool _loaded = false;

  late DateTime _visibleDate;
  late List<_CalendarEvent> _events;
  _CalendarView _view = _CalendarView.month;

  @override
  void initState() {
    super.initState();
    _schedulingCubit = SchedulingCubit.create();
    _studentsCubit = StudentsCubit.create();
    final now = DateTime.now();
    _visibleDate = DateTime(now.year, now.month, now.day);
    _events = _seedEvents(_visibleDate);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!_loaded) {
      _loaded = true;
      final userId = _authSessionOrNull(context)?.userId;
      if (userId != null) {
        _schedulingCubit.loadForCalendar(userId);
        _studentsCubit.load(userId);
      }
    }
  }

  @override
  void dispose() {
    _schedulingCubit.close();
    _studentsCubit.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiBlocProvider(
      providers: <BlocProvider<dynamic>>[
        BlocProvider<SchedulingCubit>.value(value: _schedulingCubit),
        BlocProvider<StudentsCubit>.value(value: _studentsCubit),
      ],
      child: BlocListener<SchedulingCubit, SchedulingState>(
        listener: (context, state) {
          if (state.successMessage != null) {
            ScaffoldMessenger.of(
              context,
            ).showSnackBar(SnackBar(content: Text(state.successMessage!)));
          }
          if (state.errorMessage != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                backgroundColor: AppColors.accentRed,
                content: Text(state.errorMessage!),
              ),
            );
          }
        },
        child: _buildContent(context),
      ),
    );
  }

  Widget _buildContent(BuildContext context) {
    final session = _authSessionOrNull(context);
    final teacherName = session?.fullName.trim().isNotEmpty == true
        ? session!.fullName
        : 'Ahmet Bey';
    final title = switch (_view) {
      _CalendarView.day => _dayTitleLabel(_visibleDate),
      _CalendarView.week => _weekRangeLabel(_visibleDate),
      _CalendarView.month => _monthYearLabel(_visibleDate),
    };

    return Scaffold(
      backgroundColor: AppColors.background,
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: AppColors.primary,
        foregroundColor: Colors.white,
        onPressed: _showCreateSheet,
        icon: const Icon(Icons.add_rounded),
        label: const Text('Ders Ekle'),
      ),
      body: SafeArea(
        child: CustomScrollView(
          slivers: <Widget>[
            SliverToBoxAdapter(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    AppPageHeader(title: teacherName, subtitle: 'Takvim'),
                    const SizedBox(height: 12),
                    _ViewSwitcher(
                      value: _view,
                      onChanged: (value) => setState(() => _view = value),
                    ),
                    const SizedBox(height: 14),
                    _DateNavigator(
                      label: title,
                      onPrevious: () => _moveVisibleDate(-1),
                      onToday: () => setState(() {
                        final now = DateTime.now();
                        _visibleDate = DateTime(now.year, now.month, now.day);
                      }),
                      onNext: () => _moveVisibleDate(1),
                    ),
                    const SizedBox(height: 16),
                  ],
                ),
              ),
            ),
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(16, 0, 16, 110),
              sliver: SliverToBoxAdapter(child: _buildCurrentView()),
            ),
          ],
        ),
      ),
      bottomNavigationBar: _SchedulingBottomNav(
        onHomeTap: () => context.go('/dashboard'),
        onLessonsTap: () => context.go('/lesson-sessions'),
        onStudentsTap: () => context.go('/students'),
        onMoreTap: () => context.go('/more'),
        onFinanceTap: () => context.go('/payments'),
      ),
    );
  }

  dynamic _authSessionOrNull(BuildContext context) {
    try {
      return context.read<AuthCubit>().state.session;
    } on ProviderNotFoundException {
      return null;
    }
  }

  Widget _buildCurrentView() {
    return _SyncfusionCalendarSurface(
      view: _view,
      visibleDate: _visibleDate,
      events: _calendarEvents,
      studentNameFor: _studentNameFor,
      onVisibleDateChanged: (date) => setState(() => _visibleDate = date),
      selectedDateEvents: _eventsForDay(_visibleDate),
    );
  }

  List<_CalendarEvent> get _calendarEvents {
    return <_CalendarEvent>[
      ..._schedulingCubit.state.lessons
          .where((l) => l.status != 'Cancelled')
          .map(_CalendarEvent.fromLesson),
      ..._events,
    ]..sort((a, b) => a.start.compareTo(b.start));
  }

  List<_CalendarEvent> _eventsForDay(DateTime date) {
    return _calendarEvents
        .where((event) => _sameDay(event.start, date))
        .toList()
      ..sort((a, b) => a.start.compareTo(b.start));
  }

  void _moveVisibleDate(int direction) {
    setState(() {
      _visibleDate = switch (_view) {
        _CalendarView.day => _visibleDate.add(Duration(days: direction)),
        _CalendarView.week => _visibleDate.add(Duration(days: direction * 7)),
        _CalendarView.month => DateTime(
          _visibleDate.year,
          _visibleDate.month + direction,
          1,
        ),
      };
    });
  }

  Future<void> _showCreateSheet() async {
    final teacherUserId = _authSessionOrNull(context)?.userId ?? '';
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (sheetContext) {
        return BlocProvider<SchedulingCubit>.value(
          value: _schedulingCubit,
          child: LessonFormSheet(
            teacherUserId: teacherUserId,
            students: _studentsCubit.state.students,
            existingLessons: _schedulingCubit.state.lessons,
            initialDate: _visibleDate,
          ),
        );
      },
    );
    if (mounted) {
      setState(() {});
    }
  }

  String _studentNameFor(String id) {
    final found = _studentsCubit.state.students
        .where((s) => s.id == id)
        .firstOrNull;
    return found?.fullName ?? id;
  }

  String _weekRangeLabel(DateTime date) {
    final start = _startOfWeek(date);
    final end = start.add(const Duration(days: 6));
    return '${_shortDateLabel(start)} - ${_shortDateLabel(end, includeYear: true)}';
  }

  static DateTime _startOfWeek(DateTime date) {
    final day = DateTime(date.year, date.month, date.day);
    return day.subtract(Duration(days: day.weekday - 1));
  }
}

class _SoftActionMenu extends StatelessWidget {
  const _SoftActionMenu({required this.onEdit});

  final VoidCallback onEdit;

  @override
  Widget build(BuildContext context) {
    return PopupMenuButton<String>(
      color: Colors.white,
      elevation: 12,
      shadowColor: Colors.black.withValues(alpha: 0.12),
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
      offset: const Offset(-8, 42),
      onSelected: (value) {
        if (value == 'edit') {
          onEdit();
        }
      },
      itemBuilder: (context) => <PopupMenuEntry<String>>[
        PopupMenuItem<String>(
          value: 'edit',
          child: Row(
            children: const <Widget>[
              Icon(Icons.edit_rounded, size: 18, color: AppColors.primary),
              SizedBox(width: 10),
              Text('Duzenle'),
            ],
          ),
        ),
        PopupMenuItem<String>(
          value: 'delete',
          child: Row(
            children: const <Widget>[
              Icon(
                Icons.delete_outline_rounded,
                size: 18,
                color: AppColors.accentRed,
              ),
              SizedBox(width: 10),
              Text('Sil'),
            ],
          ),
        ),
      ],
      icon: Container(
        width: 34,
        height: 34,
        decoration: BoxDecoration(
          color: AppColors.background,
          borderRadius: BorderRadius.circular(12),
        ),
        child: const Icon(
          Icons.more_horiz_rounded,
          color: AppColors.textSecondary,
          size: 20,
        ),
      ),
    );
  }
}

class _ViewSwitcher extends StatelessWidget {
  const _ViewSwitcher({required this.value, required this.onChanged});

  final _CalendarView value;
  final ValueChanged<_CalendarView> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children:
            const <_CalendarView>[
              _CalendarView.month,
              _CalendarView.week,
              _CalendarView.day,
            ].map((view) {
              final selected = view == value;
              final label = switch (view) {
                _CalendarView.day => 'Gunluk',
                _CalendarView.week => 'Haftalik',
                _CalendarView.month => 'Aylik',
              };
              return Expanded(
                child: InkWell(
                  borderRadius: BorderRadius.circular(12),
                  onTap: () => onChanged(view),
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 180),
                    height: 42,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: selected ? AppColors.primary : Colors.white,
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Text(
                      label,
                      style: Theme.of(context).textTheme.labelMedium?.copyWith(
                        color: selected
                            ? Colors.white
                            : AppColors.textSecondary,
                        fontWeight: FontWeight.w800,
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

class _DateNavigator extends StatelessWidget {
  const _DateNavigator({
    required this.label,
    required this.onPrevious,
    required this.onToday,
    required this.onNext,
  });

  final String label;
  final VoidCallback onPrevious;
  final VoidCallback onToday;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        _IconSurface(icon: Icons.chevron_left_rounded, onTap: onPrevious),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            label,
            textAlign: TextAlign.center,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        const SizedBox(width: 10),
        OutlinedButton(
          onPressed: onToday,
          style: OutlinedButton.styleFrom(
            minimumSize: const Size(70, 42),
            side: const BorderSide(color: AppColors.border),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
          ),
          child: const Text('Bugün'),
        ),
        const SizedBox(width: 10),
        _IconSurface(icon: Icons.chevron_right_rounded, onTap: onNext),
      ],
    );
  }
}

class _SyncfusionCalendarSurface extends StatelessWidget {
  const _SyncfusionCalendarSurface({
    required this.view,
    required this.visibleDate,
    required this.events,
    required this.studentNameFor,
    required this.onVisibleDateChanged,
    required this.selectedDateEvents,
  });

  final _CalendarView view;
  final DateTime visibleDate;
  final List<_CalendarEvent> events;
  final String Function(String id) studentNameFor;
  final ValueChanged<DateTime> onVisibleDateChanged;
  final List<_CalendarEvent> selectedDateEvents;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          height: _heightFor(view, MediaQuery.sizeOf(context).height),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: AppColors.border),
            boxShadow: AppShadows.soft,
          ),
          clipBehavior: Clip.antiAlias,
          child: SfCalendar(
            key: ValueKey<String>('calendar-${view.name}-${visibleDate.month}'),
            view: _syncfusionView,
            dataSource: _LessonCalendarDataSource(events),
            initialDisplayDate: visibleDate,
            initialSelectedDate: visibleDate,
            todayHighlightColor: AppColors.primary,
            selectionDecoration: BoxDecoration(
              color: Colors.transparent,
              border: Border.all(color: AppColors.primary, width: 1.5),
              borderRadius: BorderRadius.circular(10),
            ),
            headerHeight: 0,
            backgroundColor: Colors.white,
            cellBorderColor: AppColors.border,
            firstDayOfWeek: 1,
            showCurrentTimeIndicator: true,
            monthViewSettings: const MonthViewSettings(
              appointmentDisplayMode: MonthAppointmentDisplayMode.appointment,
              showAgenda: false,
            ),
            timeSlotViewSettings: const TimeSlotViewSettings(
              startHour: 8,
              endHour: 22,
              timeIntervalHeight: 62,
              timeFormat: 'HH:mm',
              dayFormat: 'EEE',
              dateFormat: 'dd',
            ),
            appointmentTextStyle:
                Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: Colors.white,
                  fontWeight: FontWeight.w800,
                ) ??
                const TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.w800,
                ),
            onTap: (details) {
              final appointment = details.appointments?.isNotEmpty == true
                  ? details.appointments!.first
                  : null;
              final selectedDate = appointment is _CalendarEvent
                  ? appointment.start
                  : details.date;
              if (selectedDate != null) {
                onVisibleDateChanged(
                  DateTime(
                    selectedDate.year,
                    selectedDate.month,
                    selectedDate.day,
                  ),
                );
              }
            },
            onViewChanged: (details) {
              if (details.visibleDates.isEmpty) {
                return;
              }
              final nextDate = _anchorDateFor(details.visibleDates);
              if (!_sameDay(nextDate, visibleDate)) {
                WidgetsBinding.instance.addPostFrameCallback((_) {
                  onVisibleDateChanged(nextDate);
                });
              }
            },
          ),
        ),
        const SizedBox(height: 16),
        _SelectedDayEventsPanel(
          date: visibleDate,
          events: selectedDateEvents,
          studentNameFor: studentNameFor,
        ),
      ],
    );
  }

  CalendarView get _syncfusionView {
    return switch (view) {
      _CalendarView.day => CalendarView.day,
      _CalendarView.week => CalendarView.week,
      _CalendarView.month => CalendarView.month,
    };
  }

  static double _heightFor(_CalendarView view, double screenHeight) {
    final available = screenHeight - 280;
    return switch (view) {
      _CalendarView.day => available.clamp(430, 620).toDouble(),
      _CalendarView.week => available.clamp(430, 620).toDouble(),
      _CalendarView.month => available.clamp(430, 560).toDouble(),
    };
  }

  static DateTime _anchorDateFor(List<DateTime> visibleDates) {
    return visibleDates[visibleDates.length ~/ 2];
  }
}

class _LessonCalendarDataSource extends CalendarDataSource {
  _LessonCalendarDataSource(List<_CalendarEvent> source) {
    appointments = source;
  }

  @override
  DateTime getStartTime(int index) => _eventAt(index).start;

  @override
  DateTime getEndTime(int index) => _eventAt(index).end;

  @override
  String getSubject(int index) {
    final event = _eventAt(index);
    return _eventShortLabel(event.type);
  }

  @override
  Color getColor(int index) => _eventColor(_eventAt(index).type);

  @override
  bool isAllDay(int index) => _eventAt(index).isAllDay;

  _CalendarEvent _eventAt(int index) {
    return appointments![index] as _CalendarEvent;
  }
}

class _SelectedDayEventsPanel extends StatelessWidget {
  const _SelectedDayEventsPanel({
    required this.date,
    required this.events,
    required this.studentNameFor,
  });

  final DateTime date;
  final List<_CalendarEvent> events;
  final String Function(String id) studentNameFor;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            _selectedDateLabel(date),
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w900,
            ),
          ),
          const SizedBox(height: 12),
          if (events.isEmpty)
            Text(
              'Bu güne ait etkinlik yok.',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: AppColors.textSecondary,
                fontWeight: FontWeight.w600,
              ),
            )
          else
            ...events.map((event) {
              final student = event.studentId == null
                  ? null
                  : studentNameFor(event.studentId!);
              final timeLabel = event.isAllDay
                  ? 'Tüm gün'
                  : DateFormat('HH:mm').format(event.start);
              return Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: _SelectedDayEventTile(
                  label: _eventShortLabel(event.type),
                  title: event.title,
                  subtitle: student,
                  timeLabel: timeLabel,
                  color: _eventColor(event.type),
                ),
              );
            }),
        ],
      ),
    );
  }
}

class _SelectedDayEventTile extends StatelessWidget {
  const _SelectedDayEventTile({
    required this.label,
    required this.title,
    required this.timeLabel,
    required this.color,
    this.subtitle,
  });

  final String label;
  final String title;
  final String? subtitle;
  final String timeLabel;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          width: 4,
          height: 48,
          decoration: BoxDecoration(
            color: color,
            borderRadius: BorderRadius.circular(999),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  _TimePill(label: label, color: color),
                  const SizedBox(width: 8),
                  Text(
                    timeLabel,
                    style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: AppColors.textSecondary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 5),
              Text(
                title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w800,
                ),
              ),
              if (subtitle != null) ...[
                const SizedBox(height: 2),
                Text(
                  subtitle!,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

// ignore: unused_element
class _DayCalendarView extends StatelessWidget {
  const _DayCalendarView({
    required this.date,
    required this.lessons,
    required this.studentNameFor,
    required this.onEmptySlotTap,
    required this.onLessonTap,
  });

  final DateTime date;
  final List<LessonSchedule> lessons;
  final String Function(String id) studentNameFor;
  final ValueChanged<int> onEmptySlotTap;
  final ValueChanged<LessonSchedule> onLessonTap;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: List.generate(14, (index) {
        final hour = index + 8;
        final hourLessons = lessons.where((lesson) {
          return lesson.startAtUtc.toLocal().hour == hour;
        }).toList();

        return _HourRow(
          hour: hour,
          lessons: hourLessons,
          studentNameFor: studentNameFor,
          onEmptySlotTap: () => onEmptySlotTap(hour),
          onLessonTap: onLessonTap,
        );
      }),
    );
  }
}

class _HourRow extends StatelessWidget {
  const _HourRow({
    required this.hour,
    required this.lessons,
    required this.studentNameFor,
    required this.onEmptySlotTap,
    required this.onLessonTap,
  });

  final int hour;
  final List<LessonSchedule> lessons;
  final String Function(String id) studentNameFor;
  final VoidCallback onEmptySlotTap;
  final ValueChanged<LessonSchedule> onLessonTap;

  @override
  Widget build(BuildContext context) {
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          SizedBox(
            width: 48,
            child: Padding(
              padding: const EdgeInsets.only(top: 14),
              child: Text(
                '${hour.toString().padLeft(2, '0')}:00',
                style: Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: AppColors.textSecondary,
                ),
              ),
            ),
          ),
          Expanded(
            child: Container(
              constraints: const BoxConstraints(minHeight: 74),
              margin: const EdgeInsets.only(bottom: 10),
              decoration: const BoxDecoration(
                border: Border(top: BorderSide(color: AppColors.border)),
              ),
              child: lessons.isEmpty
                  ? InkWell(
                      borderRadius: BorderRadius.circular(14),
                      onTap: onEmptySlotTap,
                      child: Container(
                        margin: const EdgeInsets.only(top: 8),
                        alignment: Alignment.centerLeft,
                        padding: const EdgeInsets.symmetric(horizontal: 12),
                        decoration: BoxDecoration(
                          color: Colors.white.withValues(alpha: 0.62),
                          borderRadius: BorderRadius.circular(14),
                          border: Border.all(color: AppColors.border),
                        ),
                        child: Text(
                          'Bos saat',
                          style: Theme.of(context).textTheme.labelMedium
                              ?.copyWith(color: AppColors.textSecondary),
                        ),
                      ),
                    )
                  : Column(
                      children: lessons
                          .map(
                            (lesson) => Padding(
                              padding: const EdgeInsets.only(top: 8),
                              child: _LessonTile(
                                lesson: lesson,
                                studentName: studentNameFor(lesson.studentId),
                                compact: false,
                                onTap: () => onLessonTap(lesson),
                              ),
                            ),
                          )
                          .toList(),
                    ),
            ),
          ),
        ],
      ),
    );
  }
}

// ignore: unused_element
class _WeekCalendarView extends StatelessWidget {
  const _WeekCalendarView({
    required this.weekStart,
    required this.lessons,
    required this.selectedDate,
    required this.studentNameFor,
    required this.onDayTap,
    required this.onEmptyDayTap,
    required this.onLessonTap,
  });

  final DateTime weekStart;
  final List<LessonSchedule> lessons;
  final DateTime selectedDate;
  final String Function(String id) studentNameFor;
  final ValueChanged<DateTime> onDayTap;
  final ValueChanged<DateTime> onEmptyDayTap;
  final ValueChanged<LessonSchedule> onLessonTap;

  @override
  Widget build(BuildContext context) {
    final days = List<DateTime>.generate(
      7,
      (index) => weekStart.add(Duration(days: index)),
    );
    return SizedBox(
      height: 520,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        itemBuilder: (context, index) {
          final day = days[index];
          final dayLessons = lessons.where((lesson) {
            final local = lesson.startAtUtc.toLocal();
            return local.year == day.year &&
                local.month == day.month &&
                local.day == day.day;
          }).toList();

          return _WeekDayColumn(
            date: day,
            lessons: dayLessons,
            selected: _sameDay(day, selectedDate),
            studentNameFor: studentNameFor,
            onTap: () => onDayTap(day),
            onEmptyTap: () => onEmptyDayTap(day),
            onLessonTap: onLessonTap,
          );
        },
        separatorBuilder: (_, __) => const SizedBox(width: 10),
        itemCount: days.length,
      ),
    );
  }
}

class _WeekDayColumn extends StatelessWidget {
  const _WeekDayColumn({
    required this.date,
    required this.lessons,
    required this.selected,
    required this.studentNameFor,
    required this.onTap,
    required this.onEmptyTap,
    required this.onLessonTap,
  });

  final DateTime date;
  final List<LessonSchedule> lessons;
  final bool selected;
  final String Function(String id) studentNameFor;
  final VoidCallback onTap;
  final VoidCallback onEmptyTap;
  final ValueChanged<LessonSchedule> onLessonTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: onTap,
      child: Container(
        width: 132,
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(
            color: selected ? AppColors.primary : AppColors.border,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              DateFormat('EEE').format(date),
              style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: AppColors.textSecondary,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              DateFormat('dd').format(date),
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w900,
              ),
            ),
            const SizedBox(height: 12),
            if (lessons.isEmpty)
              Expanded(
                child: InkWell(
                  borderRadius: BorderRadius.circular(14),
                  onTap: onEmptyTap,
                  child: Container(
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: AppColors.primaryLight,
                      borderRadius: BorderRadius.circular(14),
                    ),
                    child: const Icon(
                      Icons.add_rounded,
                      color: AppColors.primary,
                    ),
                  ),
                ),
              )
            else
              Expanded(
                child: ListView.separated(
                  physics: const NeverScrollableScrollPhysics(),
                  itemBuilder: (context, index) {
                    final lesson = lessons[index];
                    return _LessonTile(
                      lesson: lesson,
                      studentName: studentNameFor(lesson.studentId),
                      compact: true,
                      onTap: () => onLessonTap(lesson),
                    );
                  },
                  separatorBuilder: (_, __) => const SizedBox(height: 8),
                  itemCount: lessons.length,
                ),
              ),
          ],
        ),
      ),
    );
  }
}

// ignore: unused_element
class _MonthCalendarView extends StatelessWidget {
  const _MonthCalendarView({
    required this.visibleDate,
    required this.selectedDate,
    required this.events,
    required this.studentNameFor,
    required this.onDayTap,
    required this.onEventTap,
  });

  final DateTime visibleDate;
  final DateTime selectedDate;
  final List<_CalendarEvent> events;
  final String Function(String id) studentNameFor;
  final ValueChanged<DateTime> onDayTap;
  final ValueChanged<_CalendarEvent> onEventTap;

  @override
  Widget build(BuildContext context) {
    final monthStart = DateTime(visibleDate.year, visibleDate.month);
    final gridStart = monthStart.subtract(
      Duration(days: monthStart.weekday - 1),
    );
    final days = List<DateTime>.generate(
      42,
      (index) => gridStart.add(Duration(days: index)),
    );
    final selectedEvents = events.where((event) {
      final local = event.start;
      return _sameDay(local, selectedDate);
    }).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: AppColors.border),
          ),
          child: Column(
            children: <Widget>[
              Row(
                children: const <Widget>[
                  _WeekName('Pzt'),
                  _WeekName('Sal'),
                  _WeekName('Car'),
                  _WeekName('Per'),
                  _WeekName('Cum'),
                  _WeekName('Cmt'),
                  _WeekName('Paz'),
                ],
              ),
              const SizedBox(height: 8),
              GridView.builder(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                  crossAxisCount: 7,
                  mainAxisSpacing: 6,
                  crossAxisSpacing: 6,
                  childAspectRatio: 0.5,
                ),
                itemCount: days.length,
                itemBuilder: (context, index) {
                  final day = days[index];
                  final dayEvents = events.where((event) {
                    return _sameDay(event.start, day);
                  }).toList()..sort((a, b) => a.start.compareTo(b.start));
                  return _MonthDayCell(
                    date: day,
                    inMonth: day.month == visibleDate.month,
                    selected: _sameDay(day, selectedDate),
                    events: dayEvents,
                    studentNameFor: studentNameFor,
                    onTap: () => onDayTap(day),
                  );
                },
              ),
            ],
          ),
        ),
        const SizedBox(height: 18),
        Row(
          children: <Widget>[
            Expanded(
              child: Text(
                _longDateLabel(selectedDate),
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        if (selectedEvents.isEmpty)
          const _EmptyDayPanel()
        else
          ...selectedEvents.map(
            (event) => Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: _EventDetailCard(
                event: event,
                studentName: event.studentId == null
                    ? null
                    : studentNameFor(event.studentId!),
                onTap: () => onEventTap(event),
              ),
            ),
          ),
      ],
    );
  }
}

class _WeekName extends StatelessWidget {
  const _WeekName(this.label);

  final String label;

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: Text(
        label,
        textAlign: TextAlign.center,
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
          color: AppColors.textSecondary,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class _MonthDayCell extends StatelessWidget {
  const _MonthDayCell({
    required this.date,
    required this.inMonth,
    required this.selected,
    required this.events,
    required this.studentNameFor,
    required this.onTap,
  });

  final DateTime date;
  final bool inMonth;
  final bool selected;
  final List<_CalendarEvent> events;
  final String Function(String id) studentNameFor;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final visibleEvents = events.take(1).toList();
    return InkWell(
      borderRadius: BorderRadius.circular(12),
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 160),
        padding: const EdgeInsets.all(6),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: selected ? AppColors.primary : AppColors.border,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                Expanded(
                  child: Align(
                    alignment: Alignment.centerLeft,
                    child: AnimatedContainer(
                      duration: const Duration(milliseconds: 160),
                      width: 24,
                      height: 24,
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        color: selected
                            ? AppColors.primary
                            : Colors.transparent,
                        shape: BoxShape.circle,
                      ),
                      child: Text(
                        '${date.day}',
                        style: Theme.of(context).textTheme.labelMedium
                            ?.copyWith(
                              color: selected
                                  ? Colors.white
                                  : inMonth
                                  ? AppColors.textPrimary
                                  : AppColors.textSecondary.withValues(
                                      alpha: 0.5,
                                    ),
                              fontWeight: FontWeight.w900,
                            ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 3),
            ...visibleEvents.map(
              (event) => Padding(
                padding: const EdgeInsets.only(bottom: 2),
                child: _MonthEventChip(
                  event: event,
                  studentName: event.studentId == null
                      ? null
                      : studentNameFor(event.studentId!),
                  selected: selected,
                ),
              ),
            ),
            if (events.length - visibleEvents.length > 1)
              Text(
                '+${events.length - visibleEvents.length}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: AppColors.textSecondary,
                  fontSize: 8,
                  fontWeight: FontWeight.w800,
                  height: 1,
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _MonthEventChip extends StatelessWidget {
  const _MonthEventChip({
    required this.event,
    required this.studentName,
    required this.selected,
  });

  final _CalendarEvent event;
  final String? studentName;
  final bool selected;

  @override
  Widget build(BuildContext context) {
    final start = DateFormat('HH:mm').format(event.start);
    final accent = _eventColor(event.type);
    return Container(
      width: double.infinity,
      height: 18,
      padding: const EdgeInsets.symmetric(horizontal: 4),
      decoration: BoxDecoration(
        color: accent.withValues(alpha: selected ? 0.16 : 0.1),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Row(
        children: <Widget>[
          Container(
            width: 5,
            height: 5,
            decoration: BoxDecoration(color: accent, shape: BoxShape.circle),
          ),
          const SizedBox(width: 3),
          Expanded(
            child: Text(
              event.isAllDay ? event.title : start,
              maxLines: 1,
              overflow: TextOverflow.clip,
              style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: accent,
                fontSize: 8,
                fontWeight: FontWeight.w900,
                height: 1,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _EventDetailCard extends StatelessWidget {
  const _EventDetailCard({
    required this.event,
    required this.studentName,
    required this.onTap,
  });

  final _CalendarEvent event;
  final String? studentName;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = _eventColor(event.type);
    final timeLabel = event.isAllDay
        ? 'Tum gun'
        : '${DateFormat('HH:mm').format(event.start)} - ${DateFormat('HH:mm').format(event.end)}';
    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(15),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(22),
          border: Border.all(color: AppColors.border),
          boxShadow: AppShadows.soft,
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Icon(_eventIcon(event.type), color: color, size: 22),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    timeLabel,
                    style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: AppColors.textSecondary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 5),
                  Text(
                    studentName == null
                        ? event.title
                        : '${event.title} - $studentName',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: <Widget>[
                      _TimePill(
                        label: _eventTypeLabel(event.type),
                        color: color,
                      ),
                      if (event.format != null)
                        _TimePill(
                          label: event.format == 'Online'
                              ? 'Online'
                              : 'Yuz Yuze',
                          color: AppColors.primary,
                        ),
                      _TimePill(
                        label: event.status,
                        color: event.status == 'Tamamlandi'
                            ? AppColors.accentGreen
                            : AppColors.textSecondary,
                      ),
                    ],
                  ),
                ],
              ),
            ),
            IconButton(
              onPressed: onTap,
              icon: const Icon(Icons.edit_rounded),
              color: AppColors.textSecondary,
            ),
            _SoftActionMenu(onEdit: onTap),
          ],
        ),
      ),
    );
  }
}

class _WeeklyProgramModal extends StatefulWidget {
  const _WeeklyProgramModal({
    required this.weekStart,
    required this.events,
    required this.previewId,
    required this.studentNameFor,
    required this.hasConflict,
  });

  final DateTime weekStart;
  final List<_CalendarEvent> events;
  final String previewId;
  final String Function(String id) studentNameFor;
  final bool hasConflict;

  @override
  State<_WeeklyProgramModal> createState() => _WeeklyProgramModalState();
}

class _WeeklyProgramModalState extends State<_WeeklyProgramModal> {
  late final ScrollController _horizontalScrollController;

  @override
  void initState() {
    super.initState();
    _horizontalScrollController = ScrollController();
  }

  @override
  void dispose() {
    _horizontalScrollController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(16, 14, 16, 18),
        child: Column(
          children: <Widget>[
            Container(
              width: 46,
              height: 5,
              decoration: BoxDecoration(
                color: AppColors.border,
                borderRadius: BorderRadius.circular(999),
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
                        'Haftalık Program',
                        style: Theme.of(context).textTheme.titleLarge?.copyWith(
                          color: AppColors.textPrimary,
                          fontWeight: FontWeight.w900,
                        ),
                      ),
                      const SizedBox(height: 3),
                      Text(
                        '${_shortDateLabel(widget.weekStart)} - ${_shortDateLabel(widget.weekStart.add(const Duration(days: 6)), includeYear: true)}',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: AppColors.textSecondary,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ],
                  ),
                ),
                _TimePill(
                  label: widget.hasConflict ? 'Çakışma var' : 'Uygun',
                  color: widget.hasConflict
                      ? AppColors.accentRed
                      : AppColors.accentGreen,
                ),
                IconButton(
                  onPressed: () => Navigator.of(context).pop(),
                  icon: const Icon(Icons.close_rounded),
                  color: AppColors.textSecondary,
                ),
              ],
            ),
            const SizedBox(height: 14),
            Expanded(
              child: _ScrollableWeekSchedule(
                controller: _horizontalScrollController,
                weekStart: widget.weekStart,
                events: widget.events,
                previewId: widget.previewId,
                studentNameFor: widget.studentNameFor,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ScrollableWeekSchedule extends StatelessWidget {
  const _ScrollableWeekSchedule({
    required this.controller,
    required this.weekStart,
    required this.events,
    required this.previewId,
    required this.studentNameFor,
  });

  final ScrollController controller;
  final DateTime weekStart;
  final List<_CalendarEvent> events;
  final String previewId;
  final String Function(String id) studentNameFor;

  static const _dayWidth = 190.0;
  static const _hourHeight = 72.0;
  static const _startHour = 8;
  static const _endHour = 22;

  @override
  Widget build(BuildContext context) {
    final days = List<DateTime>.generate(
      7,
      (index) => weekStart.add(Duration(days: index)),
    );

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: <Widget>[
          Container(
            height: 34,
            alignment: Alignment.center,
            color: const Color(0xFFF7FAFD),
            child: Text(
              'Günleri görmek için sağa sola kaydır',
              style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: AppColors.textSecondary,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
          Expanded(
            child: Scrollbar(
              controller: controller,
              thumbVisibility: true,
              notificationPredicate: (notification) =>
                  notification.metrics.axis == Axis.horizontal,
              child: SingleChildScrollView(
                controller: controller,
                scrollDirection: Axis.horizontal,
                physics: const BouncingScrollPhysics(
                  parent: AlwaysScrollableScrollPhysics(),
                ),
                child: SingleChildScrollView(
                  physics: const BouncingScrollPhysics(),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: days.map((day) {
                      final dayEvents =
                          events
                              .where((event) => _sameDay(event.start, day))
                              .toList()
                            ..sort((a, b) => a.start.compareTo(b.start));
                      return _WeekDayScheduleColumn(
                        date: day,
                        events: dayEvents,
                        previewId: previewId,
                        studentNameFor: studentNameFor,
                      );
                    }).toList(),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _WeekDayScheduleColumn extends StatelessWidget {
  const _WeekDayScheduleColumn({
    required this.date,
    required this.events,
    required this.previewId,
    required this.studentNameFor,
  });

  final DateTime date;
  final List<_CalendarEvent> events;
  final String previewId;
  final String Function(String id) studentNameFor;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: _ScrollableWeekSchedule._dayWidth,
      decoration: const BoxDecoration(
        border: Border(right: BorderSide(color: AppColors.border)),
      ),
      child: Column(
        children: <Widget>[
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: const BoxDecoration(
              color: Color(0xFFF7FAFD),
              border: Border(bottom: BorderSide(color: AppColors.border)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  _weekdayName(date.weekday),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w900,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  _shortDateLabel(date, includeYear: false),
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: AppColors.textSecondary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
          ),
          ...List<Widget>.generate(
            _ScrollableWeekSchedule._endHour -
                _ScrollableWeekSchedule._startHour,
            (index) {
              final hour = _ScrollableWeekSchedule._startHour + index;
              final hourEvents = events.where((event) {
                return event.isAllDay
                    ? hour == _ScrollableWeekSchedule._startHour
                    : event.start.hour == hour;
              }).toList();
              return _WeekHourCell(
                hour: hour,
                events: hourEvents,
                previewId: previewId,
                studentNameFor: studentNameFor,
              );
            },
          ),
        ],
      ),
    );
  }
}

class _WeekHourCell extends StatelessWidget {
  const _WeekHourCell({
    required this.hour,
    required this.events,
    required this.previewId,
    required this.studentNameFor,
  });

  final int hour;
  final List<_CalendarEvent> events;
  final String previewId;
  final String Function(String id) studentNameFor;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: _ScrollableWeekSchedule._hourHeight,
      padding: const EdgeInsets.fromLTRB(10, 6, 10, 6),
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: AppColors.border)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SizedBox(
            width: 40,
            child: Text(
              '${hour.toString().padLeft(2, '0')}:00',
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: AppColors.textSecondary,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
          const SizedBox(width: 6),
          Expanded(
            child: events.isEmpty
                ? const SizedBox.shrink()
                : Column(
                    children: events.take(2).map((event) {
                      return Expanded(
                        child: Padding(
                          padding: const EdgeInsets.only(bottom: 3),
                          child: _WeeklyAppointment(
                            event: event,
                            isPreview: event.id == previewId,
                            studentName: event.studentId == null
                                ? null
                                : studentNameFor(event.studentId!),
                          ),
                        ),
                      );
                    }).toList(),
                  ),
          ),
        ],
      ),
    );
  }
}

class _WeeklyAppointment extends StatelessWidget {
  const _WeeklyAppointment({
    required this.event,
    required this.isPreview,
    this.studentName,
  });

  final _CalendarEvent event;
  final bool isPreview;
  final String? studentName;

  @override
  Widget build(BuildContext context) {
    final color = isPreview ? AppColors.primary : _eventColor(event.type);
    return Container(
      width: double.infinity,
      height: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(8),
        border: isPreview ? Border.all(color: Colors.white, width: 1.5) : null,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          Text(
            isPreview ? 'Yeni Ders' : event.title,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w900,
              fontSize: 11,
            ),
          ),
          Text(
            event.isAllDay
                ? _eventShortLabel(event.type)
                : '${DateFormat('HH:mm').format(event.start)} ${_eventShortLabel(event.type)}',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: Colors.white.withValues(alpha: 0.9),
              fontWeight: FontWeight.w800,
              fontSize: 10,
            ),
          ),
          if (studentName != null)
            Text(
              studentName!,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: Colors.white.withValues(alpha: 0.88),
                fontWeight: FontWeight.w700,
              ),
            ),
        ],
      ),
    );
  }
}

class _LessonTile extends StatelessWidget {
  const _LessonTile({
    required this.lesson,
    required this.studentName,
    required this.compact,
    required this.onTap,
  });

  final LessonSchedule lesson;
  final String studentName;
  final bool compact;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final cancelled = lesson.status == 'Cancelled';
    final accent = cancelled
        ? AppColors.accentRed
        : lesson.lessonFormat == 'Online'
        ? AppColors.accentBlue
        : AppColors.accentGreen;
    final start = lesson.startAtUtc.toLocal();
    final end = lesson.endAtUtc.toLocal();

    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Container(
        padding: EdgeInsets.all(compact ? 10 : 14),
        decoration: BoxDecoration(
          color: cancelled ? AppColors.errorSurface : Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.border),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Container(
              width: compact ? 4 : 5,
              height: compact ? 54 : 62,
              decoration: BoxDecoration(
                color: accent,
                borderRadius: BorderRadius.circular(999),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    lesson.subject,
                    maxLines: compact ? 2 : 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: AppColors.textPrimary,
                      fontSize: compact ? 13 : null,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    compact
                        ? studentName
                        : '$studentName - ${lesson.locationLabel ?? lesson.timeZone}',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                      fontSize: compact ? 11 : null,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: <Widget>[
                      _TimePill(
                        label:
                            '${DateFormat('HH:mm').format(start)}-${DateFormat('HH:mm').format(end)}',
                        color: accent,
                      ),
                      if (!compact)
                        _TimePill(
                          label: lesson.lessonFormat == 'Online'
                              ? 'Online'
                              : 'Yuz yuze',
                          color: AppColors.primary,
                        ),
                      if (cancelled)
                        const _TimePill(
                          label: 'Iptal',
                          color: AppColors.accentRed,
                        ),
                    ],
                  ),
                ],
              ),
            ),
            if (!compact)
              const Icon(
                Icons.edit_calendar_rounded,
                color: AppColors.textSecondary,
                size: 20,
              ),
          ],
        ),
      ),
    );
  }
}

class _TimePill extends StatelessWidget {
  const _TimePill({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
          color: color,
          fontWeight: FontWeight.w800,
          fontSize: 11,
        ),
      ),
    );
  }
}

class _EmptyDayPanel extends StatelessWidget {
  const _EmptyDayPanel();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        children: <Widget>[
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: BorderRadius.circular(14),
            ),
            child: const Icon(
              Icons.event_available_rounded,
              color: AppColors.primary,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            'Bu gune ait etkinlik yok',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }
}

class _IconSurface extends StatelessWidget {
  const _IconSurface({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(14),
      onTap: onTap,
      child: Container(
        width: 42,
        height: 42,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: AppColors.border),
        ),
        child: Icon(icon, color: AppColors.textPrimary),
      ),
    );
  }
}

class _SchedulingBottomNav extends StatelessWidget {
  const _SchedulingBottomNav({
    required this.onHomeTap,
    required this.onLessonsTap,
    required this.onStudentsTap,
    required this.onMoreTap,
    required this.onFinanceTap,
  });

  final VoidCallback onHomeTap;
  final VoidCallback onLessonsTap;
  final VoidCallback onStudentsTap;
  final VoidCallback onMoreTap;
  final VoidCallback onFinanceTap;

  @override
  Widget build(BuildContext context) {
    final items = <_BottomNavItem>[
      _BottomNavItem(Icons.home_rounded, 'Ana sayfa', false, onHomeTap),
      _BottomNavItem(Icons.menu_book_rounded, 'Dersler', false, onLessonsTap),
      _BottomNavItem(Icons.groups_rounded, 'Ogrenciler', false, onStudentsTap),
      const _BottomNavItem(Icons.calendar_month_rounded, 'Takvim', true),
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
        border: Border(top: BorderSide(color: AppColors.border)),
      ),
      padding: EdgeInsets.fromLTRB(
        10,
        8,
        10,
        MediaQuery.of(context).padding.bottom + 8,
      ),
      child: Row(
        children: items.map((item) {
          return Expanded(
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
                          ? AppColors.primary
                          : AppColors.textSecondary,
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
                                  ? AppColors.primary
                                  : AppColors.textSecondary,
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
          );
        }).toList(),
      ),
    );
  }
}

class _BottomNavItem {
  const _BottomNavItem(this.icon, this.label, this.selected, [this.onTap]);

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback? onTap;
}

class _CalendarEvent {
  const _CalendarEvent({
    required this.id,
    required this.type,
    required this.title,
    required this.start,
    required this.end,
    required this.status,
    this.studentId,
    this.format,
    this.lesson,
    this.isAllDay = false,
  });

  factory _CalendarEvent.fromLesson(LessonSchedule lesson) {
    return _CalendarEvent(
      id: lesson.id,
      type: _CalendarEventType.lesson,
      title: lesson.subject,
      start: lesson.startAtUtc.toLocal(),
      end: lesson.endAtUtc.toLocal(),
      status: lesson.status == 'Completed' ? 'Tamamlandi' : 'Planlandi',
      studentId: lesson.studentId,
      format: lesson.lessonFormat,
      lesson: lesson,
    );
  }

  final String id;
  final _CalendarEventType type;
  final String title;
  final DateTime start;
  final DateTime end;
  final String status;
  final String? studentId;
  final String? format;
  final LessonSchedule? lesson;
  final bool isAllDay;
}

List<_CalendarEvent> _seedEvents(DateTime today) {
  final monday = _SchedulingPageState._startOfWeek(today);
  return <_CalendarEvent>[
    _CalendarEvent(
      id: 'event-unavailable-1',
      type: _CalendarEventType.unavailable,
      title: 'Tatil / Musait Degil',
      start: DateTime(monday.year, monday.month, monday.day + 3),
      end: DateTime(monday.year, monday.month, monday.day + 3, 23, 59),
      status: 'Planlandi',
      isAllDay: true,
    ),
    _CalendarEvent(
      id: 'event-assignment-1',
      type: _CalendarEventType.assignment,
      title: 'Odev Hatirlatmasi',
      start: DateTime(monday.year, monday.month, monday.day + 2, 18),
      end: DateTime(monday.year, monday.month, monday.day + 2, 18, 15),
      status: 'Planlandi',
      studentId: 'student-2',
    ),
    _CalendarEvent(
      id: 'event-payment-1',
      type: _CalendarEventType.payment,
      title: 'Odeme Hatirlatmasi',
      start: DateTime(monday.year, monday.month, monday.day + 5, 14),
      end: DateTime(monday.year, monday.month, monday.day + 5, 14, 15),
      status: 'Planlandi',
      studentId: 'student-1',
    ),
  ];
}

bool _sameDay(DateTime first, DateTime second) {
  return first.year == second.year &&
      first.month == second.month &&
      first.day == second.day;
}

Color _eventColor(_CalendarEventType type) {
  return switch (type) {
    _CalendarEventType.lesson => AppColors.accentBlue,
    _CalendarEventType.unavailable => AppColors.accentRed,
    _CalendarEventType.assignment => AppColors.accentGreen,
    _CalendarEventType.payment => AppColors.amber,
  };
}

IconData _eventIcon(_CalendarEventType type) {
  return switch (type) {
    _CalendarEventType.lesson => Icons.menu_book_rounded,
    _CalendarEventType.unavailable => Icons.block_rounded,
    _CalendarEventType.assignment => Icons.assignment_rounded,
    _CalendarEventType.payment => Icons.payments_rounded,
  };
}

String _eventTypeLabel(_CalendarEventType type) {
  return switch (type) {
    _CalendarEventType.lesson => 'Ders',
    _CalendarEventType.unavailable => 'Tatil / Musait Degil',
    _CalendarEventType.assignment => 'Odev Hatirlatmasi',
    _CalendarEventType.payment => 'Odeme Hatirlatmasi',
  };
}

String _eventShortLabel(_CalendarEventType type) {
  return switch (type) {
    _CalendarEventType.lesson => 'Ders',
    _CalendarEventType.unavailable => 'Tatil',
    _CalendarEventType.assignment => 'Ödev',
    _CalendarEventType.payment => 'Ödeme',
  };
}

String _dayTitleLabel(DateTime date) {
  return '${date.day} ${_monthName(date.month)}, ${_weekdayName(date.weekday)}';
}

String _monthYearLabel(DateTime date) {
  return '${_monthName(date.month)} ${date.year}';
}

String _shortDateLabel(DateTime date, {bool includeYear = false}) {
  final label = '${date.day} ${_shortMonthName(date.month)}';
  return includeYear ? '$label ${date.year}' : label;
}

String _selectedDateLabel(DateTime date) {
  return '${date.day} ${_monthName(date.month)} ${_weekdayName(date.weekday)}';
}

String _monthName(int month) {
  const months = <String>[
    'Ocak',
    'Şubat',
    'Mart',
    'Nisan',
    'Mayıs',
    'Haziran',
    'Temmuz',
    'Ağustos',
    'Eylül',
    'Ekim',
    'Kasım',
    'Aralık',
  ];
  return months[month - 1];
}

String _shortMonthName(int month) {
  const months = <String>[
    'Oca',
    'Şub',
    'Mar',
    'Nis',
    'May',
    'Haz',
    'Tem',
    'Ağu',
    'Eyl',
    'Eki',
    'Kas',
    'Ara',
  ];
  return months[month - 1];
}

String _weekdayName(int weekday) {
  const weekdays = <String>[
    'Pazartesi',
    'Salı',
    'Çarşamba',
    'Perşembe',
    'Cuma',
    'Cumartesi',
    'Pazar',
  ];
  return weekdays[weekday - 1];
}

String _longDateLabel(DateTime date) {
  const months = <String>[
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
  return '${date.day} ${months[date.month - 1]} ${date.year} ${weekdays[date.weekday - 1]}';
}
