import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_state.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// Takvim, dersler ve ana sayfadaki "Ders Ekle" girislerinin tamaminin kullandigi
/// **tek** ders olusturma formu. Tek/tekrarli ders secimi, ogrenci-bazli ders
/// secimi, baslangic/bitis saati, format ve haftalik program onizlemesi icerir.
///
/// `SchedulingCubit` cagiran tarafindan `BlocProvider.value` ile saglanir (kayit
/// + isSaving/successMessage durumu icin). [students] ve [existingLessons] hazir
/// veri olarak gecirilir (onizleme/cakisma kontrolu icin).
class LessonFormSheet extends StatefulWidget {
  const LessonFormSheet({
    super.key,
    required this.teacherUserId,
    this.students = const <StudentProfile>[],
    required this.existingLessons,
    this.initialDate,
    this.initialHour = 10,
    this.initialLesson,
    this.studentName,
  });

  final String teacherUserId;
  final List<StudentProfile> students;
  final List<LessonSchedule> existingLessons;
  final DateTime? initialDate;
  final int initialHour;

  /// Doluysa form **duzenleme** modundadir; bu dersi gunceller (yerine yeni
  /// olusturmaz). Duzenlemede ogrenci sabittir, tekrar secimi gizlenir.
  final LessonSchedule? initialLesson;

  /// Duzenleme modunda gosterilecek (salt-okunur) ogrenci adi.
  final String? studentName;

  @override
  State<LessonFormSheet> createState() => _LessonFormSheetState();
}

enum _LessonCreateMode { single, recurring }

enum _LessonFormatOption { faceToFace, online }

enum _RecurrenceFrequency { daily, weekly, monthly }

enum _WeekdayChoice {
  monday,
  tuesday,
  wednesday,
  thursday,
  friday,
  saturday,
  sunday,
}

class _LessonFormSheetState extends State<LessonFormSheet> {
  final _formKey = GlobalKey<FormState>();
  final _notesController = TextEditingController();
  final _meetingLinkController = TextEditingController();
  final _subjectController = TextEditingController();

  bool get _isEdit => widget.initialLesson != null;

  _LessonCreateMode _mode = _LessonCreateMode.single;
  _LessonFormatOption _format = _LessonFormatOption.faceToFace;
  _RecurrenceFrequency _recurrenceFrequency = _RecurrenceFrequency.weekly;
  int _reminderMinutes = 60;
  StudentProfile? _selectedStudent;
  String? _selectedSubject;
  DateTime? _selectedDate;
  DateTime? _recurrenceEndDate;
  TimeOfDay? _startTime;
  TimeOfDay? _endTime;
  final Set<_WeekdayChoice> _selectedWeekdays = <_WeekdayChoice>{
    _WeekdayChoice.monday,
  };

  @override
  void initState() {
    super.initState();
    final edit = widget.initialLesson;
    if (edit != null) {
      final start = edit.startAtUtc.toLocal();
      final end = edit.endAtUtc.toLocal();
      _selectedDate = DateTime(start.year, start.month, start.day);
      _startTime = TimeOfDay(hour: start.hour, minute: start.minute);
      _endTime = TimeOfDay(hour: end.hour, minute: end.minute);
      _subjectController.text = edit.subject;
      _format = edit.lessonFormat.toLowerCase().contains('online')
          ? _LessonFormatOption.online
          : _LessonFormatOption.faceToFace;
      _meetingLinkController.text = edit.meetingUrl ?? '';
      _notesController.text = edit.notes ?? '';
      _reminderMinutes = edit.reminderOffsetMinutes ?? 60;
      return;
    }
    _selectedDate = widget.initialDate ?? DateTime.now();
    _startTime = TimeOfDay(hour: widget.initialHour, minute: 0);
    _endTime = TimeOfDay(hour: widget.initialHour + 1, minute: 0);
    _selectedWeekdays
      ..clear()
      ..add(_weekdayFromDate(_selectedDate));
    if (widget.students.isNotEmpty) {
      _selectedStudent = widget.students.first;
      final subjects = _subjectsForStudent(_selectedStudent);
      _selectedSubject = subjects.isNotEmpty ? subjects.first : null;
    }
  }

  @override
  void dispose() {
    _notesController.dispose();
    _meetingLinkController.dispose();
    _subjectController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final students = widget.students;
    final subjects = _subjectsForStudent(_selectedStudent);

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
                          color: AppColors.border,
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
                                _isEdit ? 'Dersi Duzenle' : 'Ders Ekle',
                                style: Theme.of(context).textTheme.titleLarge
                                    ?.copyWith(
                                      color: AppColors.textPrimary,
                                      fontWeight: FontWeight.w800,
                                    ),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                _isEdit
                                    ? 'Ders bilgilerini guncelle.'
                                    : 'Ders detaylarini girerek yeni plan olustur.',
                                style: Theme.of(context).textTheme.bodyMedium
                                    ?.copyWith(color: AppColors.textSecondary),
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
                              color: AppColors.background,
                              borderRadius: BorderRadius.circular(14),
                              border: Border.all(color: AppColors.border),
                            ),
                            child: const Icon(
                              Icons.close_rounded,
                              color: AppColors.textPrimary,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 20),
                    if (!_isEdit) ...<Widget>[
                      const _SheetLabel(text: 'Ders tipi'),
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
                              _recurrenceEndDate = null;
                              _selectedWeekdays
                                ..clear()
                                ..add(_weekdayFromDate(_selectedDate));
                            }
                          });
                        },
                      ),
                      const SizedBox(height: 16),
                    ],
                    // Ogrenci
                    if (_isEdit) ...<Widget>[
                      const _SheetLabel(text: 'Ogrenci'),
                      const SizedBox(height: 8),
                      _ReadonlyBox(text: widget.studentName ?? 'Ogrenci'),
                      const SizedBox(height: 16),
                    ] else if (students.isEmpty)
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 12),
                        child: Text(
                          'Once ogrenci eklemelisin.',
                          style: Theme.of(context).textTheme.bodyMedium
                              ?.copyWith(color: AppColors.textSecondary),
                        ),
                      )
                    else ...<Widget>[
                      const _SheetLabel(text: 'Ogrenci secimi'),
                      const SizedBox(height: 8),
                      DropdownButtonFormField<StudentProfile>(
                        initialValue: _selectedStudent,
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
                        validator: (value) =>
                            value == null ? 'Ogrenci secimi zorunlu.' : null,
                      ),
                      const SizedBox(height: 16),
                    ],
                    // Ders konusu
                    if (_isEdit) ...<Widget>[
                      const _SheetLabel(text: 'Ders adi'),
                      const SizedBox(height: 8),
                      TextFormField(
                        controller: _subjectController,
                        decoration: _inputDecoration('Matematik'),
                        validator: (value) =>
                            (value == null || value.trim().isEmpty)
                            ? 'Ders adi zorunlu.'
                            : null,
                      ),
                      const SizedBox(height: 16),
                    ] else if (students.isNotEmpty) ...<Widget>[
                      const _SheetLabel(text: 'Ders secimi'),
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
                    ],
                    if (_isEdit || students.isNotEmpty) ...<Widget>[
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
                                  ? AppColors.textSecondary
                                  : AppColors.textPrimary,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),
                      if (_mode == _LessonCreateMode.recurring) ...<Widget>[
                        const _SheetLabel(text: 'Tekrar sikligi'),
                        const SizedBox(height: 8),
                        _WideSegmentedControl<_RecurrenceFrequency>(
                          values: const <_RecurrenceFrequency>[
                            _RecurrenceFrequency.daily,
                            _RecurrenceFrequency.weekly,
                            _RecurrenceFrequency.monthly,
                          ],
                          selectedValue: _recurrenceFrequency,
                          labelBuilder: _frequencyLabel,
                          onChanged: (value) {
                            setState(() => _recurrenceFrequency = value);
                          },
                        ),
                        const SizedBox(height: 16),
                        const _SheetLabel(text: 'Bitis tarihi'),
                        const SizedBox(height: 8),
                        InkWell(
                          borderRadius: BorderRadius.circular(18),
                          onTap: _pickRecurrenceEndDate,
                          child: InputDecorator(
                            decoration: _inputDecoration('Bitis tarihi sec'),
                            child: Text(
                              _recurrenceEndDate == null
                                  ? 'Bitis tarihi sec'
                                  : _formatDate(_recurrenceEndDate!),
                              style: TextStyle(
                                color: _recurrenceEndDate == null
                                    ? AppColors.textSecondary
                                    : AppColors.textPrimary,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),
                        if (_recurrenceFrequency ==
                            _RecurrenceFrequency.weekly) ...<Widget>[
                          const _SheetLabel(text: 'Hangi gunler'),
                          const SizedBox(height: 8),
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: _WeekdayChoice.values.map((day) {
                              final selected = _selectedWeekdays.contains(day);
                              return OutlinedButton(
                                onPressed: () => _toggleWeekday(day),
                                style: OutlinedButton.styleFrom(
                                  backgroundColor: selected
                                      ? AppColors.primary
                                      : Colors.white,
                                  foregroundColor: selected
                                      ? Colors.white
                                      : AppColors.textSecondary,
                                  side: BorderSide(
                                    color: selected
                                        ? AppColors.primary
                                        : AppColors.border,
                                  ),
                                  shape: RoundedRectangleBorder(
                                    borderRadius: BorderRadius.circular(999),
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
                      const _SheetLabel(text: 'Ders sekli'),
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
                      if (_format == _LessonFormatOption.online) ...<Widget>[
                        const SizedBox(height: 16),
                        const _SheetLabel(text: 'Toplanti linki'),
                        const SizedBox(height: 8),
                        TextFormField(
                          controller: _meetingLinkController,
                          keyboardType: TextInputType.url,
                          decoration: _inputDecoration(
                            'https://zoom.us/... veya Meet linki',
                          ),
                        ),
                      ],
                      const SizedBox(height: 16),
                      const _SheetLabel(text: 'Hatirlatma'),
                      const SizedBox(height: 8),
                      _WideSegmentedControl<int>(
                        values: _reminderOptions,
                        selectedValue:
                            _reminderOptions.contains(_reminderMinutes)
                            ? _reminderMinutes
                            : 60,
                        labelBuilder: _reminderLabel,
                        onChanged: (value) =>
                            setState(() => _reminderMinutes = value),
                        fontSize: 11,
                      ),
                      const SizedBox(height: 16),
                      _WeeklyPreviewCard(
                        draft: _buildDraft(),
                        existingLessons: widget.existingLessons,
                        studentNames: <String, String>{
                          for (final s in widget.students) s.id: s.fullName,
                        },
                      ),
                      const SizedBox(height: 16),
                      const _SheetLabel(text: 'Not'),
                      const SizedBox(height: 8),
                      TextFormField(
                        controller: _notesController,
                        maxLines: 3,
                        maxLength: 180,
                        decoration: _inputDecoration(
                          'Ders notu ekleyebilirsin (istege bagli)',
                        ),
                      ),
                      const SizedBox(height: 12),
                      SizedBox(
                        width: double.infinity,
                        child: FilledButton(
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.primary,
                            foregroundColor: Colors.white,
                            padding: const EdgeInsets.symmetric(vertical: 14),
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
                                    valueColor: AlwaysStoppedAnimation<Color>(
                                      Colors.white,
                                    ),
                                  ),
                                )
                              : Text(
                                  _isEdit
                                      ? 'Degisiklikleri Kaydet'
                                      : 'Dersi Kaydet',
                                ),
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
  }

  _LessonDraft? _buildDraft() {
    if (_selectedDate == null || _startTime == null || _endTime == null) {
      return null;
    }
    final start = DateTime(
      _selectedDate!.year,
      _selectedDate!.month,
      _selectedDate!.day,
      _startTime!.hour,
      _startTime!.minute,
    );
    final end = DateTime(
      _selectedDate!.year,
      _selectedDate!.month,
      _selectedDate!.day,
      _endTime!.hour,
      _endTime!.minute,
    );
    final subject = _isEdit
        ? _subjectController.text.trim()
        : (_selectedSubject ?? '');
    return _LessonDraft(
      start: start,
      end: end,
      title: subject.isEmpty ? 'Yeni ders' : subject,
      studentName: _isEdit ? widget.studentName : _selectedStudent?.fullName,
    );
  }

  List<String> _subjectsForStudent(StudentProfile? student) {
    if (student == null) {
      return const <String>[];
    }
    final subjects = student.subjects
        .map((item) => item.subject.trim())
        .where((item) => item.isNotEmpty)
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
      _snack(context, 'Tarih secimi zorunlu.');
      return;
    }
    if (_startTime == null || _endTime == null) {
      _snack(context, 'Baslangic ve bitis saati secilmeli.');
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
      _snack(context, 'Bitis saati baslangic saatinden sonra olmali.');
      return;
    }

    if (_mode == _LessonCreateMode.recurring) {
      if (_recurrenceEndDate == null) {
        _snack(context, 'Tekrarli ders icin bitis tarihi sec.');
        return;
      }
      if (_recurrenceFrequency == _RecurrenceFrequency.weekly &&
          _selectedWeekdays.isEmpty) {
        _snack(context, 'En az bir gun secmelisin.');
        return;
      }
    }

    final isOnline = _format == _LessonFormatOption.online;
    final meetingLink = _meetingLinkController.text.trim();
    final cubit = context.read<SchedulingCubit>();

    if (_isEdit) {
      final base = widget.initialLesson!;
      final updated = LessonSchedule(
        id: base.id,
        teacherUserId: base.teacherUserId,
        studentId: base.studentId,
        subject: _subjectController.text.trim(),
        lessonFormat: isOnline ? 'Online' : 'InPerson',
        startAtUtc: startLocal.toUtc(),
        endAtUtc: endLocal.toUtc(),
        timeZone: base.timeZone,
        status: base.status,
        recurrenceRule: base.recurrenceRule,
        reminderOffsetMinutes: _reminderMinutes,
        locationLabel: isOnline ? 'Online' : 'Yuz yuze',
        meetingUrl: isOnline && meetingLink.isNotEmpty ? meetingLink : null,
        notes: _notesController.text.trim().isEmpty
            ? null
            : _notesController.text.trim(),
      );
      await cubit.updateLesson(updated);
      return;
    }

    final lesson = LessonSchedule(
      id: '',
      teacherUserId: widget.teacherUserId,
      studentId: _selectedStudent!.id,
      subject: _selectedSubject!,
      lessonFormat: isOnline ? 'Online' : 'InPerson',
      startAtUtc: startLocal.toUtc(),
      endAtUtc: endLocal.toUtc(),
      timeZone: 'Europe/Istanbul',
      recurrenceRule: _buildRecurrenceRule(),
      reminderOffsetMinutes: _reminderMinutes,
      locationLabel: isOnline ? 'Online' : 'Yuz yuze',
      meetingUrl: isOnline && meetingLink.isNotEmpty ? meetingLink : null,
      notes: _notesController.text.trim().isEmpty
          ? null
          : _notesController.text.trim(),
    );

    await cubit.createLesson(lesson);
  }

  void _snack(BuildContext context, String message) {
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }

  String? _buildRecurrenceRule() {
    if (_mode != _LessonCreateMode.recurring) {
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
        borderSide: const BorderSide(color: AppColors.border),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(18),
        borderSide: const BorderSide(color: AppColors.primary),
      ),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(18),
        borderSide: const BorderSide(color: AppColors.border),
      ),
    );
  }

  String _formatDate(DateTime date) {
    return '${date.day} ${_months[date.month]} ${date.year} ${_weekdaysLong[date.weekday - 1]}';
  }

  String _formatTime(TimeOfDay time) {
    final hour = time.hour.toString().padLeft(2, '0');
    final minute = time.minute.toString().padLeft(2, '0');
    return '$hour:$minute';
  }
}

/// Hatırlatma seçenekleri (dk) — 0 = kapalı. Öğrenci program formuyla aynı küme.
const List<int> _reminderOptions = <int>[0, 15, 30, 60, 1440];

String _reminderLabel(int minutes) {
  return switch (minutes) {
    0 => 'Kapali',
    60 => '1 saat',
    1440 => '1 gun',
    _ => '$minutes dk',
  };
}

const List<String> _months = <String>[
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

const List<String> _weekdaysLong = <String>[
  'Pazartesi',
  'Sali',
  'Carsamba',
  'Persembe',
  'Cuma',
  'Cumartesi',
  'Pazar',
];

/// Forma girilen taslak dersin haftalik takvimdeki yerini gosteren onizleme.
class _LessonDraft {
  const _LessonDraft({
    required this.start,
    required this.end,
    required this.title,
    this.studentName,
  });

  final DateTime start;
  final DateTime end;
  final String title;
  final String? studentName;
}

class _WeeklyPreviewCard extends StatelessWidget {
  const _WeeklyPreviewCard({
    required this.draft,
    required this.existingLessons,
    required this.studentNames,
  });

  final _LessonDraft? draft;
  final List<LessonSchedule> existingLessons;
  final Map<String, String> studentNames;

  bool get _hasConflict {
    final d = draft;
    if (d == null) return false;
    return existingLessons.any((l) {
      if (l.status == 'Cancelled') return false;
      final s = l.startAtUtc.toLocal();
      final e = l.endAtUtc.toLocal();
      return _sameDay(s, d.start) && d.start.isBefore(e) && d.end.isAfter(s);
    });
  }

  @override
  Widget build(BuildContext context) {
    final conflict = _hasConflict;
    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: draft == null ? null : () => _openModal(context),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: AppColors.background,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(
            color: conflict
                ? AppColors.accentRed.withValues(alpha: 0.45)
                : AppColors.border,
          ),
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 42,
              height: 42,
              decoration: BoxDecoration(
                color: AppColors.primaryLight,
                borderRadius: BorderRadius.circular(14),
              ),
              child: const Icon(
                Icons.calendar_view_week_rounded,
                color: AppColors.primary,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'Haftalik programini goster',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                  const SizedBox(height: 3),
                  Text(
                    'Secilen dersi haftalik takvimde kontrol et.',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 10),
            _StatusPill(
              label: conflict ? 'Cakisma var' : 'Uygun',
              color: conflict ? AppColors.accentRed : AppColors.accentGreen,
            ),
            const SizedBox(width: 4),
            const Icon(
              Icons.chevron_right_rounded,
              color: AppColors.textSecondary,
            ),
          ],
        ),
      ),
    );
  }

  void _openModal(BuildContext context) {
    final d = draft!;
    final weekStart = _startOfWeek(d.start);
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (sheetContext) {
        return FractionallySizedBox(
          heightFactor: 0.82,
          child: _WeeklyProgramModal(
            weekStart: weekStart,
            draft: d,
            existingLessons: existingLessons,
            studentNames: studentNames,
            hasConflict: _hasConflict,
          ),
        );
      },
    );
  }
}

class _WeeklyProgramModal extends StatelessWidget {
  const _WeeklyProgramModal({
    required this.weekStart,
    required this.draft,
    required this.existingLessons,
    required this.studentNames,
    required this.hasConflict,
  });

  final DateTime weekStart;
  final _LessonDraft draft;
  final List<LessonSchedule> existingLessons;
  final Map<String, String> studentNames;
  final bool hasConflict;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 14, 20, 20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Center(
            child: Container(
              width: 48,
              height: 5,
              decoration: BoxDecoration(
                color: AppColors.border,
                borderRadius: BorderRadius.circular(999),
              ),
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  'Haftalik Program',
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              _StatusPill(
                label: hasConflict ? 'Cakisma var' : 'Uygun',
                color: hasConflict
                    ? AppColors.accentRed
                    : AppColors.accentGreen,
              ),
            ],
          ),
          const SizedBox(height: 14),
          Expanded(
            child: ListView.separated(
              itemCount: 7,
              separatorBuilder: (_, _) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                final day = weekStart.add(Duration(days: index));
                return _WeekDayRow(
                  day: day,
                  draft: _sameDay(day, draft.start) ? draft : null,
                  lessons:
                      existingLessons
                          .where(
                            (l) =>
                                l.status != 'Cancelled' &&
                                _sameDay(l.startAtUtc.toLocal(), day),
                          )
                          .toList()
                        ..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc)),
                  studentNames: studentNames,
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _WeekDayRow extends StatelessWidget {
  const _WeekDayRow({
    required this.day,
    required this.draft,
    required this.lessons,
    required this.studentNames,
  });

  final DateTime day;
  final _LessonDraft? draft;
  final List<LessonSchedule> lessons;
  final Map<String, String> studentNames;

  @override
  Widget build(BuildContext context) {
    final isEmpty = lessons.isEmpty && draft == null;
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            '${_weekdaysLong[day.weekday - 1]} · ${day.day} ${_months[day.month]}',
            style: Theme.of(context).textTheme.labelLarge?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 8),
          if (isEmpty)
            Text(
              'Ders yok',
              style: Theme.of(
                context,
              ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
            )
          else ...<Widget>[
            for (final l in lessons)
              _SlotRow(
                time:
                    '${_hm(l.startAtUtc.toLocal())} - ${_hm(l.endAtUtc.toLocal())}',
                label: studentNames[l.studentId] == null
                    ? l.subject
                    : '${l.subject} · ${studentNames[l.studentId]}',
                highlighted: false,
              ),
            if (draft != null)
              _SlotRow(
                time: '${_hm(draft!.start)} - ${_hm(draft!.end)}',
                label: draft!.studentName == null
                    ? '${draft!.title} (yeni)'
                    : '${draft!.title} · ${draft!.studentName} (yeni)',
                highlighted: true,
              ),
          ],
        ],
      ),
    );
  }

  String _hm(DateTime d) =>
      '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
}

class _SlotRow extends StatelessWidget {
  const _SlotRow({
    required this.time,
    required this.label,
    required this.highlighted,
  });

  final String time;
  final String label;
  final bool highlighted;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 6),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: highlighted ? AppColors.primaryLight : Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(
          color: highlighted ? AppColors.primary : AppColors.border,
        ),
      ),
      child: Row(
        children: <Widget>[
          Text(
            time,
            style: TextStyle(
              color: highlighted ? AppColors.primary : AppColors.textSecondary,
              fontWeight: FontWeight.w700,
              fontSize: 12,
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                color: AppColors.textPrimary,
                fontWeight: highlighted ? FontWeight.w800 : FontWeight.w600,
                fontSize: 13,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _StatusPill extends StatelessWidget {
  const _StatusPill({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: color,
          fontWeight: FontWeight.w800,
          fontSize: 11,
        ),
      ),
    );
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
        color: AppColors.textPrimary,
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
        color: AppColors.background,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.border),
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
                  color: isSelected ? AppColors.primary : Colors.transparent,
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
                    color: isSelected ? Colors.white : AppColors.textSecondary,
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

class _ReadonlyBox extends StatelessWidget {
  const _ReadonlyBox({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
      decoration: BoxDecoration(
        color: AppColors.background,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: <Widget>[
          const Icon(
            Icons.person_rounded,
            size: 20,
            color: AppColors.textSecondary,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
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
                borderSide: const BorderSide(color: AppColors.border),
              ),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(18),
                borderSide: const BorderSide(color: AppColors.border),
              ),
            ),
            child: Text(
              value,
              style: TextStyle(
                color: value == 'Saat sec'
                    ? AppColors.textSecondary
                    : AppColors.textPrimary,
                fontWeight: FontWeight.w500,
              ),
            ),
          ),
        ),
      ],
    );
  }
}

bool _sameDay(DateTime a, DateTime b) =>
    a.year == b.year && a.month == b.month && a.day == b.day;

DateTime _startOfWeek(DateTime date) {
  final d = DateTime(date.year, date.month, date.day);
  return d.subtract(Duration(days: d.weekday - 1));
}
