import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/pages/assignment_follow_up_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_note_view_page.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/cubit/scheduling_state.dart';
import 'package:egitim_ussu_mobile/features/scheduling/presentation/widgets/lesson_form_sheet.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class LessonDetailPayload {
  const LessonDetailPayload({
    required this.studentName,
    required this.subject,
    required this.dateLabel,
    required this.timeLabel,
    required this.modeLabel,
    required this.accent,
    this.lessonId,
    this.lessonStatus,
    this.meetingUrl,
    this.lesson,
  });

  final String studentName;
  final String subject;
  final String dateLabel;
  final String timeLabel;
  final String modeLabel;
  final Color accent;
  final String? lessonId;
  final String? lessonStatus;

  /// Online dersler icin toplanti baglantisi.
  final String? meetingUrl;

  /// Kalici duzenleme (PUT) icin kaynak ders. Doluysa "Dersi Duzenle" gercek
  /// formu acar; null ise (or. demo/dashboard) kozmetik duzenleme kullanilir.
  final LessonSchedule? lesson;
}

class LessonDetailPage extends StatefulWidget {
  const LessonDetailPage({super.key, this.payload});

  final LessonDetailPayload? payload;

  @override
  State<LessonDetailPage> createState() => _LessonDetailPageState();
}

class _LessonDetailPageState extends State<LessonDetailPage> {
  int _selectedTab = 0;
  late final List<_NoteFileItem> _lessonNotes = <_NoteFileItem>[
    const _NoteFileItem(
      title: 'Polinomlar Konu Anlatim.pdf',
      meta: '1.2 MB  20 Mayis',
      accent: AppColors.accentBlue,
      noteText:
          'Polinomlarda temel kavramlar, toplama-cikarma ve carpma islemleri anlatildi. Ders sonunda yeni nesil soru ornekleri cozuldu.',
      sourceFilePath: null,
    ),
    const _NoteFileItem(
      title: 'Fonksiyonlar Ozet Notu.pdf',
      meta: '860 KB  18 Mayis',
      accent: AppColors.accentGreen,
      noteText:
          'Fonksiyon cesitleri, tanim kumesi ve deger kumesi uzerinden kisa bir tekrar yapildi. Eksik kalan noktalar ayrica not edildi.',
      sourceFilePath: null,
    ),
  ];

  static const _tabs = <String>['Ders notu', 'Odevler', 'Odeme'];

  LessonDetailPayload? _editedPayload;
  SchedulingCubit? _schedulingCubit;
  String? _lessonStatus;

  LessonDetailPayload get _payload =>
      _editedPayload ??
      widget.payload ??
      const LessonDetailPayload(
        studentName: 'Zeynep Demir',
        subject: 'Matematik',
        dateLabel: '20 Mayis 2025 Sali',
        timeLabel: '10:00 - 11:00',
        modeLabel: 'Online',
        accent: AppColors.accentBlue,
      );

  bool get _canComplete =>
      _payload.lessonId != null && _lessonStatus == 'Planned';

  @override
  void initState() {
    super.initState();
    _lessonStatus = widget.payload?.lessonStatus;
    if (widget.payload?.lessonId != null) {
      _schedulingCubit = SchedulingCubit.create();
    }
  }

  @override
  void dispose() {
    _schedulingCubit?.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final payload = _payload;

    final scaffold = Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        backgroundColor: AppColors.background,
        elevation: 0,
        surfaceTintColor: Colors.transparent,
        title: const Text('Ders Detayi'),
      ),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 0),
          child: Column(
            children: <Widget>[
              _HeroCard(payload: payload),
              const SizedBox(height: 16),
              Row(
                children: <Widget>[
                  Expanded(
                    child: _ActionCard(
                      icon: Icons.edit_calendar_rounded,
                      label: 'Dersi Duzenle',
                      onTap: _showEditLessonSheet,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: _ActionCard(
                      icon: Icons.note_add_rounded,
                      label: 'Ders Notu Ekle',
                      onTap: _showAddLessonNoteSheet,
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: _ActionCard(
                      icon: Icons.post_add_rounded,
                      label: 'Odev Ekle',
                      onTap: () => context.push(
                        '/assignments/demo-lesson-session',
                        extra: AssignmentFormContext(
                          studentName: payload.studentName,
                          lessonName: payload.subject,
                          lockSelection: true,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
              if (_canComplete) ...<Widget>[
                const SizedBox(height: 10),
                SizedBox(
                  width: double.infinity,
                  child: BlocBuilder<SchedulingCubit, SchedulingState>(
                    bloc: _schedulingCubit,
                    builder: (context, state) {
                      return FilledButton.icon(
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColors.accentGreen,
                          foregroundColor: Colors.white,
                          padding: const EdgeInsets.symmetric(vertical: 13),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(16),
                          ),
                        ),
                        onPressed: state.isSaving
                            ? null
                            : () => _schedulingCubit!.completeLesson(
                                lessonId: _payload.lessonId!,
                              ),
                        icon: state.isSaving
                            ? const SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: Colors.white,
                                ),
                              )
                            : const Icon(Icons.check_circle_outline_rounded),
                        label: const Text('Dersi Tamamla'),
                      );
                    },
                  ),
                ),
              ],
              if (payload.modeLabel == 'Online' &&
                  (payload.meetingUrl?.trim().isNotEmpty ?? false)) ...<Widget>[
                const SizedBox(height: 10),
                _JoinMeetingCard(url: payload.meetingUrl!.trim()),
              ],
              const SizedBox(height: 18),
              _DetailTabs(
                tabs: _tabs,
                selectedIndex: _selectedTab,
                onChanged: (index) => setState(() => _selectedTab = index),
              ),
              const SizedBox(height: 16),
              Expanded(
                child: AnimatedSwitcher(
                  duration: const Duration(milliseconds: 180),
                  child: KeyedSubtree(
                    key: ValueKey<int>(_selectedTab),
                    child: _buildTabContent(),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );

    final cubit = _schedulingCubit;
    if (cubit == null) return scaffold;

    return BlocListener<SchedulingCubit, SchedulingState>(
      bloc: cubit,
      listener: (context, state) {
        if (state.successMessage != null) {
          if (state.successMessage!.contains('tamamland')) {
            setState(() => _lessonStatus = 'Completed');
          }
          ScaffoldMessenger.of(
            context,
          ).showSnackBar(SnackBar(content: Text(state.successMessage!)));
        }
        if (state.errorMessage != null) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(state.errorMessage!),
              backgroundColor: Colors.red,
            ),
          );
        }
      },
      child: scaffold,
    );
  }

  Widget _buildTabContent() {
    switch (_selectedTab) {
      case 0:
        return ListView(
          padding: const EdgeInsets.only(bottom: 24),
          children: List<Widget>.generate(_lessonNotes.length, (index) {
            final item = _lessonNotes[index];
            return Padding(
              padding: EdgeInsets.only(
                bottom: index == _lessonNotes.length - 1 ? 0 : 12,
              ),
              child: _InfoFileCard(
                icon: index == 0
                    ? Icons.description_rounded
                    : Icons.description_outlined,
                iconColor: item.accent,
                title: item.title,
                subtitle: item.meta,
                trailingLabel: 'Goruntule',
                onTap: () => _openLessonNote(context, item),
              ),
            );
          }),
        );
      case 1:
        return ListView(
          padding: const EdgeInsets.only(bottom: 24),
          children: const <Widget>[
            _HomeworkCard(
              title: 'Polinomlar Test Cozumleri',
              subtitle: 'Teslim 22 Mayis 2025',
              statusLabel: 'Teslim edildi',
              actionLabel: 'Kontrol et',
            ),
            SizedBox(height: 12),
            _HomeworkCard(
              title: 'Carpanlara Ayirma Alistirmalari',
              subtitle: 'Teslim 25 Mayis 2025',
              statusLabel: 'Devam ediyor',
              actionLabel: 'Kontrol et',
              statusColor: AppColors.amber,
            ),
          ],
        );
      default:
        return ListView(
          padding: const EdgeInsets.only(bottom: 24),
          children: const <Widget>[
            _PaymentCard(
              title: 'Mayis Dersi Odeme Kaydi',
              subtitle: '2 Saat  20 Mayis 2025',
              amountLabel: '2.000 TL',
              statusLabel: 'Odendi',
            ),
            SizedBox(height: 12),
            _PaymentCard(
              title: 'Ek Etut Odeme Kaydi',
              subtitle: '1 Saat  23 Mayis 2025',
              amountLabel: '1.000 TL',
              statusLabel: 'Beklemede',
              statusColor: AppColors.amber,
            ),
          ],
        );
    }
  }

  Future<void> _showAddLessonNoteSheet() async {
    final formKey = GlobalKey<FormState>();
    final titleController = TextEditingController();
    final descriptionController = TextEditingController();
    PlatformFile? selectedFile;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (context) {
        return SafeArea(
          child: Padding(
            padding: EdgeInsets.only(
              left: 20,
              right: 20,
              top: 16,
              bottom: MediaQuery.of(context).viewInsets.bottom + 20,
            ),
            child: StatefulBuilder(
              builder: (context, setModalState) {
                return SingleChildScrollView(
                  child: Form(
                    key: formKey,
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
                              child: Text(
                                'Ders Notu Ekle',
                                style: Theme.of(context).textTheme.titleLarge
                                    ?.copyWith(
                                      color: AppColors.textPrimary,
                                      fontWeight: FontWeight.w800,
                                    ),
                              ),
                            ),
                            IconButton(
                              onPressed: () => Navigator.of(context).pop(),
                              icon: const Icon(Icons.close_rounded),
                            ),
                          ],
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'Dosya ve not bilgisini girerek bu derse yeni not ekle.',
                          style: Theme.of(context).textTheme.bodyMedium
                              ?.copyWith(color: AppColors.textSecondary),
                        ),
                        const SizedBox(height: 18),
                        _SheetField(
                          controller: titleController,
                          label: 'Not basligi',
                          hint: 'Polinomlar Konu Anlatimi',
                        ),
                        const SizedBox(height: 12),
                        Text(
                          'Dosya secimi',
                          style: Theme.of(context).textTheme.labelLarge
                              ?.copyWith(
                                color: AppColors.textPrimary,
                                fontWeight: FontWeight.w700,
                              ),
                        ),
                        const SizedBox(height: 8),
                        InkWell(
                          borderRadius: BorderRadius.circular(18),
                          onTap: () async {
                            final result = await FilePicker.platform.pickFiles(
                              allowMultiple: false,
                            );
                            if (result != null && result.files.isNotEmpty) {
                              setModalState(() {
                                selectedFile = result.files.first;
                              });
                            }
                          },
                          child: Container(
                            width: double.infinity,
                            padding: const EdgeInsets.all(16),
                            decoration: BoxDecoration(
                              color: Colors.white,
                              borderRadius: BorderRadius.circular(18),
                              border: Border.all(color: AppColors.border),
                            ),
                            child: Row(
                              children: <Widget>[
                                Container(
                                  width: 42,
                                  height: 42,
                                  decoration: BoxDecoration(
                                    color: AppColors.primary.withValues(
                                      alpha: 0.08,
                                    ),
                                    borderRadius: BorderRadius.circular(14),
                                  ),
                                  child: const Icon(
                                    Icons.attach_file_rounded,
                                    color: AppColors.primary,
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: <Widget>[
                                      Text(
                                        selectedFile?.name ?? 'Dosya sec',
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: Theme.of(context)
                                            .textTheme
                                            .titleSmall
                                            ?.copyWith(
                                              color: selectedFile == null
                                                  ? AppColors.textSecondary
                                                  : AppColors.textPrimary,
                                              fontWeight: FontWeight.w700,
                                            ),
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        selectedFile == null
                                            ? 'Dosya secildiginde boyutu burada gorunur'
                                            : _formatFileSize(
                                                selectedFile!.size,
                                              ),
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodySmall
                                            ?.copyWith(
                                              color: AppColors.textSecondary,
                                            ),
                                      ),
                                    ],
                                  ),
                                ),
                                const Icon(
                                  Icons.chevron_right_rounded,
                                  color: AppColors.textSecondary,
                                ),
                              ],
                            ),
                          ),
                        ),
                        const SizedBox(height: 12),
                        _SheetField(
                          controller: descriptionController,
                          label: 'Ders notu',
                          hint: 'Bu derste hangi konularin islendigini yaz.',
                          maxLines: 4,
                        ),
                        const SizedBox(height: 18),
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
                            onPressed: () {
                              if ((formKey.currentState?.validate() ?? false) ==
                                  false) {
                                return;
                              }
                              if (selectedFile == null) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  const SnackBar(
                                    content: Text('Lutfen bir dosya sec.'),
                                  ),
                                );
                                return;
                              }
                              setState(() {
                                _lessonNotes.insert(
                                  0,
                                  _NoteFileItem(
                                    title: selectedFile!.name,
                                    meta:
                                        '${_formatFileSize(selectedFile!.size)}  ${_payload.dateLabel.split(' ').take(2).join(' ')}',
                                    accent: AppColors.accentBlue,
                                    noteText:
                                        descriptionController.text
                                            .trim()
                                            .isEmpty
                                        ? titleController.text.trim()
                                        : descriptionController.text.trim(),
                                    sourceFilePath: selectedFile!.path,
                                  ),
                                );
                                _selectedTab = 0;
                              });
                              Navigator.of(context).pop();
                            },
                            child: const Text('Ders Notunu Kaydet'),
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          ),
        );
      },
    );
  }

  Future<void> _showEditLessonSheet() async {
    final lesson = _payload.lesson;
    final cubit = _schedulingCubit;
    // Tam ders verisi varsa kalici (PUT) duzenleme; yoksa kozmetik duzenleme.
    if (lesson == null || cubit == null) {
      return _showCosmeticEditSheet();
    }

    final base = _payload;
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (sheetContext) {
        return BlocProvider<SchedulingCubit>.value(
          value: cubit,
          child: LessonFormSheet(
            teacherUserId: lesson.teacherUserId,
            existingLessons: cubit.state.lessons,
            initialLesson: lesson,
            studentName: base.studentName,
          ),
        );
      },
    );

    final matches = cubit.state.lessons.where((l) => l.id == lesson.id);
    if (matches.isNotEmpty && mounted) {
      setState(() => _editedPayload = _payloadFromLesson(matches.first, base));
    }
  }

  LessonDetailPayload _payloadFromLesson(
    LessonSchedule lesson,
    LessonDetailPayload base,
  ) {
    final start = lesson.startAtUtc.toLocal();
    final end = lesson.endAtUtc.toLocal();
    final isOnline = lesson.lessonFormat.toLowerCase().contains('online');
    String hm(DateTime d) =>
        '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
    return LessonDetailPayload(
      studentName: base.studentName,
      subject: lesson.subject,
      dateLabel: _formatTrDate(start),
      timeLabel: '${hm(start)} - ${hm(end)}',
      modeLabel: isOnline ? 'Online' : 'Yuz yuze',
      accent: base.accent,
      lessonId: lesson.id,
      lessonStatus: lesson.status,
      meetingUrl: lesson.meetingUrl,
      lesson: lesson,
    );
  }

  Future<void> _showCosmeticEditSheet() async {
    final payload = _payload;
    final formKey = GlobalKey<FormState>();
    final subjectController = TextEditingController(text: payload.subject);
    final meetingController = TextEditingController(
      text: payload.meetingUrl ?? '',
    );
    var modeLabel = payload.modeLabel;
    var selectedDate = _parseTrDate(payload.dateLabel) ?? DateTime.now();
    final parsed = _parseTimeRange(payload.timeLabel);
    var startTime = parsed.$1 ?? const TimeOfDay(hour: 10, minute: 0);
    var endTime = parsed.$2 ?? const TimeOfDay(hour: 11, minute: 0);

    final updatedPayload = await showModalBottomSheet<LessonDetailPayload>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (context) {
        return SafeArea(
          child: Padding(
            padding: EdgeInsets.only(
              left: 20,
              right: 20,
              top: 16,
              bottom: MediaQuery.of(context).viewInsets.bottom + 20,
            ),
            child: StatefulBuilder(
              builder: (context, setModalState) {
                Future<void> pickDate() async {
                  final picked = await showDatePicker(
                    context: context,
                    initialDate: selectedDate,
                    firstDate: DateTime.now().subtract(
                      const Duration(days: 30),
                    ),
                    lastDate: DateTime.now().add(const Duration(days: 365)),
                  );
                  if (picked != null) {
                    setModalState(() => selectedDate = picked);
                  }
                }

                Future<void> pickTime({required bool isStart}) async {
                  final picked = await showTimePicker(
                    context: context,
                    initialTime: isStart ? startTime : endTime,
                  );
                  if (picked != null) {
                    setModalState(() {
                      if (isStart) {
                        startTime = picked;
                      } else {
                        endTime = picked;
                      }
                    });
                  }
                }

                return SingleChildScrollView(
                  child: Form(
                    key: formKey,
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
                              child: Text(
                                'Dersi Duzenle',
                                style: Theme.of(context).textTheme.titleLarge
                                    ?.copyWith(
                                      color: AppColors.textPrimary,
                                      fontWeight: FontWeight.w800,
                                    ),
                              ),
                            ),
                            IconButton(
                              onPressed: () => Navigator.of(context).pop(),
                              icon: const Icon(Icons.close_rounded),
                            ),
                          ],
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'Ders bilgilerini guncelle. Kaydedince detay karti aninda yenilenir.',
                          style: Theme.of(context).textTheme.bodyMedium
                              ?.copyWith(color: AppColors.textSecondary),
                        ),
                        const SizedBox(height: 18),
                        _SheetField(
                          controller: subjectController,
                          label: 'Ders adi',
                          hint: 'Matematik',
                        ),
                        const SizedBox(height: 14),
                        const _EditFieldLabel(text: 'Tarih'),
                        const SizedBox(height: 8),
                        _PickerBox(
                          value: _formatTrDate(selectedDate),
                          icon: Icons.event_rounded,
                          onTap: pickDate,
                        ),
                        const SizedBox(height: 14),
                        Row(
                          children: <Widget>[
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: <Widget>[
                                  const _EditFieldLabel(text: 'Baslangic'),
                                  const SizedBox(height: 8),
                                  _PickerBox(
                                    value: _hm(startTime),
                                    icon: Icons.schedule_rounded,
                                    onTap: () => pickTime(isStart: true),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: <Widget>[
                                  const _EditFieldLabel(text: 'Bitis'),
                                  const SizedBox(height: 8),
                                  _PickerBox(
                                    value: _hm(endTime),
                                    icon: Icons.schedule_rounded,
                                    onTap: () => pickTime(isStart: false),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 14),
                        const _EditFieldLabel(text: 'Ders sekli'),
                        const SizedBox(height: 8),
                        Row(
                          children: <Widget>[
                            for (final option in const <String>[
                              'Yuz yuze',
                              'Online',
                            ])
                              Expanded(
                                child: Padding(
                                  padding: EdgeInsets.only(
                                    right: option == 'Yuz yuze' ? 8 : 0,
                                  ),
                                  child: GestureDetector(
                                    behavior: HitTestBehavior.opaque,
                                    onTap: () =>
                                        setModalState(() => modeLabel = option),
                                    child: Container(
                                      padding: const EdgeInsets.symmetric(
                                        vertical: 13,
                                      ),
                                      alignment: Alignment.center,
                                      decoration: BoxDecoration(
                                        color: modeLabel == option
                                            ? AppColors.primary
                                            : Colors.white,
                                        borderRadius: BorderRadius.circular(14),
                                        border: Border.all(
                                          color: modeLabel == option
                                              ? AppColors.primary
                                              : AppColors.border,
                                        ),
                                      ),
                                      child: Text(
                                        option,
                                        style: TextStyle(
                                          color: modeLabel == option
                                              ? Colors.white
                                              : AppColors.textSecondary,
                                          fontWeight: FontWeight.w700,
                                        ),
                                      ),
                                    ),
                                  ),
                                ),
                              ),
                          ],
                        ),
                        if (modeLabel == 'Online') ...<Widget>[
                          const SizedBox(height: 12),
                          _SheetField(
                            controller: meetingController,
                            label: 'Toplanti linki',
                            hint: 'https://zoom.us/...',
                          ),
                        ],
                        const SizedBox(height: 18),
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
                            onPressed: () {
                              if (!(formKey.currentState?.validate() ??
                                  false)) {
                                return;
                              }
                              if (!_isAfter(endTime, startTime)) {
                                ScaffoldMessenger.of(context).showSnackBar(
                                  const SnackBar(
                                    content: Text(
                                      'Bitis saati baslangictan sonra olmali.',
                                    ),
                                  ),
                                );
                                return;
                              }
                              final link = meetingController.text.trim();
                              Navigator.of(context).pop(
                                LessonDetailPayload(
                                  studentName: payload.studentName,
                                  subject: subjectController.text.trim(),
                                  dateLabel: _formatTrDate(selectedDate),
                                  timeLabel:
                                      '${_hm(startTime)} - ${_hm(endTime)}',
                                  modeLabel: modeLabel,
                                  accent: payload.accent,
                                  lessonId: payload.lessonId,
                                  lessonStatus: payload.lessonStatus,
                                  meetingUrl:
                                      modeLabel == 'Online' && link.isNotEmpty
                                      ? link
                                      : null,
                                ),
                              );
                            },
                            child: const Text('Degisiklikleri Kaydet'),
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          ),
        );
      },
    );

    subjectController.dispose();
    meetingController.dispose();

    if (updatedPayload == null) {
      return;
    }
    setState(() => _editedPayload = updatedPayload);
    if (!mounted) {
      return;
    }
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Ders bilgileri guncellendi.')),
    );
  }

  String _formatFileSize(int bytes) {
    if (bytes >= 1024 * 1024) {
      return '${(bytes / (1024 * 1024)).toStringAsFixed(1)} MB';
    }
    if (bytes >= 1024) {
      return '${(bytes / 1024).toStringAsFixed(0)} KB';
    }
    return '$bytes B';
  }

  void _openLessonNote(BuildContext context, _NoteFileItem item) {
    context.push(
      '/lesson-sessions/detail/note',
      extra: LessonNoteViewPayload(
        title: item.title,
        meta: item.meta,
        noteText: item.noteText,
        accent: item.accent,
        sourceFilePath: item.sourceFilePath,
      ),
    );
  }
}

class _JoinMeetingCard extends StatelessWidget {
  const _JoinMeetingCard({required this.url});

  final String url;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: () async {
        await Clipboard.setData(ClipboardData(text: url));
        if (!context.mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Toplanti linki panoya kopyalandi.')),
        );
      },
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: AppColors.accentGreen.withValues(alpha: 0.08),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: AppColors.accentGreen.withValues(alpha: 0.35),
          ),
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 42,
              height: 42,
              decoration: BoxDecoration(
                color: AppColors.accentGreen.withValues(alpha: 0.14),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(
                Icons.videocam_rounded,
                color: AppColors.accentGreen,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'Toplantiya Katil',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    url,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.accentGreen,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            const Icon(
              Icons.copy_rounded,
              color: AppColors.accentGreen,
              size: 18,
            ),
          ],
        ),
      ),
    );
  }
}

class _HeroCard extends StatelessWidget {
  const _HeroCard({required this.payload});

  final LessonDetailPayload payload;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.soft,
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          _Avatar(name: payload.studentName, accent: payload.accent),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  payload.studentName,
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  payload.subject,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: <Widget>[
                    _MetaChip(
                      icon: Icons.calendar_today_rounded,
                      label: payload.dateLabel,
                    ),
                    _MetaChip(
                      icon: Icons.schedule_rounded,
                      label: payload.timeLabel,
                    ),
                    _MetaChip(
                      icon: payload.modeLabel == 'Online'
                          ? Icons.videocam_rounded
                          : Icons.groups_rounded,
                      label: payload.modeLabel,
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ActionCard extends StatelessWidget {
  const _ActionCard({required this.icon, required this.label, this.onTap});

  final IconData icon;
  final String label;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 92,
      child: InkWell(
        borderRadius: BorderRadius.circular(18),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 14),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: AppColors.border),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Icon(icon, color: AppColors.primary, size: 20),
              const SizedBox(height: 8),
              Text(
                label,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w700,
                  height: 1.2,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

const List<String> _trMonths = <String>[
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

const List<String> _trWeekdays = <String>[
  'Pazartesi',
  'Sali',
  'Carsamba',
  'Persembe',
  'Cuma',
  'Cumartesi',
  'Pazar',
];

String _formatTrDate(DateTime d) =>
    '${d.day} ${_trMonths[d.month]} ${d.year} ${_trWeekdays[d.weekday - 1]}';

DateTime? _parseTrDate(String label) {
  final parts = label.trim().split(RegExp(r'\s+'));
  if (parts.length < 3) return null;
  final day = int.tryParse(parts[0]);
  final month = _trMonths.indexOf(parts[1]);
  final year = int.tryParse(parts[2]);
  if (day == null || month < 1 || year == null) return null;
  return DateTime(year, month, day);
}

(TimeOfDay?, TimeOfDay?) _parseTimeRange(String label) {
  final parts = label.split('-');
  if (parts.length != 2) return (null, null);
  return (_parseHm(parts[0]), _parseHm(parts[1]));
}

TimeOfDay? _parseHm(String value) {
  final hm = value.trim().split(':');
  if (hm.length != 2) return null;
  final h = int.tryParse(hm[0]);
  final m = int.tryParse(hm[1]);
  if (h == null || m == null) return null;
  return TimeOfDay(hour: h, minute: m);
}

String _hm(TimeOfDay t) =>
    '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

bool _isAfter(TimeOfDay a, TimeOfDay b) =>
    a.hour * 60 + a.minute > b.hour * 60 + b.minute;

class _EditFieldLabel extends StatelessWidget {
  const _EditFieldLabel({required this.text});

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

class _PickerBox extends StatelessWidget {
  const _PickerBox({
    required this.value,
    required this.icon,
    required this.onTap,
  });

  final String value;
  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(color: AppColors.border),
        ),
        child: Row(
          children: <Widget>[
            Expanded(
              child: Text(
                value,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ),
            const SizedBox(width: 8),
            Icon(icon, size: 20, color: AppColors.textSecondary),
          ],
        ),
      ),
    );
  }
}

class _SheetField extends StatelessWidget {
  const _SheetField({
    required this.controller,
    required this.label,
    required this.hint,
    this.maxLines = 1,
  });

  final TextEditingController controller;
  final String label;
  final String hint;
  final int maxLines;

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      maxLines: maxLines,
      validator: (value) {
        if (label == 'Ders notu') {
          return null;
        }
        if (value == null || value.trim().isEmpty) {
          return 'Bu alan zorunlu.';
        }
        return null;
      },
      decoration: InputDecoration(
        labelText: label,
        hintText: hint,
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
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: AppColors.primary),
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: AppColors.border),
        ),
      ),
    );
  }
}

class _NoteFileItem {
  const _NoteFileItem({
    required this.title,
    required this.meta,
    required this.accent,
    required this.noteText,
    required this.sourceFilePath,
  });

  final String title;
  final String meta;
  final Color accent;
  final String noteText;
  final String? sourceFilePath;
}

class _DetailTabs extends StatelessWidget {
  const _DetailTabs({
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
          final selected = index == selectedIndex;
          return Expanded(
            child: GestureDetector(
              onTap: () => onChanged(index),
              behavior: HitTestBehavior.opaque,
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                curve: Curves.easeOut,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: selected ? AppColors.primary : Colors.transparent,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  tabs[index],
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
                    color: selected ? Colors.white : AppColors.textSecondary,
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

class _InfoFileCard extends StatelessWidget {
  const _InfoFileCard({
    required this.icon,
    required this.iconColor,
    required this.title,
    required this.subtitle,
    required this.trailingLabel,
    this.onTap,
  });

  final IconData icon;
  final Color iconColor;
  final String title;
  final String subtitle;
  final String trailingLabel;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return _BaseDetailCard(
      leading: _LeadingIcon(icon: icon, color: iconColor),
      title: title,
      subtitle: subtitle,
      trailing: Align(
        alignment: Alignment.centerRight,
        child: OutlinedButton(
          style: OutlinedButton.styleFrom(
            foregroundColor: AppColors.primary,
            side: BorderSide(color: AppColors.primary.withValues(alpha: 0.18)),
            backgroundColor: AppColors.primary.withValues(alpha: 0.04),
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
            minimumSize: const Size(0, 32),
            tapTargetSize: MaterialTapTargetSize.shrinkWrap,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
            textStyle: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w700,
            ),
          ),
          onPressed: onTap,
          child: Text(trailingLabel),
        ),
      ),
    );
  }
}

class _HomeworkCard extends StatelessWidget {
  const _HomeworkCard({
    required this.title,
    required this.subtitle,
    required this.statusLabel,
    required this.actionLabel,
    this.statusColor = AppColors.accentGreen,
  });

  final String title;
  final String subtitle;
  final String statusLabel;
  final String actionLabel;
  final Color statusColor;

  @override
  Widget build(BuildContext context) {
    return _BaseDetailCard(
      leading: const _LeadingIcon(
        icon: Icons.assignment_rounded,
        color: AppColors.accentBlue,
      ),
      title: title,
      subtitle: subtitle,
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: <Widget>[
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(999),
            ),
            child: Text(
              statusLabel,
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: statusColor,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          const SizedBox(height: 8),
          OutlinedButton(
            style: OutlinedButton.styleFrom(
              foregroundColor: AppColors.primary,
              side: BorderSide(
                color: AppColors.primary.withValues(alpha: 0.18),
              ),
              backgroundColor: AppColors.primary.withValues(alpha: 0.04),
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              minimumSize: const Size(0, 32),
              tapTargetSize: MaterialTapTargetSize.shrinkWrap,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12),
              ),
              textStyle: const TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
              ),
            ),
            onPressed: () {},
            child: Text(actionLabel),
          ),
        ],
      ),
    );
  }
}

class _PaymentCard extends StatelessWidget {
  const _PaymentCard({
    required this.title,
    required this.subtitle,
    required this.amountLabel,
    required this.statusLabel,
    this.statusColor = AppColors.accentGreen,
  });

  final String title;
  final String subtitle;
  final String amountLabel;
  final String statusLabel;
  final Color statusColor;

  @override
  Widget build(BuildContext context) {
    return _BaseDetailCard(
      leading: const _LeadingIcon(
        icon: Icons.payments_rounded,
        color: AppColors.amber,
      ),
      title: title,
      subtitle: subtitle,
      trailing: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: <Widget>[
          Text(
            amountLabel,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
            decoration: BoxDecoration(
              color: statusColor.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(999),
            ),
            child: Text(
              statusLabel,
              style: Theme.of(context).textTheme.labelSmall?.copyWith(
                color: statusColor,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _BaseDetailCard extends StatelessWidget {
  const _BaseDetailCard({
    required this.leading,
    required this.title,
    required this.subtitle,
    required this.trailing,
  });

  final Widget leading;
  final String title;
  final String subtitle;
  final Widget trailing;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.soft,
      ),
      child: Row(
        children: <Widget>[
          leading,
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  subtitle,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          SizedBox(width: 112, child: trailing),
        ],
      ),
    );
  }
}

class _LeadingIcon extends StatelessWidget {
  const _LeadingIcon({required this.icon, required this.color});

  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 46,
      height: 46,
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Icon(icon, color: color),
    );
  }
}

class _MetaChip extends StatelessWidget {
  const _MetaChip({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: AppColors.tabBackground,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, size: 14, color: AppColors.primary),
          const SizedBox(width: 6),
          Text(
            label,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: AppColors.primary,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

class _Avatar extends StatelessWidget {
  const _Avatar({required this.name, required this.accent});

  final String name;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    final parts = name.trim().split(RegExp(r'\s+'));
    final initials = parts.length > 1
        ? '${parts.first[0]}${parts.last[0]}'.toUpperCase()
        : name.substring(0, 1).toUpperCase();
    return Container(
      width: 64,
      height: 64,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            accent.withValues(alpha: 0.9),
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
        style: Theme.of(context).textTheme.titleMedium?.copyWith(
          color: Colors.white,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}
