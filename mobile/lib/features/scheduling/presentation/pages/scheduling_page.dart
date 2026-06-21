import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
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
  static const _navy = Color(0xFF082B4F);
  static const _navySoft = Color(0xFFE9F4FF);
  static const _blue = Color(0xFF3D8BFF);
  static const _emerald = Color(0xFF20B486);
  static const _red = Color(0xFFFF5A5F);
  static const _slate = Color(0xFF6B7A90);
  static const _text = Color(0xFF10233D);
  static const _background = Color(0xFFF4F8FC);
  static const _border = Color(0xFFE5EEF7);

  late DateTime _visibleDate;
  late List<LessonSchedule> _lessons;
  late List<_CalendarEvent> _events;
  _CalendarView _view = _CalendarView.month;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _visibleDate = DateTime(now.year, now.month, now.day);
    _lessons = _seedLessons(_visibleDate);
    _events = _seedEvents(_visibleDate);
  }

  @override
  Widget build(BuildContext context) {
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
      backgroundColor: _background,
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: _navy,
        foregroundColor: Colors.white,
        onPressed: () => _openLessonSheet(initialDate: _visibleDate),
        icon: const Icon(Icons.add_rounded),
        label: const Text('Ders Planla'),
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
                    _Header(teacherName: teacherName),
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
      ..._lessons.map(_CalendarEvent.fromLesson),
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

  Future<void> _openLessonSheet({
    LessonSchedule? lesson,
    DateTime? initialDate,
    int initialHour = 10,
  }) async {
    final edited = lesson != null;
    final baseDate =
        lesson?.startAtUtc.toLocal() ?? initialDate ?? _visibleDate;
    var selectedStudentId = lesson?.studentId ?? _students.first.id;
    var selectedFormat = lesson?.lessonFormat ?? 'Online';
    var selectedDate = DateTime(baseDate.year, baseDate.month, baseDate.day);
    var selectedTime = TimeOfDay.fromDateTime(
      lesson?.startAtUtc.toLocal() ??
          DateTime(baseDate.year, baseDate.month, baseDate.day, initialHour),
    );

    final subjectController = TextEditingController(
      text: lesson?.subject ?? 'Matematik',
    );
    final durationController = TextEditingController(
      text: lesson == null
          ? '60'
          : lesson.endAtUtc.difference(lesson.startAtUtc).inMinutes.toString(),
    );
    final locationController = TextEditingController(
      text: lesson?.locationLabel ?? 'Zoom',
    );
    final notesController = TextEditingController(text: lesson?.notes ?? '');
    final formKey = GlobalKey<FormState>();

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (sheetContext) {
        return StatefulBuilder(
          builder: (context, setSheetState) {
            final start = DateTime(
              selectedDate.year,
              selectedDate.month,
              selectedDate.day,
              selectedTime.hour,
              selectedTime.minute,
            );
            final duration = int.tryParse(durationController.text.trim()) ?? 60;
            final end = start.add(Duration(minutes: duration));

            return Padding(
              padding: EdgeInsets.only(
                left: 20,
                right: 20,
                top: 14,
                bottom: MediaQuery.of(sheetContext).viewInsets.bottom + 20,
              ),
              child: Form(
                key: formKey,
                child: SingleChildScrollView(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Center(
                        child: Container(
                          width: 38,
                          height: 4,
                          decoration: BoxDecoration(
                            color: _border,
                            borderRadius: BorderRadius.circular(999),
                          ),
                        ),
                      ),
                      const SizedBox(height: 18),
                      Row(
                        children: <Widget>[
                          Expanded(
                            child: Text(
                              edited ? 'Dersi duzenle' : 'Ders planla',
                              style: Theme.of(context).textTheme.titleLarge
                                  ?.copyWith(
                                    color: _text,
                                    fontWeight: FontWeight.w800,
                                  ),
                            ),
                          ),
                          _TimePill(
                            label:
                                '${DateFormat('HH:mm').format(start)} - ${DateFormat('HH:mm').format(end)}',
                            color: _navy,
                          ),
                        ],
                      ),
                      const SizedBox(height: 18),
                      AppDropdownField<String>(
                        value: selectedStudentId,
                        labelText: 'Ogrenci',
                        items: _students
                            .map(
                              (student) => DropdownMenuItem<String>(
                                value: student.id,
                                child: Text(student.name),
                              ),
                            )
                            .toList(),
                        onChanged: (value) => setSheetState(
                          () => selectedStudentId = value ?? selectedStudentId,
                        ),
                      ),
                      const SizedBox(height: 12),
                      AppTextField(
                        controller: subjectController,
                        labelText: 'Ders konusu',
                        hintText: 'Matematik - Fonksiyonlar',
                        validator: _required,
                      ),
                      const SizedBox(height: 12),
                      Row(
                        children: <Widget>[
                          Expanded(
                            child: AppTextField(
                              controller: TextEditingController(
                                text: DateFormat(
                                  'dd.MM.yyyy',
                                ).format(selectedDate),
                              ),
                              labelText: 'Tarih',
                              readOnly: true,
                              suffixIcon: const Icon(Icons.event_rounded),
                              onTap: () async {
                                final picked = await showDatePicker(
                                  context: sheetContext,
                                  initialDate: selectedDate,
                                  firstDate: DateTime(2025),
                                  lastDate: DateTime(2028),
                                );
                                if (picked != null) {
                                  setSheetState(
                                    () => selectedDate = DateTime(
                                      picked.year,
                                      picked.month,
                                      picked.day,
                                    ),
                                  );
                                }
                              },
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: AppTextField(
                              controller: TextEditingController(
                                text: selectedTime.format(context),
                              ),
                              labelText: 'Saat',
                              readOnly: true,
                              suffixIcon: const Icon(Icons.schedule_rounded),
                              onTap: () async {
                                final picked = await showTimePicker(
                                  context: sheetContext,
                                  initialTime: selectedTime,
                                );
                                if (picked != null) {
                                  setSheetState(() => selectedTime = picked);
                                }
                              },
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      Row(
                        children: <Widget>[
                          Expanded(
                            child: AppTextField(
                              controller: durationController,
                              labelText: 'Sure',
                              hintText: '60',
                              keyboardType: TextInputType.number,
                              validator: _durationValidator,
                            ),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: AppDropdownField<String>(
                              value: selectedFormat,
                              labelText: 'Format',
                              items: const <DropdownMenuItem<String>>[
                                DropdownMenuItem(
                                  value: 'Online',
                                  child: Text('Online'),
                                ),
                                DropdownMenuItem(
                                  value: 'InPerson',
                                  child: Text('Yuz yuze'),
                                ),
                              ],
                              onChanged: (value) => setSheetState(
                                () => selectedFormat = value ?? selectedFormat,
                              ),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      AppTextField(
                        controller: locationController,
                        labelText: selectedFormat == 'Online'
                            ? 'Baglanti / platform'
                            : 'Konum',
                        validator: _required,
                      ),
                      const SizedBox(height: 12),
                      AppTextField(
                        controller: notesController,
                        labelText: 'Not',
                        hintText: 'Haftalik tekrar, kaynak, odev bilgisi',
                        maxLines: 2,
                        maxLength: 160,
                      ),
                      const SizedBox(height: 16),
                      _ProgramPreview(
                        weekStart: _startOfWeek(selectedDate),
                        events: _calendarEvents,
                        previewEvent: _CalendarEvent(
                          id: lesson?.id ?? 'preview',
                          type: _CalendarEventType.lesson,
                          title: subjectController.text.trim().isEmpty
                              ? 'Yeni ders'
                              : subjectController.text.trim(),
                          start: start,
                          end: end,
                          studentId: selectedStudentId,
                          format: selectedFormat,
                          status: lesson?.status ?? 'Planlandi',
                        ),
                        editingId: lesson?.id,
                        studentNameFor: _studentNameFor,
                      ),
                      const SizedBox(height: 16),
                      AppPrimaryButton(
                        label: 'Kaydet',
                        onPressed: () {
                          if (!(formKey.currentState?.validate() ?? false)) {
                            return;
                          }

                          if (_hasConflict(
                            start: start,
                            end: end,
                            ignoredLessonId: lesson?.id,
                          )) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              const SnackBar(
                                content: Text(
                                  'Bu saat araliginda baska bir ders var.',
                                ),
                              ),
                            );
                            return;
                          }

                          final newLesson = LessonSchedule(
                            id:
                                lesson?.id ??
                                'lesson-${DateTime.now().microsecondsSinceEpoch}',
                            teacherUserId: lesson?.teacherUserId ?? 'teacher-1',
                            studentId: selectedStudentId,
                            subject: subjectController.text.trim(),
                            lessonFormat: selectedFormat,
                            startAtUtc: start.toUtc(),
                            endAtUtc: end.toUtc(),
                            timeZone: 'Europe/Istanbul',
                            status: lesson?.status ?? 'Planned',
                            reminderOffsetMinutes: 60,
                            locationLabel: locationController.text.trim(),
                            notes: notesController.text.trim(),
                          );

                          setState(() {
                            if (edited) {
                              _lessons = _lessons
                                  .map(
                                    (item) => item.id == newLesson.id
                                        ? newLesson
                                        : item,
                                  )
                                  .toList();
                            } else {
                              _lessons = <LessonSchedule>[
                                ..._lessons,
                                newLesson,
                              ];
                            }
                            _sortLessons();
                            _visibleDate = selectedDate;
                          });
                          Navigator.of(sheetContext).pop();
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                              content: Text(
                                edited
                                    ? 'Ders guncellendi.'
                                    : 'Ders takvime eklendi.',
                              ),
                            ),
                          );
                        },
                      ),
                      if (edited) ...<Widget>[
                        const SizedBox(height: 10),
                        SizedBox(
                          width: double.infinity,
                          child: OutlinedButton.icon(
                            style: OutlinedButton.styleFrom(
                              foregroundColor: _red,
                              side: const BorderSide(color: _red),
                              minimumSize: const Size.fromHeight(50),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(14),
                              ),
                            ),
                            onPressed: () {
                              setState(() {
                                _lessons = _lessons
                                    .where((item) => item.id != lesson.id)
                                    .toList();
                              });
                              Navigator.of(sheetContext).pop();
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(content: Text('Ders silindi.')),
                              );
                            },
                            icon: const Icon(Icons.delete_outline_rounded),
                            label: const Text('Dersi Sil'),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            );
          },
        );
      },
    );
  }

  bool _hasConflict({
    required DateTime start,
    required DateTime end,
    String? ignoredLessonId,
  }) {
    return _lessons.any((lesson) {
      if (lesson.id == ignoredLessonId || lesson.status == 'Cancelled') {
        return false;
      }
      final lessonStart = lesson.startAtUtc.toLocal();
      final lessonEnd = lesson.endAtUtc.toLocal();
      return start.isBefore(lessonEnd) && end.isAfter(lessonStart);
    });
  }

  void _sortLessons() {
    _lessons.sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));
  }

  // ignore: unused_element
  List<LessonSchedule> _lessonsForDay(DateTime date) {
    final day = DateTime(date.year, date.month, date.day);
    return _lessons.where((lesson) {
      final local = lesson.startAtUtc.toLocal();
      return local.year == day.year &&
          local.month == day.month &&
          local.day == day.day;
    }).toList()..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));
  }

  // ignore: unused_element
  List<LessonSchedule> _lessonsForRange(DateTime start, DateTime end) {
    return _lessons.where((lesson) {
      final local = lesson.startAtUtc.toLocal();
      return !local.isBefore(start) && local.isBefore(end);
    }).toList()..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));
  }

  String _studentNameFor(String id) {
    return _students
        .firstWhere(
          (student) => student.id == id,
          orElse: () => _Student(id: id, name: id, level: ''),
        )
        .name;
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

  String? _required(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Bu alan zorunlu.';
    }
    return null;
  }

  String? _durationValidator(String? value) {
    final required = _required(value);
    if (required != null) {
      return required;
    }
    final duration = int.tryParse(value!.trim());
    if (duration == null) {
      return 'Dakika gir.';
    }
    if (duration < 30 || duration > 240) {
      return '30-240 dakika olmali.';
    }
    return null;
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.teacherName});

  final String teacherName;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                teacherName,
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                  color: _SchedulingPageState._text,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                'Takvim',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: _SchedulingPageState._slate,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
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

class _HeaderIconButton extends StatelessWidget {
  const _HeaderIconButton({
    required this.icon,
    required this.onTap,
    this.badgeText,
  });

  final IconData icon;
  final VoidCallback onTap;
  final String? badgeText;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: SizedBox(
        width: 46,
        height: 46,
        child: Stack(
          clipBehavior: Clip.none,
          children: <Widget>[
            Positioned.fill(
              child: Container(
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(16),
                  border: Border.all(color: _SchedulingPageState._border),
                ),
                child: Icon(icon, color: _SchedulingPageState._text),
              ),
            ),
            if (badgeText != null)
              Positioned(
                right: -2,
                top: -2,
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 5,
                    vertical: 2,
                  ),
                  decoration: BoxDecoration(
                    color: _SchedulingPageState._red,
                    borderRadius: BorderRadius.circular(999),
                    border: Border.all(color: Colors.white, width: 2),
                  ),
                  child: Text(
                    badgeText!,
                    style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: Colors.white,
                      fontSize: 9,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
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
              Icon(
                Icons.edit_rounded,
                size: 18,
                color: _SchedulingPageState._navy,
              ),
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
                color: _SchedulingPageState._red,
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
          color: const Color(0xFFF4F8FC),
          borderRadius: BorderRadius.circular(12),
        ),
        child: const Icon(
          Icons.more_horiz_rounded,
          color: _SchedulingPageState._slate,
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
        border: Border.all(color: _SchedulingPageState._border),
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
                      color: selected
                          ? _SchedulingPageState._navy
                          : Colors.white,
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Text(
                      label,
                      style: Theme.of(context).textTheme.labelMedium?.copyWith(
                        color: selected
                            ? Colors.white
                            : _SchedulingPageState._slate,
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
              color: _SchedulingPageState._text,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        const SizedBox(width: 10),
        OutlinedButton(
          onPressed: onToday,
          style: OutlinedButton.styleFrom(
            minimumSize: const Size(70, 42),
            side: const BorderSide(color: _SchedulingPageState._border),
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
            border: Border.all(color: _SchedulingPageState._border),
            boxShadow: const <BoxShadow>[
              BoxShadow(
                color: Color(0x10082B4F),
                blurRadius: 24,
                offset: Offset(0, 10),
              ),
            ],
          ),
          clipBehavior: Clip.antiAlias,
          child: SfCalendar(
            key: ValueKey<String>('calendar-${view.name}-${visibleDate.month}'),
            view: _syncfusionView,
            dataSource: _LessonCalendarDataSource(events),
            initialDisplayDate: visibleDate,
            initialSelectedDate: visibleDate,
            todayHighlightColor: _SchedulingPageState._navy,
            selectionDecoration: BoxDecoration(
              color: Colors.transparent,
              border: Border.all(color: _SchedulingPageState._navy, width: 1.5),
              borderRadius: BorderRadius.circular(10),
            ),
            headerHeight: 0,
            backgroundColor: Colors.white,
            cellBorderColor: _SchedulingPageState._border,
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
        border: Border.all(color: _SchedulingPageState._border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            _selectedDateLabel(date),
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: _SchedulingPageState._text,
              fontWeight: FontWeight.w900,
            ),
          ),
          const SizedBox(height: 12),
          if (events.isEmpty)
            Text(
              'Bu güne ait etkinlik yok.',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: _SchedulingPageState._slate,
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
                      color: _SchedulingPageState._slate,
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
                  color: _SchedulingPageState._text,
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
                    color: _SchedulingPageState._slate,
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
                  color: _SchedulingPageState._slate,
                ),
              ),
            ),
          ),
          Expanded(
            child: Container(
              constraints: const BoxConstraints(minHeight: 74),
              margin: const EdgeInsets.only(bottom: 10),
              decoration: const BoxDecoration(
                border: Border(
                  top: BorderSide(color: _SchedulingPageState._border),
                ),
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
                          border: Border.all(
                            color: _SchedulingPageState._border,
                          ),
                        ),
                        child: Text(
                          'Bos saat',
                          style: Theme.of(context).textTheme.labelMedium
                              ?.copyWith(color: _SchedulingPageState._slate),
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
            color: selected
                ? _SchedulingPageState._navy
                : _SchedulingPageState._border,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              DateFormat('EEE').format(date),
              style: Theme.of(context).textTheme.labelMedium?.copyWith(
                color: _SchedulingPageState._slate,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              DateFormat('dd').format(date),
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: _SchedulingPageState._text,
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
                      color: _SchedulingPageState._navySoft,
                      borderRadius: BorderRadius.circular(14),
                    ),
                    child: const Icon(
                      Icons.add_rounded,
                      color: _SchedulingPageState._navy,
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
            border: Border.all(color: _SchedulingPageState._border),
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
                  color: _SchedulingPageState._text,
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
          color: _SchedulingPageState._slate,
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
            color: selected
                ? _SchedulingPageState._navy
                : _SchedulingPageState._border,
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
                            ? _SchedulingPageState._navy
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
                                  ? _SchedulingPageState._text
                                  : _SchedulingPageState._slate.withValues(
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
                  color: _SchedulingPageState._slate,
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
          border: Border.all(color: _SchedulingPageState._border),
          boxShadow: <BoxShadow>[
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.045),
              blurRadius: 18,
              offset: const Offset(0, 8),
            ),
          ],
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
                      color: _SchedulingPageState._slate,
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
                      color: _SchedulingPageState._text,
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
                          color: _SchedulingPageState._navy,
                        ),
                      _TimePill(
                        label: event.status,
                        color: event.status == 'Tamamlandi'
                            ? _SchedulingPageState._emerald
                            : _SchedulingPageState._slate,
                      ),
                    ],
                  ),
                ],
              ),
            ),
            IconButton(
              onPressed: onTap,
              icon: const Icon(Icons.edit_rounded),
              color: _SchedulingPageState._slate,
            ),
            _SoftActionMenu(onEdit: onTap),
          ],
        ),
      ),
    );
  }
}

class _ProgramPreview extends StatelessWidget {
  const _ProgramPreview({
    required this.weekStart,
    required this.events,
    required this.previewEvent,
    required this.editingId,
    required this.studentNameFor,
  });

  final DateTime weekStart;
  final List<_CalendarEvent> events;
  final _CalendarEvent previewEvent;
  final String? editingId;
  final String Function(String id) studentNameFor;

  @override
  Widget build(BuildContext context) {
    final previewEvents = <_CalendarEvent>[
      ...events.where((event) => event.id != editingId),
      previewEvent,
    ]..sort((a, b) => a.start.compareTo(b.start));
    final hasConflict = previewEvents.any((event) {
      if (event.id == previewEvent.id ||
          event.type != _CalendarEventType.lesson) {
        return false;
      }
      return _sameDay(event.start, previewEvent.start) &&
          previewEvent.start.isBefore(event.end) &&
          previewEvent.end.isAfter(event.start);
    });

    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: () => _showWeeklyProgramModal(
        context,
        weekStart: weekStart,
        events: previewEvents,
        previewId: previewEvent.id,
        studentNameFor: studentNameFor,
        hasConflict: hasConflict,
      ),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: const Color(0xFFF7FAFD),
          borderRadius: BorderRadius.circular(18),
          border: Border.all(
            color: hasConflict
                ? _SchedulingPageState._red.withValues(alpha: 0.45)
                : _SchedulingPageState._border,
          ),
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 42,
              height: 42,
              decoration: BoxDecoration(
                color: _SchedulingPageState._navySoft,
                borderRadius: BorderRadius.circular(14),
              ),
              child: const Icon(
                Icons.calendar_view_week_rounded,
                color: _SchedulingPageState._navy,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'Haftalık programını göster',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: _SchedulingPageState._text,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                  const SizedBox(height: 3),
                  Text(
                    'Seçilen dersi haftalık takvimde kontrol et.',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _SchedulingPageState._slate,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 10),
            _TimePill(
              label: hasConflict ? 'Çakışma var' : 'Uygun',
              color: hasConflict
                  ? _SchedulingPageState._red
                  : _SchedulingPageState._emerald,
            ),
            const SizedBox(width: 4),
            const Icon(
              Icons.chevron_right_rounded,
              color: _SchedulingPageState._slate,
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _showWeeklyProgramModal(
    BuildContext context, {
    required DateTime weekStart,
    required List<_CalendarEvent> events,
    required String previewId,
    required String Function(String id) studentNameFor,
    required bool hasConflict,
  }) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (sheetContext) {
        return FractionallySizedBox(
          heightFactor: 0.86,
          child: _WeeklyProgramModal(
            weekStart: weekStart,
            events: events,
            previewId: previewId,
            studentNameFor: studentNameFor,
            hasConflict: hasConflict,
          ),
        );
      },
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
                color: _SchedulingPageState._border,
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
                          color: _SchedulingPageState._text,
                          fontWeight: FontWeight.w900,
                        ),
                      ),
                      const SizedBox(height: 3),
                      Text(
                        '${_shortDateLabel(widget.weekStart)} - ${_shortDateLabel(widget.weekStart.add(const Duration(days: 6)), includeYear: true)}',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                          color: _SchedulingPageState._slate,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ],
                  ),
                ),
                _TimePill(
                  label: widget.hasConflict ? 'Çakışma var' : 'Uygun',
                  color: widget.hasConflict
                      ? _SchedulingPageState._red
                      : _SchedulingPageState._emerald,
                ),
                IconButton(
                  onPressed: () => Navigator.of(context).pop(),
                  icon: const Icon(Icons.close_rounded),
                  color: _SchedulingPageState._slate,
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
        border: Border.all(color: _SchedulingPageState._border),
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
                color: _SchedulingPageState._slate,
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
        border: Border(right: BorderSide(color: _SchedulingPageState._border)),
      ),
      child: Column(
        children: <Widget>[
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: const BoxDecoration(
              color: Color(0xFFF7FAFD),
              border: Border(
                bottom: BorderSide(color: _SchedulingPageState._border),
              ),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  _weekdayName(date.weekday),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: _SchedulingPageState._text,
                    fontWeight: FontWeight.w900,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  _shortDateLabel(date, includeYear: false),
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: _SchedulingPageState._slate,
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
        border: Border(bottom: BorderSide(color: _SchedulingPageState._border)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SizedBox(
            width: 40,
            child: Text(
              '${hour.toString().padLeft(2, '0')}:00',
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: _SchedulingPageState._slate,
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
    final color = isPreview
        ? _SchedulingPageState._navy
        : _eventColor(event.type);
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
        ? _SchedulingPageState._red
        : lesson.lessonFormat == 'Online'
        ? _SchedulingPageState._blue
        : _SchedulingPageState._emerald;
    final start = lesson.startAtUtc.toLocal();
    final end = lesson.endAtUtc.toLocal();

    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Container(
        padding: EdgeInsets.all(compact ? 10 : 14),
        decoration: BoxDecoration(
          color: cancelled ? const Color(0xFFFFF2F2) : Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: _SchedulingPageState._border),
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
                      color: _SchedulingPageState._text,
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
                      color: _SchedulingPageState._slate,
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
                          color: _SchedulingPageState._navy,
                        ),
                      if (cancelled)
                        const _TimePill(
                          label: 'Iptal',
                          color: _SchedulingPageState._red,
                        ),
                    ],
                  ),
                ],
              ),
            ),
            if (!compact)
              const Icon(
                Icons.edit_calendar_rounded,
                color: _SchedulingPageState._slate,
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
        border: Border.all(color: _SchedulingPageState._border),
      ),
      child: Column(
        children: <Widget>[
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: _SchedulingPageState._navySoft,
              borderRadius: BorderRadius.circular(14),
            ),
            child: const Icon(
              Icons.event_available_rounded,
              color: _SchedulingPageState._navy,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            'Bu gune ait etkinlik yok',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: _SchedulingPageState._text,
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
          border: Border.all(color: _SchedulingPageState._border),
        ),
        child: Icon(icon, color: _SchedulingPageState._text),
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
        border: Border(top: BorderSide(color: _SchedulingPageState._border)),
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
                          ? _SchedulingPageState._navy
                          : _SchedulingPageState._slate,
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
                                  ? _SchedulingPageState._navy
                                  : _SchedulingPageState._slate,
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

class _Student {
  const _Student({required this.id, required this.name, required this.level});

  final String id;
  final String name;
  final String level;
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

const _students = <_Student>[
  _Student(id: 'student-1', name: 'Zeynep Demir', level: '10. Sinif'),
  _Student(id: 'student-2', name: 'Ali Yilmaz', level: '8. Sinif'),
  _Student(id: 'student-3', name: 'Merve Kaya', level: '11. Sinif'),
  _Student(id: 'student-4', name: 'Ege Arslan', level: '7. Sinif'),
];

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

List<LessonSchedule> _seedLessons(DateTime today) {
  final monday = _SchedulingPageState._startOfWeek(today);
  return <LessonSchedule>[
    _lesson(
      id: 'lesson-1',
      day: monday,
      hour: 10,
      minute: 30,
      duration: 60,
      studentId: 'student-1',
      subject: 'Matematik - Trigonometri',
      format: 'Online',
      location: 'Zoom',
      notes: 'Sinus teoremi tekrar',
    ),
    _lesson(
      id: 'lesson-2',
      day: monday.add(const Duration(days: 1)),
      hour: 17,
      duration: 90,
      studentId: 'student-2',
      subject: 'Fizik - Kuvvet ve Hareket',
      format: 'InPerson',
      location: 'Kadikoy etut merkezi',
      notes: 'Deneme analizi ile basla',
    ),
    _lesson(
      id: 'lesson-3',
      day: monday.add(const Duration(days: 2)),
      hour: 19,
      duration: 60,
      studentId: 'student-3',
      subject: 'Geometri - Problem Cozumu',
      format: 'Online',
      location: 'Google Meet',
      notes: 'Cember sorulari',
    ),
    _lesson(
      id: 'lesson-4',
      day: monday.add(const Duration(days: 4)),
      hour: 15,
      minute: 30,
      duration: 60,
      studentId: 'student-4',
      subject: 'Turkce - Paragraf',
      format: 'Online',
      location: 'Zoom',
      notes: 'Sure tutarak coz',
    ),
    _lesson(
      id: 'lesson-5',
      day: monday.add(const Duration(days: 5)),
      hour: 11,
      duration: 75,
      studentId: 'student-1',
      subject: 'Matematik - Deneme Analizi',
      format: 'InPerson',
      location: 'Besiktas',
      notes: 'Yanlis defteri kontrol',
    ),
  ]..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));
}

LessonSchedule _lesson({
  required String id,
  required DateTime day,
  required int hour,
  required int duration,
  required String studentId,
  required String subject,
  required String format,
  required String location,
  required String notes,
  int minute = 0,
}) {
  final start = DateTime(day.year, day.month, day.day, hour, minute);
  return LessonSchedule(
    id: id,
    teacherUserId: 'teacher-1',
    studentId: studentId,
    subject: subject,
    lessonFormat: format,
    startAtUtc: start.toUtc(),
    endAtUtc: start.add(Duration(minutes: duration)).toUtc(),
    timeZone: 'Europe/Istanbul',
    reminderOffsetMinutes: 60,
    locationLabel: location,
    notes: notes,
  );
}

bool _sameDay(DateTime first, DateTime second) {
  return first.year == second.year &&
      first.month == second.month &&
      first.day == second.day;
}

Color _eventColor(_CalendarEventType type) {
  return switch (type) {
    _CalendarEventType.lesson => _SchedulingPageState._blue,
    _CalendarEventType.unavailable => _SchedulingPageState._red,
    _CalendarEventType.assignment => _SchedulingPageState._emerald,
    _CalendarEventType.payment => const Color(0xFFFFB84D),
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
