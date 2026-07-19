import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/scheduling_format.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:flutter/material.dart';

/// Öğrencinin kendi ders programı girdisini (StudyScheduleEntry) oluşturma/düzenleme formu.
/// Öğretmen [LessonFormSheet]'inin öğrenci-tarafı karşılığıdır: öğrenci seçimi/ders formatı yoktur;
/// ders adı, saat, tek/tekrarlı, hatırlatma ve not alanları vardır. Tam ekran açılır; kaydı kendisi
/// yapar; öğretmen dersiyle saat çakışması (409) durumunda ekran açık kalır ve uyarı gösterir.
/// Başarıda `Navigator.pop(true)`.
///
/// [studentId] zorunlu. [initial] doluysa düzenleme modundadır.
class StudyEntryFormSheet extends StatefulWidget {
  const StudyEntryFormSheet({
    super.key,
    required this.studentId,
    this.initial,
    this.initialDate,
  });

  final String studentId;
  final StudyScheduleEntry? initial;
  final DateTime? initialDate;

  @override
  State<StudyEntryFormSheet> createState() => _StudyEntryFormSheetState();
}

enum _EntryMode { single, recurring }

enum _Frequency { daily, weekly, monthly }

enum _Weekday { monday, tuesday, wednesday, thursday, friday, saturday, sunday }

/// Hatırlatma seçenekleri (dk) — 0 = kapalı. Öğretmen ders formuyla aynı küme.
const List<int> _reminderOptions = <int>[0, 15, 30, 60, 1440];

String _reminderLabel(int minutes) {
  return switch (minutes) {
    0 => 'Kapalı',
    60 => '1 saat',
    1440 => '1 gün',
    _ => '$minutes dk',
  };
}

class _StudyEntryFormSheetState extends State<StudyEntryFormSheet> {
  final _formKey = GlobalKey<FormState>();
  final _subjectController = TextEditingController();
  final _topicController = TextEditingController();
  final _notesController = TextEditingController();

  _EntryMode _mode = _EntryMode.single;
  _Frequency _frequency = _Frequency.weekly;
  DateTime? _date;
  DateTime? _recurrenceEndDate;
  TimeOfDay? _startTime;
  TimeOfDay? _endTime;
  final Set<_Weekday> _weekdays = <_Weekday>{_Weekday.monday};
  int _reminderMinutes = 0;
  bool _saving = false;

  /// Ç-06: öğrencinin ders kataloğu — ders/konu katalog seçicisi (isim tutarlılığı).
  List<SubjectCatalog> _catalog = const <SubjectCatalog>[];

  bool get _isEdit => widget.initial != null;

  /// Girilen ders adına karşılık gelen katalog dersinin konuları (yoksa boş).
  List<TopicCatalog> get _topicsForSubject {
    final name = _subjectController.text.trim().toLowerCase();
    for (final s in _catalog) {
      if (s.name.trim().toLowerCase() == name) return s.topics;
    }
    return const <TopicCatalog>[];
  }

  Future<void> _loadCatalog() async {
    try {
      final subjects =
          await injector<StudyRepository>().listSubjects(widget.studentId);
      if (!mounted) return;
      setState(() => _catalog = subjects);
    } catch (_) {
      // Katalog alınamazsa serbest metin girişi her zaman mümkün.
    }
  }

  @override
  void initState() {
    super.initState();
    _loadCatalog();
    final edit = widget.initial;
    if (edit != null) {
      final start = edit.startAtUtc.toLocal();
      final end = edit.endAtUtc.toLocal();
      _date = DateTime(start.year, start.month, start.day);
      _startTime = TimeOfDay(hour: start.hour, minute: start.minute);
      _endTime = TimeOfDay(hour: end.hour, minute: end.minute);
      _subjectController.text = edit.subject;
      _topicController.text = edit.topic ?? '';
      _notesController.text = edit.notes ?? '';
      _reminderMinutes = edit.reminderOffsetMinutes ?? 0;
      // Düzenlemede tekrar kuralı korunur ama form tek/tekrarlı UI'sini basit tutar:
      // mevcut kural varsa "tekrarlı" olarak işaretlenir (kural aynen gönderilir).
      _mode = (edit.recurrenceRule ?? '').isNotEmpty
          ? _EntryMode.recurring
          : _EntryMode.single;
    } else {
      _date = widget.initialDate ?? DateTime.now();
      _startTime = const TimeOfDay(hour: 15, minute: 0);
      _endTime = const TimeOfDay(hour: 16, minute: 0);
      _weekdays
        ..clear()
        ..add(_weekdayFromDate(_date));
    }
  }

  @override
  void dispose() {
    _subjectController.dispose();
    _topicController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
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
                const SizedBox(height: 4),
                Row(
                  children: <Widget>[
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: <Widget>[
                          Text(
                            _isEdit ? 'Dersi düzenle' : 'Ders ekle',
                            style: Theme.of(context).textTheme.titleLarge
                                ?.copyWith(
                                  color: AppColors.textPrimary,
                                  fontWeight: FontWeight.w800,
                                ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            'Kendi çalışma programına ders ekle. Öğretmeninin planladığı dersleri sadece öğretmenin ekleyebilir.',
                            style: Theme.of(context).textTheme.bodySmall
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
                  const _Label('Ders tipi'),
                  const SizedBox(height: 8),
                  _Segmented<_EntryMode>(
                    values: const <_EntryMode>[
                      _EntryMode.single,
                      _EntryMode.recurring,
                    ],
                    selected: _mode,
                    labelOf: (v) =>
                        v == _EntryMode.single ? 'Tek ders' : 'Tekrarlı ders',
                    onChanged: (v) => setState(() {
                      _mode = v;
                      if (v == _EntryMode.single) {
                        _recurrenceEndDate = null;
                      }
                    }),
                  ),
                  const SizedBox(height: 16),
                ],
                const _Label('Ders adı'),
                const SizedBox(height: 8),
                if (_catalog.isNotEmpty) ...<Widget>[
                  _CatalogChips(
                    labels: <String>[for (final s in _catalog) s.name],
                    selected: _subjectController.text.trim(),
                    onSelected: (name) => setState(() {
                      _subjectController.text = name;
                      // Ders değişince önceki konu seçimi geçersiz olabilir.
                      _topicController.clear();
                    }),
                  ),
                  const SizedBox(height: 8),
                ],
                TextFormField(
                  controller: _subjectController,
                  decoration: _decoration('Örn. Matematik'),
                  onChanged: (_) => setState(() {}),
                  validator: (v) => (v == null || v.trim().isEmpty)
                      ? 'Ders adı zorunlu.'
                      : null,
                ),
                const SizedBox(height: 16),
                const _Label('Konu (isteğe bağlı)'),
                const SizedBox(height: 8),
                if (_topicsForSubject.isNotEmpty) ...<Widget>[
                  _CatalogChips(
                    labels: <String>[for (final t in _topicsForSubject) t.name],
                    selected: _topicController.text.trim(),
                    onSelected: (name) =>
                        setState(() => _topicController.text = name),
                  ),
                  const SizedBox(height: 8),
                ],
                TextFormField(
                  controller: _topicController,
                  decoration: _decoration('Örn. Türev'),
                ),
                const SizedBox(height: 16),
                _Label(
                  _mode == _EntryMode.recurring ? 'Başlangıç tarihi' : 'Tarih',
                ),
                const SizedBox(height: 8),
                _PickerField(
                  value: _date == null
                      ? 'Tarih seç'
                      : SchedulingFormat.longDate(_date!),
                  onTap: _pickDate,
                ),
                const SizedBox(height: 16),
                if (_mode == _EntryMode.recurring) ...<Widget>[
                  const _Label('Tekrar sıklığı'),
                  const SizedBox(height: 8),
                  _Segmented<_Frequency>(
                    values: const <_Frequency>[
                      _Frequency.daily,
                      _Frequency.weekly,
                      _Frequency.monthly,
                    ],
                    selected: _frequency,
                    labelOf: (v) => switch (v) {
                      _Frequency.daily => 'Günlük',
                      _Frequency.weekly => 'Haftalık',
                      _Frequency.monthly => 'Aylık',
                    },
                    onChanged: (v) => setState(() => _frequency = v),
                  ),
                  const SizedBox(height: 16),
                  const _Label('Bitiş tarihi'),
                  const SizedBox(height: 8),
                  _PickerField(
                    value: _recurrenceEndDate == null
                        ? 'Bitiş tarihi seç'
                        : SchedulingFormat.longDate(_recurrenceEndDate!),
                    onTap: _pickRecurrenceEnd,
                  ),
                  const SizedBox(height: 16),
                  if (_frequency == _Frequency.weekly) ...<Widget>[
                    const _Label('Hangi günler'),
                    const SizedBox(height: 8),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: _Weekday.values.map((day) {
                        final selected = _weekdays.contains(day);
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
                          child: Text(_weekdayShort(day)),
                        );
                      }).toList(),
                    ),
                    const SizedBox(height: 16),
                  ],
                ],
                Row(
                  children: <Widget>[
                    Expanded(
                      child: _PickerColumn(
                        label: 'Başlangıç saati',
                        value: _startTime == null
                            ? 'Saat seç'
                            : _hm(_startTime!),
                        onTap: () => _pickTime(isStart: true),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _PickerColumn(
                        label: 'Bitiş saati',
                        value: _endTime == null ? 'Saat seç' : _hm(_endTime!),
                        onTap: () => _pickTime(isStart: false),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                const _Label('Hatırlatma'),
                const SizedBox(height: 8),
                _Segmented<int>(
                  values: _reminderOptions,
                  selected: _reminderOptions.contains(_reminderMinutes)
                      ? _reminderMinutes
                      : 0,
                  labelOf: _reminderLabel,
                  onChanged: (v) => setState(() => _reminderMinutes = v),
                  fontSize: 11,
                ),
                const SizedBox(height: 16),
                const _Label('Not (isteğe bağlı)'),
                const SizedBox(height: 8),
                TextFormField(
                  controller: _notesController,
                  maxLines: 3,
                  maxLength: 180,
                  decoration: _decoration('Kısa bir not ekleyebilirsin'),
                ),
                const SizedBox(height: 8),
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
                    onPressed: _saving ? null : _submit,
                    child: _saving
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
                            _isEdit ? 'Değişiklikleri kaydet' : 'Dersi kaydet',
                          ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    if (_date == null || _startTime == null || _endTime == null) {
      _snack('Tarih ve saatleri seç.');
      return;
    }
    final startLocal = DateTime(
      _date!.year,
      _date!.month,
      _date!.day,
      _startTime!.hour,
      _startTime!.minute,
    );
    final endLocal = DateTime(
      _date!.year,
      _date!.month,
      _date!.day,
      _endTime!.hour,
      _endTime!.minute,
    );
    if (!endLocal.isAfter(startLocal)) {
      _snack('Bitiş saati başlangıçtan sonra olmalı.');
      return;
    }
    if (_mode == _EntryMode.recurring) {
      if (_recurrenceEndDate == null) {
        _snack('Tekrarlı ders için bitiş tarihi seç.');
        return;
      }
      if (_frequency == _Frequency.weekly && _weekdays.isEmpty) {
        _snack('En az bir gün seç.');
        return;
      }
    }

    final entry = StudyScheduleEntry(
      id: widget.initial?.id ?? '',
      studentId: widget.studentId,
      subject: _subjectController.text.trim(),
      topic: _topicController.text.trim().isEmpty
          ? null
          : _topicController.text.trim(),
      startAtUtc: startLocal.toUtc(),
      endAtUtc: endLocal.toUtc(),
      timeZone: 'Europe/Istanbul',
      recurrenceRule: _buildRecurrenceRule(),
      reminderOffsetMinutes: _reminderMinutes,
      notes: _notesController.text.trim().isEmpty
          ? null
          : _notesController.text.trim(),
    );

    setState(() => _saving = true);
    try {
      final repo = injector<SchedulingRepository>();
      if (_isEdit) {
        await repo.updateStudyEntry(entry);
      } else {
        await repo.createStudyEntry(studentId: widget.studentId, entry: entry);
      }
      if (!mounted) return;
      Navigator.of(context).pop(true);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _saving = false);
      _snack(e.message);
    }
  }

  String? _buildRecurrenceRule() {
    if (_mode != _EntryMode.recurring) {
      // Düzenlemede mevcut kural korunur (form tek olarak açıldıysa değiştirme).
      return _isEdit ? widget.initial?.recurrenceRule : null;
    }
    final until = _recurrenceEndDate == null
        ? ''
        : ';UNTIL=${_untilStamp(_recurrenceEndDate!)}';
    return switch (_frequency) {
      _Frequency.daily => 'FREQ=DAILY$until',
      _Frequency.weekly =>
        'FREQ=WEEKLY;BYDAY=${_weekdays.map(_weekdayCode).join(',')}$until',
      _Frequency.monthly => 'FREQ=MONTHLY$until',
    };
  }

  String _untilStamp(DateTime date) {
    final utc = DateTime(date.year, date.month, date.day, 23, 59).toUtc();
    final s = utc.toIso8601String().replaceAll('-', '').replaceAll(':', '');
    return '${s.split('.').first}Z';
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _date ?? now,
      firstDate: now.subtract(const Duration(days: 7)),
      lastDate: now.add(const Duration(days: 365)),
    );
    if (picked != null) {
      setState(() {
        _date = picked;
        if (_mode == _EntryMode.recurring && _weekdays.length <= 1) {
          _weekdays
            ..clear()
            ..add(_weekdayFromDate(picked));
        }
      });
    }
  }

  Future<void> _pickRecurrenceEnd() async {
    final anchor = _date ?? DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _recurrenceEndDate ?? anchor.add(const Duration(days: 30)),
      firstDate: anchor,
      lastDate: anchor.add(const Duration(days: 365 * 2)),
    );
    if (picked != null) setState(() => _recurrenceEndDate = picked);
  }

  Future<void> _pickTime({required bool isStart}) async {
    final picked = await showTimePicker(
      context: context,
      initialTime: isStart
          ? (_startTime ?? const TimeOfDay(hour: 15, minute: 0))
          : (_endTime ?? const TimeOfDay(hour: 16, minute: 0)),
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

  void _toggleWeekday(_Weekday day) {
    setState(() {
      if (_weekdays.contains(day)) {
        if (_weekdays.length > 1) _weekdays.remove(day);
      } else {
        _weekdays.add(day);
      }
    });
  }

  void _snack(String message) {
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }

  _Weekday _weekdayFromDate(DateTime? date) =>
      _Weekday.values[(date ?? DateTime.now()).weekday - 1];

  String _weekdayShort(_Weekday d) => switch (d) {
    _Weekday.monday => 'Pzt',
    _Weekday.tuesday => 'Sal',
    _Weekday.wednesday => 'Çar',
    _Weekday.thursday => 'Per',
    _Weekday.friday => 'Cum',
    _Weekday.saturday => 'Cmt',
    _Weekday.sunday => 'Paz',
  };

  String _weekdayCode(_Weekday d) => switch (d) {
    _Weekday.monday => 'MO',
    _Weekday.tuesday => 'TU',
    _Weekday.wednesday => 'WE',
    _Weekday.thursday => 'TH',
    _Weekday.friday => 'FR',
    _Weekday.saturday => 'SA',
    _Weekday.sunday => 'SU',
  };

  String _hm(TimeOfDay t) =>
      '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

  InputDecoration _decoration(String hint) {
    return InputDecoration(
      hintText: hint,
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
}

/// Ç-06: katalogdan hızlı seçim çipleri (ders/konu). Seçilen çip ilgili metin alanını doldurur;
/// serbest metin girişi de her zaman mümkündür.
class _CatalogChips extends StatelessWidget {
  const _CatalogChips({
    required this.labels,
    required this.selected,
    required this.onSelected,
  });

  final List<String> labels;
  final String selected;
  final ValueChanged<String> onSelected;

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: <Widget>[
        for (final label in labels)
          ChoiceChip(
            label: Text(label),
            selected: label.trim().toLowerCase() == selected.toLowerCase(),
            onSelected: (_) => onSelected(label),
            showCheckmark: false,
            selectedColor: AppColors.primaryLight,
            labelStyle: TextStyle(
              color: label.trim().toLowerCase() == selected.toLowerCase()
                  ? AppColors.primary
                  : AppColors.textSecondary,
              fontWeight: FontWeight.w700,
              fontSize: 13,
            ),
            backgroundColor: AppColors.surface,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(999),
              side: const BorderSide(color: AppColors.border),
            ),
          ),
      ],
    );
  }
}

class _Label extends StatelessWidget {
  const _Label(this.text);
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

class _PickerField extends StatelessWidget {
  const _PickerField({required this.value, required this.onTap});
  final String value;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final isPlaceholder = value.contains('seç');
    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: onTap,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(color: AppColors.border),
        ),
        child: Text(
          value,
          style: TextStyle(
            color: isPlaceholder
                ? AppColors.textSecondary
                : AppColors.textPrimary,
            fontWeight: FontWeight.w500,
          ),
        ),
      ),
    );
  }
}

class _PickerColumn extends StatelessWidget {
  const _PickerColumn({
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
        _Label(label),
        const SizedBox(height: 8),
        _PickerField(value: value, onTap: onTap),
      ],
    );
  }
}

class _Segmented<T> extends StatelessWidget {
  const _Segmented({
    required this.values,
    required this.selected,
    required this.labelOf,
    required this.onChanged,
    this.fontSize = 12,
  });

  final List<T> values;
  final T selected;
  final String Function(T value) labelOf;
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
          final isSelected = value == selected;
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
                padding: const EdgeInsets.symmetric(horizontal: 6),
                child: Text(
                  labelOf(value),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: fontSize,
                    fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
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
