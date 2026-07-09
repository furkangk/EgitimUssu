import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/scheduling_format.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/widgets/study_entry_form_sheet.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:syncfusion_flutter_calendar/calendar.dart';

/// "Takvim" sekmesi — öğrencinin birleşik ders programı (ogrenci_ux §9a).
/// Öğretmen takvim ekranıyla **aynı** takvim: `SfCalendar` (Aylık/Haftalık/Günlük geçişi +
/// tarih gezinme çubuğu) + altında seçili günün listesi. Öğretmenin planladığı dersler
/// salt-okunur/öncelikli; öğrenci kendi dersini ekler/düzenler/siler.
class StudentCalendarPage extends StatefulWidget {
  const StudentCalendarPage({super.key});

  @override
  State<StudentCalendarPage> createState() => _StudentCalendarPageState();
}

enum _CalendarView { day, week, month }

class _StudentCalendarPageState extends State<StudentCalendarPage> {
  String? _studentId;
  List<CalendarOccurrence> _occurrences = const <CalendarOccurrence>[];
  bool _loading = true;
  String? _error;

  _CalendarView _view = _CalendarView.month;
  late DateTime _visibleDate;
  int? _loadedMonthKey;

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _visibleDate = DateTime(now.year, now.month, now.day);
    _load();
  }

  int _monthKey(DateTime d) => d.year * 12 + d.month;

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final session = context.read<AuthCubit>().state.session;
      final studentId =
          _studentId ??
          await StudentScope.resolve(
            userId: session?.userId ?? '',
            fullName: session?.fullName ?? '',
          );
      // Görünen ayın 6 haftalık ızgarasını + tampon (haftanın ay sınırını aşması için) kapsar.
      final start = DateTime(
        _visibleDate.year,
        _visibleDate.month,
        1,
      ).subtract(const Duration(days: 14));
      final end = DateTime(
        _visibleDate.year,
        _visibleDate.month + 1,
        1,
      ).add(const Duration(days: 14));
      final occurrences = await injector<SchedulingRepository>()
          .getStudentCalendar(
            studentId: studentId,
            startAtUtc: start.toUtc(),
            endAtUtc: end.toUtc(),
          );
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _occurrences = occurrences;
        _loadedMonthKey = _monthKey(_visibleDate);
        _loading = false;
      });
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _error = e.message;
          _loading = false;
        });
      }
    }
  }

  bool _isSameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;

  List<CalendarOccurrence> get _dayOccurrences {
    final list =
        _occurrences
            .where((o) => _isSameDay(o.startAtUtc.toLocal(), _visibleDate))
            .toList()
          ..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));
    return list;
  }

  /// Görünen tarih değiştiğinde gerekirse (ay değişmişse) veriyi tazeler.
  void _setVisibleDate(DateTime date) {
    setState(() => _visibleDate = date);
    if (_loadedMonthKey != _monthKey(date)) {
      _load();
    }
  }

  void _moveVisibleDate(int direction) {
    final next = switch (_view) {
      _CalendarView.day => _visibleDate.add(Duration(days: direction)),
      _CalendarView.week => _visibleDate.add(Duration(days: direction * 7)),
      _CalendarView.month => DateTime(
        _visibleDate.year,
        _visibleDate.month + direction,
        _visibleDate.day.clamp(1, 28),
      ),
    };
    _setVisibleDate(next);
  }

  void _onCalendarTap(CalendarTapDetails details) {
    final date = details.date;
    if (date == null) return;
    final day = DateTime(date.year, date.month, date.day);
    if (!_isSameDay(day, _visibleDate)) {
      _setVisibleDate(day);
    }
  }

  void _onViewChanged(ViewChangedDetails details) {
    if (details.visibleDates.isEmpty) return;
    final anchor = details.visibleDates[details.visibleDates.length ~/ 2];
    if (_monthKey(anchor) == _loadedMonthKey) return;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _setVisibleDate(DateTime(anchor.year, anchor.month, anchor.day));
    });
  }

  Future<void> _addEntry() async {
    final studentId = _studentId;
    if (studentId == null) return;
    final saved = await _openForm(
      StudyEntryFormSheet(studentId: studentId, initialDate: _visibleDate),
    );
    if (saved == true) await _load();
  }

  Future<void> _editEntry(CalendarOccurrence occ) async {
    final studentId = _studentId;
    if (studentId == null) return;
    final saved = await _openForm(
      StudyEntryFormSheet(studentId: studentId, initial: _toEntry(occ)),
    );
    if (saved == true) await _load();
  }

  Future<bool?> _openForm(Widget form) {
    return Navigator.of(context).push<bool>(
      MaterialPageRoute<bool>(
        fullscreenDialog: true,
        builder: (_) => Scaffold(backgroundColor: Colors.white, body: form),
      ),
    );
  }

  StudyScheduleEntry _toEntry(CalendarOccurrence occ) {
    return StudyScheduleEntry(
      id: occ.entryId,
      studentId: _studentId ?? '',
      subject: occ.subject,
      topic: occ.topic,
      startAtUtc: occ.startAtUtc,
      endAtUtc: occ.endAtUtc,
      timeZone: 'Europe/Istanbul',
      recurrenceRule: occ.recurrenceRule,
      colorHex: occ.colorHex,
      notes: occ.notes,
    );
  }

  Future<void> _deleteEntry(CalendarOccurrence occ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Dersi sil'),
        content: Text(
          '"${occ.subject}" dersini programından silmek istiyor musun?'
          '${occ.isRecurring ? '\n\nBu tekrarlı bir derstir; tüm tekrarları silinir.' : ''}',
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.accentRed),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Sil'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    try {
      await injector<SchedulingRepository>().deleteStudyEntry(occ.entryId);
      if (!mounted) return;
      await _load();
    } on ApiException catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.message), backgroundColor: AppColors.error),
      );
    }
  }

  String get _navTitle => switch (_view) {
    _CalendarView.day => SchedulingFormat.dayHeader(_visibleDate),
    _CalendarView.week => SchedulingFormat.weekRange(_visibleDate),
    _CalendarView.month => SchedulingFormat.monthYear(_visibleDate),
  };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        bottom: false,
        child: _loading
            ? const LoadingStateView(message: 'Takvimin yükleniyor...')
            : _error != null
            ? ErrorStateView(message: _error!, onRetry: _load)
            : _content(),
      ),
      floatingActionButton: (_loading || _error != null)
          ? null
          : FloatingActionButton.extended(
              onPressed: _addEntry,
              backgroundColor: AppColors.primary,
              foregroundColor: Colors.white,
              icon: const Icon(Icons.add_rounded),
              label: const Text('Ders ekle'),
            ),
      bottomNavigationBar: const StudentBottomNav(
        current: StudentNavTab.calendar,
      ),
    );
  }

  Widget _content() {
    final dayItems = _dayOccurrences;

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 10, 16, 96),
        children: <Widget>[
          const AppPageHeader(
            title: 'Takvim',
            subtitle: 'Ders programın tek yerde.',
          ),
          const SizedBox(height: 12),
          _ViewSwitcher(
            value: _view,
            onChanged: (value) => setState(() => _view = value),
          ),
          const SizedBox(height: 14),
          _DateNavigator(
            label: _navTitle,
            onPrevious: () => _moveVisibleDate(-1),
            onToday: () {
              final now = DateTime.now();
              _setVisibleDate(DateTime(now.year, now.month, now.day));
            },
            onNext: () => _moveVisibleDate(1),
          ),
          const SizedBox(height: 16),
          Container(
            height: _calendarHeight(context),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(18),
              border: Border.all(color: AppColors.border),
              boxShadow: AppShadows.soft,
            ),
            clipBehavior: Clip.antiAlias,
            child: SfCalendar(
              key: ValueKey<String>(
                'student-calendar-${_view.name}-${_visibleDate.year}-${_visibleDate.month}',
              ),
              view: _syncfusionView,
              dataSource: _OccurrenceDataSource(_occurrences),
              initialDisplayDate: _visibleDate,
              initialSelectedDate: _visibleDate,
              todayHighlightColor: AppColors.primary,
              headerHeight: 0,
              backgroundColor: Colors.white,
              cellBorderColor: AppColors.border,
              firstDayOfWeek: 1,
              showCurrentTimeIndicator: true,
              selectionDecoration: BoxDecoration(
                color: Colors.transparent,
                border: Border.all(color: AppColors.primary, width: 1.5),
                borderRadius: BorderRadius.circular(10),
              ),
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
              onTap: _onCalendarTap,
              onViewChanged: _onViewChanged,
            ),
          ),
          const SizedBox(height: 16),
          _SelectedDayPanel(
            date: _visibleDate,
            items: dayItems,
            onEdit: _editEntry,
            onDelete: _deleteEntry,
          ),
        ],
      ),
    );
  }

  CalendarView get _syncfusionView => switch (_view) {
    _CalendarView.day => CalendarView.day,
    _CalendarView.week => CalendarView.week,
    _CalendarView.month => CalendarView.month,
  };

  double _calendarHeight(BuildContext context) {
    final available = MediaQuery.sizeOf(context).height - 320;
    return switch (_view) {
      _CalendarView.day => available.clamp(430.0, 620.0),
      _CalendarView.week => available.clamp(430.0, 620.0),
      _CalendarView.month => available.clamp(400.0, 520.0),
    };
  }
}

class _OccurrenceDataSource extends CalendarDataSource {
  _OccurrenceDataSource(List<CalendarOccurrence> source) {
    appointments = source;
  }

  CalendarOccurrence _at(int index) =>
      appointments![index] as CalendarOccurrence;

  @override
  DateTime getStartTime(int index) => _at(index).startAtUtc.toLocal();

  @override
  DateTime getEndTime(int index) => _at(index).endAtUtc.toLocal();

  @override
  String getSubject(int index) => _at(index).subject;

  @override
  Color getColor(int index) {
    final occ = _at(index);
    return occ.isTeacher
        ? AppColors.primary
        : SchedulingFormat.colorFromHex(occ.colorHex);
  }

  @override
  bool isAllDay(int index) => false;
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
                _CalendarView.day => 'Günlük',
                _CalendarView.week => 'Haftalık',
                _CalendarView.month => 'Aylık',
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

class _IconSurface extends StatelessWidget {
  const _IconSurface({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(12),
      onTap: onTap,
      child: Container(
        width: 46,
        height: 42,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: AppColors.border),
        ),
        child: Icon(icon, color: AppColors.textPrimary),
      ),
    );
  }
}

class _SelectedDayPanel extends StatelessWidget {
  const _SelectedDayPanel({
    required this.date,
    required this.items,
    required this.onEdit,
    required this.onDelete,
  });

  final DateTime date;
  final List<CalendarOccurrence> items;
  final ValueChanged<CalendarOccurrence> onEdit;
  final ValueChanged<CalendarOccurrence> onDelete;

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
            SchedulingFormat.dayHeader(date),
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w900,
            ),
          ),
          const SizedBox(height: 12),
          if (items.isEmpty)
            Text(
              'Bu güne ait ders yok.',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: AppColors.textSecondary,
                fontWeight: FontWeight.w600,
              ),
            )
          else
            ...items.map(
              (occ) => Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: _DayEventTile(
                  occ: occ,
                  onEdit: () => onEdit(occ),
                  onDelete: () => onDelete(occ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _DayEventTile extends StatelessWidget {
  const _DayEventTile({
    required this.occ,
    required this.onEdit,
    required this.onDelete,
  });

  final CalendarOccurrence occ;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final bool teacher = occ.isTeacher;
    final Color color = teacher
        ? AppColors.primary
        : SchedulingFormat.colorFromHex(occ.colorHex);
    final String title = occ.topic == null || occ.topic!.isEmpty
        ? occ.subject
        : '${occ.subject} · ${occ.topic}';
    final String subtitle = teacher && (occ.locationLabel ?? '').isNotEmpty
        ? '${SchedulingFormat.recurrenceSummary(occ.recurrenceRule)} · ${occ.locationLabel}'
        : SchedulingFormat.recurrenceSummary(occ.recurrenceRule);

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          width: 4,
          height: 52,
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
                  _TimePill(
                    label: teacher ? 'Öğretmen' : 'Kendi',
                    color: color,
                  ),
                  const SizedBox(width: 8),
                  Text(
                    SchedulingFormat.timeRange(occ.startAtUtc, occ.endAtUtc),
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
              const SizedBox(height: 2),
              Row(
                children: <Widget>[
                  Icon(
                    occ.isRecurring
                        ? Icons.repeat_rounded
                        : Icons.event_rounded,
                    size: 13,
                    color: AppColors.textSecondary,
                  ),
                  const SizedBox(width: 4),
                  Expanded(
                    child: Text(
                      subtitle,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
        if (occ.isEditable)
          PopupMenuButton<String>(
            icon: const Icon(
              Icons.more_vert_rounded,
              color: AppColors.textSecondary,
            ),
            onSelected: (value) {
              if (value == 'edit') onEdit();
              if (value == 'delete') onDelete();
            },
            itemBuilder: (_) => const <PopupMenuEntry<String>>[
              PopupMenuItem<String>(value: 'edit', child: Text('Düzenle')),
              PopupMenuItem<String>(value: 'delete', child: Text('Sil')),
            ],
          )
        else
          const Padding(
            padding: EdgeInsets.only(left: 4, top: 6),
            child: Icon(
              Icons.lock_outline_rounded,
              size: 18,
              color: AppColors.textMuted,
            ),
          ),
      ],
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
