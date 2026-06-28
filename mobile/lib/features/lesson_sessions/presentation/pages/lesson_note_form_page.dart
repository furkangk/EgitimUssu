import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class LessonNoteFormPage extends StatefulWidget {
  const LessonNoteFormPage({super.key, this.initialContext});

  final LessonNoteFormContext? initialContext;

  @override
  State<LessonNoteFormPage> createState() => _LessonNoteFormPageState();
}

class LessonNoteFormContext {
  const LessonNoteFormContext({
    required this.studentName,
    required this.lessonName,
    this.lockSelection = false,
  });

  final String studentName;
  final String lessonName;
  final bool lockSelection;
}

class _LessonNoteFormPageState extends State<LessonNoteFormPage> {
  final _formKey = GlobalKey<FormState>();
  final _titleController = TextEditingController();
  final _noteController = TextEditingController();

  PlatformFile? _selectedFile;
  late String _selectedStudent;
  late String _selectedLesson;

  static const _studentOptions = <String>[
    'Zeynep Demir',
    'Ali Yilmaz',
    'Merve Kaya',
    'Ece Aydin',
  ];

  static const _lessonsByStudent = <String, List<String>>{
    'Zeynep Demir': <String>['Matematik', 'Geometri'],
    'Ali Yilmaz': <String>['Fizik', 'Matematik'],
    'Merve Kaya': <String>['Geometri', 'Kimya'],
    'Ece Aydin': <String>['Matematik', 'Turkce'],
  };

  bool get _isSelectionLocked => widget.initialContext?.lockSelection ?? false;

  @override
  void initState() {
    super.initState();
    _selectedStudent =
        widget.initialContext?.studentName ?? _studentOptions.first;
    _selectedLesson =
        widget.initialContext?.lessonName ??
        _lessonsForStudent(_selectedStudent).first;
    _titleController.text = 'Polinomlar ders notu';
    _noteController.text =
        'Polinomlarda derece, katsayi ve temel islemler tekrar edildi. Bir sonraki derste yeni nesil soru cozumu yapilacak.';
  }

  @override
  void dispose() {
    _titleController.dispose();
    _noteController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Column(
          children: <Widget>[
            _TopBar(
              onBack: () {
                if (context.canPop()) {
                  context.pop();
                } else {
                  context.go('/dashboard');
                }
              },
            ),
            Expanded(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 112),
                children: <Widget>[
                  Form(
                    key: _formKey,
                    child: Column(
                      children: <Widget>[
                        if (!_isSelectionLocked) ...<Widget>[
                          _SelectionFields(
                            selectedStudent: _selectedStudent,
                            selectedLesson: _selectedLesson,
                            studentOptions: _studentOptions,
                            lessonOptions: _lessonsForStudent(_selectedStudent),
                            onStudentChanged: (value) {
                              if (value == null) {
                                return;
                              }
                              setState(() {
                                _selectedStudent = value;
                                _selectedLesson = _lessonsForStudent(
                                  value,
                                ).first;
                              });
                            },
                            onLessonChanged: (value) {
                              if (value == null) {
                                return;
                              }
                              setState(() => _selectedLesson = value);
                            },
                          ),
                          const SizedBox(height: 14),
                        ],
                        AppTextField(
                          controller: _titleController,
                          labelText: 'Baslik',
                          hintText: 'Ders notu basligi',
                          textCapitalization: TextCapitalization.sentences,
                          validator: _required,
                        ),
                        const SizedBox(height: 14),
                        AppTextField(
                          controller: _noteController,
                          labelText: 'Ders notu',
                          hintText: 'Ders notunu yaz',
                          minLines: 6,
                          maxLines: 10,
                          textCapitalization: TextCapitalization.sentences,
                          validator: _required,
                        ),
                        const SizedBox(height: 14),
                        _FilePickerCard(
                          selectedFile: _selectedFile,
                          onPick: _pickFile,
                          onClear: () => setState(() => _selectedFile = null),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
      bottomNavigationBar: Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          border: Border(top: BorderSide(color: AppColors.divider)),
        ),
        padding: EdgeInsets.fromLTRB(
          16,
          12,
          16,
          MediaQuery.of(context).padding.bottom + 12,
        ),
        child: AppPrimaryButton(label: 'Ders Notunu Kaydet', onPressed: _save),
      ),
    );
  }

  Future<void> _pickFile() async {
    final result = await FilePicker.platform.pickFiles(
      allowMultiple: false,
      type: FileType.custom,
      allowedExtensions: const <String>['pdf', 'doc', 'docx', 'jpg', 'png'],
    );
    if (result == null || result.files.isEmpty) {
      return;
    }
    setState(() => _selectedFile = result.files.first);
  }

  void _save() {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(const SnackBar(content: Text('Ders notu kaydedildi.')));
  }

  String? _required(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Bu alan zorunlu.';
    }
    return null;
  }

  List<String> _lessonsForStudent(String student) {
    return _lessonsByStudent[student] ?? const <String>['Matematik'];
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({required this.onBack});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 4, 16, 4),
      child: Row(
        children: <Widget>[
          IconButton(
            onPressed: onBack,
            icon: const Icon(Icons.arrow_back_rounded),
            color: AppColors.textPrimary,
          ),
          Expanded(
            child: Text(
              'Ders Notu Ekle',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _SelectionFields extends StatelessWidget {
  const _SelectionFields({
    required this.selectedStudent,
    required this.selectedLesson,
    required this.studentOptions,
    required this.lessonOptions,
    required this.onStudentChanged,
    required this.onLessonChanged,
  });

  final String selectedStudent;
  final String selectedLesson;
  final List<String> studentOptions;
  final List<String> lessonOptions;
  final ValueChanged<String?> onStudentChanged;
  final ValueChanged<String?> onLessonChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const AppFieldLabel(text: 'Ogrenci'),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          initialValue: selectedStudent,
          decoration: appInputDecoration('Ogrenci sec'),
          items: studentOptions
              .map(
                (student) => DropdownMenuItem<String>(
                  value: student,
                  child: Text(student),
                ),
              )
              .toList(),
          onChanged: onStudentChanged,
        ),
        const SizedBox(height: 14),
        const AppFieldLabel(text: 'Ders'),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          key: ValueKey<String>('lesson-$selectedStudent'),
          initialValue: selectedLesson,
          decoration: appInputDecoration('Ders sec'),
          items: lessonOptions
              .map(
                (lesson) => DropdownMenuItem<String>(
                  value: lesson,
                  child: Text(lesson),
                ),
              )
              .toList(),
          onChanged: onLessonChanged,
        ),
      ],
    );
  }
}

class _FilePickerCard extends StatelessWidget {
  const _FilePickerCard({
    required this.selectedFile,
    required this.onPick,
    required this.onClear,
  });

  final PlatformFile? selectedFile;
  final VoidCallback onPick;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    final file = selectedFile;
    return InkWell(
      borderRadius: BorderRadius.circular(20),
      onTap: onPick,
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: AppColors.border),
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color:
                    (file == null
                            ? AppColors.accentBlue
                            : AppColors.accentGreen)
                        .withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Icon(
                file == null ? Icons.attach_file_rounded : Icons.check_rounded,
                color: file == null
                    ? AppColors.accentBlue
                    : AppColors.accentGreen,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    file?.name ?? 'Dosya ekle',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    file == null
                        ? 'PDF, Word veya gorsel dosya sec'
                        : '${(file.size / 1024).ceil()} KB',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            if (file != null)
              IconButton(
                onPressed: onClear,
                icon: const Icon(Icons.close_rounded),
                color: AppColors.textSecondary,
              )
            else
              const Icon(
                Icons.chevron_right_rounded,
                color: AppColors.textSecondary,
              ),
          ],
        ),
      ),
    );
  }
}
