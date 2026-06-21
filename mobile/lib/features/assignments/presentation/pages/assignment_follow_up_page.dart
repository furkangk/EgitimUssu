import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/assignment_follow_up_cubit.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/assignment_follow_up_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class AssignmentFollowUpPage extends StatefulWidget {
  const AssignmentFollowUpPage({
    super.key,
    required this.lessonSessionId,
    this.initialContext,
  });

  final String lessonSessionId;
  final AssignmentFormContext? initialContext;

  @override
  State<AssignmentFollowUpPage> createState() => _AssignmentFollowUpPageState();
}

class AssignmentFormContext {
  const AssignmentFormContext({
    required this.studentName,
    required this.lessonName,
    this.lockSelection = false,
  });

  final String studentName;
  final String lessonName;
  final bool lockSelection;
}

class _AssignmentFollowUpPageState extends State<AssignmentFollowUpPage> {
  static const _text = Color(0xFF10233D);
  static const _slate = Color(0xFF6B7A90);
  static const _background = Color(0xFFF4F8FC);
  static const _border = Color(0xFFD7E7F8);
  static const _divider = Color(0xFFE5EEF7);
  static const _blue = Color(0xFF3D8BFF);
  static const _emerald = Color(0xFF20B486);

  final _formKey = GlobalKey<FormState>();
  final _titleController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _dueDateController = TextEditingController();

  DateTime? _selectedDueDate;
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
    _titleController.text = 'Polinomlar tekrar testi';
    _descriptionController.text =
        'Kaynak kitaptan 42-45. sayfalardaki 24 soruyu coz. Yanlis yaptigin sorulari isaretle.';
    _selectedDueDate = DateTime.now().add(const Duration(days: 3));
    _dueDateController.text = _formatDate(_selectedDueDate!);
  }

  @override
  void dispose() {
    _titleController.dispose();
    _descriptionController.dispose();
    _dueDateController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return BlocProvider<AssignmentFollowUpCubit>(
      create: (_) => AssignmentFollowUpCubit.create(),
      child: Scaffold(
        backgroundColor: _background,
        body: SafeArea(
          child: BlocConsumer<AssignmentFollowUpCubit, AssignmentFollowUpState>(
            listener: (context, state) {
              if (state.successMessage != null) {
                ScaffoldMessenger.of(
                  context,
                ).showSnackBar(SnackBar(content: Text(state.successMessage!)));
              }
              if (state.errorMessage != null) {
                ScaffoldMessenger.of(
                  context,
                ).showSnackBar(SnackBar(content: Text(state.errorMessage!)));
              }
            },
            builder: (context, state) {
              return Column(
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
                                  lessonOptions: _lessonsForStudent(
                                    _selectedStudent,
                                  ),
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
                                hintText: 'Odev basligi',
                                textCapitalization:
                                    TextCapitalization.sentences,
                                validator: _required,
                              ),
                              const SizedBox(height: 14),
                              AppTextField(
                                controller: _descriptionController,
                                labelText: 'Aciklama',
                                hintText: 'Odev detaylarini yaz',
                                minLines: 5,
                                maxLines: 8,
                                textCapitalization:
                                    TextCapitalization.sentences,
                                validator: _required,
                              ),
                              const SizedBox(height: 14),
                              AppTextField(
                                controller: _dueDateController,
                                labelText: 'Son teslim tarihi',
                                hintText: 'Tarih sec',
                                readOnly: true,
                                onTap: _pickDueDate,
                                suffixIcon: const Icon(Icons.event_rounded),
                                validator: _required,
                              ),
                              const SizedBox(height: 14),
                              _FilePickerCard(
                                selectedFile: _selectedFile,
                                onPick: _pickFile,
                                onClear: () =>
                                    setState(() => _selectedFile = null),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              );
            },
          ),
        ),
        bottomNavigationBar:
            BlocBuilder<AssignmentFollowUpCubit, AssignmentFollowUpState>(
              builder: (context, state) {
                return Container(
                  decoration: const BoxDecoration(
                    color: Colors.white,
                    border: Border(top: BorderSide(color: _divider)),
                  ),
                  padding: EdgeInsets.fromLTRB(
                    16,
                    12,
                    16,
                    MediaQuery.of(context).padding.bottom + 12,
                  ),
                  child: AppPrimaryButton(
                    label: 'Odevi Gonder',
                    isLoading: state.isSaving,
                    onPressed: () => _save(context),
                  ),
                );
              },
            ),
      ),
    );
  }

  Future<void> _pickDueDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _selectedDueDate ?? now.add(const Duration(days: 1)),
      firstDate: DateTime(now.year, now.month, now.day),
      lastDate: DateTime(now.year + 2),
    );
    if (picked == null) {
      return;
    }
    setState(() {
      _selectedDueDate = picked;
      _dueDateController.text = _formatDate(picked);
    });
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
    final file = result.files.first;
    setState(() => _selectedFile = file);
  }

  void _save(BuildContext context) {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    context.read<AssignmentFollowUpCubit>().save(
      FollowUpAssignment(
        lessonSessionId: widget.lessonSessionId.isEmpty
            ? 'manual-$_selectedStudent-$_selectedLesson'
            : widget.lessonSessionId,
        summary: '',
        coveredTopics: '',
        recommendations: '',
        assignments: <AssignmentItem>[
          AssignmentItem(
            title: _titleController.text.trim(),
            description: _descriptionController.text.trim(),
            dueDateUtc: _selectedDueDate?.toUtc(),
            attachmentUrl: _selectedFile?.path ?? _selectedFile?.name,
            status: 'Pending',
          ),
        ],
      ),
    );
  }

  String? _required(String? value) {
    if (value == null || value.trim().isEmpty) {
      return 'Bu alan zorunlu.';
    }
    return null;
  }

  String _formatDate(DateTime date) {
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
    return '${date.day} ${months[date.month - 1]} ${date.year}';
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
            color: _AssignmentFollowUpPageState._text,
          ),
          Expanded(
            child: Text(
              'Odev Ver',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: _AssignmentFollowUpPageState._text,
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
          border: Border.all(color: _AssignmentFollowUpPageState._border),
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color:
                    (file == null
                            ? _AssignmentFollowUpPageState._blue
                            : _AssignmentFollowUpPageState._emerald)
                        .withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Icon(
                file == null ? Icons.attach_file_rounded : Icons.check_rounded,
                color: file == null
                    ? _AssignmentFollowUpPageState._blue
                    : _AssignmentFollowUpPageState._emerald,
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
                      color: _AssignmentFollowUpPageState._text,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    file == null
                        ? 'PDF, Word veya gorsel dosya sec'
                        : '${(file.size / 1024).ceil()} KB',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _AssignmentFollowUpPageState._slate,
                    ),
                  ),
                ],
              ),
            ),
            if (file != null)
              IconButton(
                onPressed: onClear,
                icon: const Icon(Icons.close_rounded),
                color: _AssignmentFollowUpPageState._slate,
              )
            else
              const Icon(
                Icons.chevron_right_rounded,
                color: _AssignmentFollowUpPageState._slate,
              ),
          ],
        ),
      ),
    );
  }
}
