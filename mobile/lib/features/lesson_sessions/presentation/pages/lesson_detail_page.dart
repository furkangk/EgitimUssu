import 'package:egitim_ussu_mobile/features/assignments/presentation/pages/assignment_follow_up_page.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/presentation/pages/lesson_note_view_page.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class LessonDetailPayload {
  const LessonDetailPayload({
    required this.studentName,
    required this.subject,
    required this.dateLabel,
    required this.timeLabel,
    required this.modeLabel,
    required this.accent,
  });

  final String studentName;
  final String subject;
  final String dateLabel;
  final String timeLabel;
  final String modeLabel;
  final Color accent;
}

class LessonDetailPage extends StatefulWidget {
  const LessonDetailPage({super.key, this.payload});

  final LessonDetailPayload? payload;

  @override
  State<LessonDetailPage> createState() => _LessonDetailPageState();
}

class _LessonDetailPageState extends State<LessonDetailPage> {
  static const _navy = Color(0xFF062B52);
  static const _text = Color(0xFF10233D);
  static const _slate = Color(0xFF7A8494);
  static const _background = Color(0xFFF4F8FC);
  static const _border = Color(0xFFE5EAF0);
  static const _surface = Colors.white;
  static const _emerald = Color(0xFF20B486);
  static const _amber = Color(0xFFFFB84D);
  static const _blue = Color(0xFF3D8BFF);

  int _selectedTab = 0;
  late final List<_NoteFileItem> _lessonNotes = <_NoteFileItem>[
    const _NoteFileItem(
      title: 'Polinomlar Konu Anlatim.pdf',
      meta: '1.2 MB  20 Mayis',
      accent: _blue,
      noteText:
          'Polinomlarda temel kavramlar, toplama-cikarma ve carpma islemleri anlatildi. Ders sonunda yeni nesil soru ornekleri cozuldu.',
      sourceFilePath: null,
    ),
    const _NoteFileItem(
      title: 'Fonksiyonlar Ozet Notu.pdf',
      meta: '860 KB  18 Mayis',
      accent: _emerald,
      noteText:
          'Fonksiyon cesitleri, tanim kumesi ve deger kumesi uzerinden kisa bir tekrar yapildi. Eksik kalan noktalar ayrica not edildi.',
      sourceFilePath: null,
    ),
  ];

  static const _tabs = <String>['Ders notu', 'Odevler', 'Odeme'];

  LessonDetailPayload? _editedPayload;

  LessonDetailPayload get _payload =>
      _editedPayload ??
      widget.payload ??
      const LessonDetailPayload(
        studentName: 'Zeynep Demir',
        subject: 'Matematik',
        dateLabel: '20 Mayis 2025 Sali',
        timeLabel: '10:00 - 11:00',
        modeLabel: 'Online',
        accent: _blue,
      );

  @override
  Widget build(BuildContext context) {
    final payload = _payload;

    return Scaffold(
      backgroundColor: _background,
      appBar: AppBar(
        backgroundColor: _background,
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
              statusColor: _amber,
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
              statusColor: _amber,
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
                              color: _border,
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
                                      color: _text,
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
                          style: Theme.of(
                            context,
                          ).textTheme.bodyMedium?.copyWith(color: _slate),
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
                                color: _text,
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
                              border: Border.all(color: _border),
                            ),
                            child: Row(
                              children: <Widget>[
                                Container(
                                  width: 42,
                                  height: 42,
                                  decoration: BoxDecoration(
                                    color: _navy.withValues(alpha: 0.08),
                                    borderRadius: BorderRadius.circular(14),
                                  ),
                                  child: const Icon(
                                    Icons.attach_file_rounded,
                                    color: _navy,
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
                                                  ? _slate
                                                  : _text,
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
                                            ?.copyWith(color: _slate),
                                      ),
                                    ],
                                  ),
                                ),
                                const Icon(
                                  Icons.chevron_right_rounded,
                                  color: _slate,
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
                              backgroundColor: _navy,
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
                                    accent: _blue,
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
    final payload = _payload;
    final formKey = GlobalKey<FormState>();
    final subjectController = TextEditingController(text: payload.subject);
    final dateController = TextEditingController(text: payload.dateLabel);
    final timeController = TextEditingController(text: payload.timeLabel);
    var modeLabel = payload.modeLabel;

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
                              color: _border,
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
                                      color: _text,
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
                          style: Theme.of(
                            context,
                          ).textTheme.bodyMedium?.copyWith(color: _slate),
                        ),
                        const SizedBox(height: 18),
                        _SheetField(
                          controller: subjectController,
                          label: 'Ders adi',
                          hint: 'Matematik',
                        ),
                        const SizedBox(height: 12),
                        _SheetField(
                          controller: dateController,
                          label: 'Tarih',
                          hint: '20 Mayis 2025 Sali',
                        ),
                        const SizedBox(height: 12),
                        _SheetField(
                          controller: timeController,
                          label: 'Saat araligi',
                          hint: '10:00 - 11:00',
                        ),
                        const SizedBox(height: 12),
                        DropdownButtonFormField<String>(
                          initialValue: modeLabel,
                          decoration: InputDecoration(
                            labelText: 'Ders sekli',
                            filled: true,
                            fillColor: Colors.white,
                            contentPadding: const EdgeInsets.symmetric(
                              horizontal: 16,
                              vertical: 16,
                            ),
                            enabledBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(18),
                              borderSide: const BorderSide(color: _border),
                            ),
                            focusedBorder: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(18),
                              borderSide: const BorderSide(color: _navy),
                            ),
                            border: OutlineInputBorder(
                              borderRadius: BorderRadius.circular(18),
                              borderSide: const BorderSide(color: _border),
                            ),
                          ),
                          items: const <DropdownMenuItem<String>>[
                            DropdownMenuItem<String>(
                              value: 'Online',
                              child: Text('Online'),
                            ),
                            DropdownMenuItem<String>(
                              value: 'Yuz yuze',
                              child: Text('Yuz yuze'),
                            ),
                          ],
                          onChanged: (value) {
                            if (value == null) {
                              return;
                            }
                            setModalState(() => modeLabel = value);
                          },
                        ),
                        const SizedBox(height: 18),
                        SizedBox(
                          width: double.infinity,
                          child: FilledButton(
                            style: FilledButton.styleFrom(
                              backgroundColor: _navy,
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
                              Navigator.of(context).pop(
                                LessonDetailPayload(
                                  studentName: payload.studentName,
                                  subject: subjectController.text.trim(),
                                  dateLabel: dateController.text.trim(),
                                  timeLabel: timeController.text.trim(),
                                  modeLabel: modeLabel,
                                  accent: payload.accent,
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
    dateController.dispose();
    timeController.dispose();

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

class _HeroCard extends StatelessWidget {
  const _HeroCard({required this.payload});

  final LessonDetailPayload payload;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: _LessonDetailPageState._surface,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: _LessonDetailPageState._border),
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
          _Avatar(name: payload.studentName, accent: payload.accent),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  payload.studentName,
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: _LessonDetailPageState._text,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  payload.subject,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: _LessonDetailPageState._navy,
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
            color: _LessonDetailPageState._surface,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: _LessonDetailPageState._border),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Icon(icon, color: _LessonDetailPageState._navy, size: 20),
              const SizedBox(height: 8),
              Text(
                label,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: _LessonDetailPageState._text,
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
          borderSide: const BorderSide(color: _LessonDetailPageState._border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: _LessonDetailPageState._navy),
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(18),
          borderSide: const BorderSide(color: _LessonDetailPageState._border),
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
        color: const Color(0xFFF3F5F8),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: _LessonDetailPageState._border),
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
                  color: selected
                      ? _LessonDetailPageState._navy
                      : Colors.transparent,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  tabs[index],
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: selected ? FontWeight.w600 : FontWeight.w500,
                    color: selected
                        ? Colors.white
                        : _LessonDetailPageState._slate,
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
            foregroundColor: _LessonDetailPageState._navy,
            side: BorderSide(
              color: _LessonDetailPageState._navy.withValues(alpha: 0.18),
            ),
            backgroundColor: _LessonDetailPageState._navy.withValues(
              alpha: 0.04,
            ),
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
    this.statusColor = _LessonDetailPageState._emerald,
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
        color: _LessonDetailPageState._blue,
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
              foregroundColor: _LessonDetailPageState._navy,
              side: BorderSide(
                color: _LessonDetailPageState._navy.withValues(alpha: 0.18),
              ),
              backgroundColor: _LessonDetailPageState._navy.withValues(
                alpha: 0.04,
              ),
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
    this.statusColor = _LessonDetailPageState._emerald,
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
        color: _LessonDetailPageState._amber,
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
              color: _LessonDetailPageState._text,
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
        color: _LessonDetailPageState._surface,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _LessonDetailPageState._border),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x12082B4F),
            blurRadius: 24,
            offset: Offset(0, 10),
          ),
        ],
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
                    color: _LessonDetailPageState._text,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  subtitle,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: _LessonDetailPageState._slate,
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
        color: const Color(0xFFF3F5F8),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icon, size: 14, color: _LessonDetailPageState._navy),
          const SizedBox(width: 6),
          Text(
            label,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: _LessonDetailPageState._navy,
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
