import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/assignment_follow_up_cubit.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/assignment_follow_up_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

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

class _AssignmentEntry {
  _AssignmentEntry()
    : titleController = TextEditingController(),
      descriptionController = TextEditingController(),
      dueDateController = TextEditingController();

  final TextEditingController titleController;
  final TextEditingController descriptionController;
  final TextEditingController dueDateController;
  DateTime? selectedDueDate;
  PlatformFile? selectedFile;

  void dispose() {
    titleController.dispose();
    descriptionController.dispose();
    dueDateController.dispose();
  }
}

class _AssignmentFollowUpPageState extends State<AssignmentFollowUpPage> {
  final _formKey = GlobalKey<FormState>();
  final List<_AssignmentEntry> _assignments = [];

  @override
  void initState() {
    super.initState();
    _assignments.add(_AssignmentEntry());
  }

  @override
  void dispose() {
    for (final entry in _assignments) {
      entry.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return BlocProvider<AssignmentFollowUpCubit>(
      create: (_) => AssignmentFollowUpCubit.create(),
      child: Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          child: BlocConsumer<AssignmentFollowUpCubit, AssignmentFollowUpState>(
            listener: (context, state) {
              if (state.successMessage != null) {
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(
                    content: const Text('Ders takibi kaydedildi.'),
                    backgroundColor: AppColors.accentGreen,
                    behavior: SnackBarBehavior.floating,
                  ),
                );
                if (context.canPop()) context.pop();
              }
              if (state.errorMessage != null) {
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(
                    content: Text(state.errorMessage!),
                    backgroundColor: AppColors.accentRed,
                    behavior: SnackBarBehavior.floating,
                  ),
                );
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
                    child: Form(
                      key: _formKey,
                      child: ListView(
                        padding: const EdgeInsets.fromLTRB(16, 8, 16, 120),
                        children: <Widget>[
                          if (widget.initialContext != null)
                            _ContextCard(ctx: widget.initialContext!),
                          const SizedBox(height: 16),
                          _SectionHeader(
                            icon: Icons.assignment_rounded,
                            title: 'Ödevler',
                            trailing: Text(
                              '${_assignments.length} ödev',
                              style: Theme.of(context).textTheme.labelMedium
                                  ?.copyWith(
                                    color: AppColors.textSecondary,
                                    fontWeight: FontWeight.w700,
                                  ),
                            ),
                          ),
                          const SizedBox(height: 12),
                          ...List<Widget>.generate(_assignments.length, (i) {
                            return Padding(
                              padding: const EdgeInsets.only(bottom: 14),
                              child: _AssignmentEntryCard(
                                index: i,
                                entry: _assignments[i],
                                canRemove: _assignments.length > 1,
                                onRemove: () => _removeAssignment(i),
                                onPickDate: () => _pickDueDate(i),
                                onPickFile: () => _pickFile(i),
                                onClearFile: () => setState(
                                  () => _assignments[i].selectedFile = null,
                                ),
                              ),
                            );
                          }),
                          _AddAssignmentButton(onTap: _addAssignment),
                        ],
                      ),
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
                    border: Border(top: BorderSide(color: AppColors.divider)),
                  ),
                  padding: EdgeInsets.fromLTRB(
                    16,
                    12,
                    16,
                    MediaQuery.of(context).padding.bottom + 12,
                  ),
                  child: AppPrimaryButton(
                    label: 'Takibi Kaydet',
                    isLoading: state.isSaving,
                    onPressed: () => _save(context),
                  ),
                );
              },
            ),
      ),
    );
  }

  void _addAssignment() {
    setState(() => _assignments.add(_AssignmentEntry()));
  }

  void _removeAssignment(int index) {
    setState(() {
      _assignments[index].dispose();
      _assignments.removeAt(index);
    });
  }

  Future<void> _pickDueDate(int index) async {
    final now = DateTime.now();
    final entry = _assignments[index];
    final picked = await showDatePicker(
      context: context,
      initialDate: entry.selectedDueDate ?? now.add(const Duration(days: 1)),
      firstDate: DateTime(now.year, now.month, now.day),
      lastDate: DateTime(now.year + 2),
    );
    if (picked == null) return;
    setState(() {
      entry.selectedDueDate = picked;
      entry.dueDateController.text = _formatDate(picked);
    });
  }

  Future<void> _pickFile(int index) async {
    final result = await FilePicker.platform.pickFiles(
      allowMultiple: false,
      type: FileType.custom,
      allowedExtensions: const <String>['pdf', 'doc', 'docx', 'jpg', 'png'],
    );
    if (result == null || result.files.isEmpty) return;
    setState(() => _assignments[index].selectedFile = result.files.first);
  }

  void _save(BuildContext context) {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final hasValidAssignment = _assignments.any(
      (e) => e.titleController.text.trim().isNotEmpty,
    );
    if (!hasValidAssignment) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: const Text('En az bir ödev başlığı giriniz.'),
          backgroundColor: AppColors.accentRed,
          behavior: SnackBarBehavior.floating,
        ),
      );
      return;
    }

    final assignmentItems = _assignments
        .where((e) => e.titleController.text.trim().isNotEmpty)
        .map(
          (e) => AssignmentItem(
            title: e.titleController.text.trim(),
            description: e.descriptionController.text.trim(),
            dueDateUtc: e.selectedDueDate?.toUtc(),
            attachmentUrl: e.selectedFile?.path ?? e.selectedFile?.name,
          ),
        )
        .toList();

    context.read<AssignmentFollowUpCubit>().save(
      FollowUpAssignment(
        lessonSessionId: widget.lessonSessionId,
        summary: '',
        coveredTopics: '',
        recommendations: '',
        assignments: assignmentItems,
      ),
    );
  }

  String _formatDate(DateTime date) {
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
    return '${date.day} ${months[date.month - 1]} ${date.year}';
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
              'Ödev Ver',
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

class _ContextCard extends StatelessWidget {
  const _ContextCard({required this.ctx});

  final AssignmentFormContext ctx;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.primary.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: <Widget>[
          Container(
            width: 44,
            height: 44,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(14),
            ),
            child: const Icon(Icons.person_rounded, color: AppColors.primary),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  ctx.studentName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  ctx.lessonName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({
    required this.icon,
    required this.title,
    this.trailing,
  });

  final IconData icon;
  final String title;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Icon(icon, size: 20, color: AppColors.primary),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            title,
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        if (trailing != null) trailing!,
      ],
    );
  }
}

class _AssignmentEntryCard extends StatelessWidget {
  const _AssignmentEntryCard({
    required this.index,
    required this.entry,
    required this.canRemove,
    required this.onRemove,
    required this.onPickDate,
    required this.onPickFile,
    required this.onClearFile,
  });

  final int index;
  final _AssignmentEntry entry;
  final bool canRemove;
  final VoidCallback onRemove;
  final VoidCallback onPickDate;
  final VoidCallback onPickFile;
  final VoidCallback onClearFile;

  @override
  Widget build(BuildContext context) {
    final file = entry.selectedFile;
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 28,
                height: 28,
                decoration: BoxDecoration(
                  color: AppColors.accentBlue.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(8),
                ),
                alignment: Alignment.center,
                child: Text(
                  '${index + 1}',
                  style: Theme.of(context).textTheme.labelLarge?.copyWith(
                    color: AppColors.accentBlue,
                    fontWeight: FontWeight.w900,
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  'Ödev ${index + 1}',
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              if (canRemove)
                IconButton(
                  onPressed: onRemove,
                  icon: const Icon(Icons.delete_outline_rounded),
                  color: AppColors.accentRed,
                  iconSize: 20,
                  padding: EdgeInsets.zero,
                  constraints: const BoxConstraints(
                    minWidth: 36,
                    minHeight: 36,
                  ),
                ),
            ],
          ),
          const SizedBox(height: 14),
          AppTextField(
            controller: entry.titleController,
            labelText: 'Başlık',
            hintText: 'Ödev başlığı',
            textCapitalization: TextCapitalization.sentences,
            validator: index == 0
                ? (v) => (v == null || v.trim().isEmpty)
                      ? 'İlk ödev başlığı zorunlu.'
                      : null
                : null,
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: entry.descriptionController,
            labelText: 'Açıklama',
            hintText: 'Ödev detayları (isteğe bağlı)',
            minLines: 2,
            maxLines: 4,
            textCapitalization: TextCapitalization.sentences,
          ),
          const SizedBox(height: 12),
          AppTextField(
            controller: entry.dueDateController,
            labelText: 'Son Teslim Tarihi',
            hintText: 'Tarih seç (isteğe bağlı)',
            readOnly: true,
            onTap: onPickDate,
            suffixIcon: const Icon(Icons.event_rounded),
          ),
          const SizedBox(height: 12),
          _FilePicker(file: file, onPick: onPickFile, onClear: onClearFile),
        ],
      ),
    );
  }
}

class _FilePicker extends StatelessWidget {
  const _FilePicker({
    required this.file,
    required this.onPick,
    required this.onClear,
  });

  final PlatformFile? file;
  final VoidCallback onPick;
  final VoidCallback onClear;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onPick,
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: AppColors.background,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: AppColors.border),
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color:
                    (file == null
                            ? AppColors.accentBlue
                            : AppColors.accentGreen)
                        .withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Icon(
                file == null ? Icons.attach_file_rounded : Icons.check_rounded,
                size: 20,
                color: file == null
                    ? AppColors.accentBlue
                    : AppColors.accentGreen,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    file?.name ?? 'Dosya Ekle',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.labelLarge?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  if (file == null)
                    Text(
                      'PDF, Word veya görsel',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    )
                  else
                    Text(
                      '${(file!.size / 1024).ceil()} KB',
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
                icon: const Icon(Icons.close_rounded, size: 18),
                color: AppColors.textSecondary,
                padding: EdgeInsets.zero,
                constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
              ),
          ],
        ),
      ),
    );
  }
}

class _AddAssignmentButton extends StatelessWidget {
  const _AddAssignmentButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: onTap,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(vertical: 14),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(
            color: AppColors.accentBlue.withValues(alpha: 0.4),
            width: 1.5,
          ),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: <Widget>[
            Icon(Icons.add_rounded, color: AppColors.accentBlue, size: 20),
            const SizedBox(width: 8),
            Text(
              'Ödev Ekle',
              style: Theme.of(context).textTheme.labelLarge?.copyWith(
                color: AppColors.accentBlue,
                fontWeight: FontWeight.w800,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
