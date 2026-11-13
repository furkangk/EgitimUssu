import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/lesson_sessions/domain/lesson_session_contracts.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/form_fields.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
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
    this.studentId,
    this.lessonSessionId,
  });

  final String studentName;
  final String lessonName;
  final bool lockSelection;
  final String? studentId;
  final String? lessonSessionId;
}

class _LessonNoteFormPageState extends State<LessonNoteFormPage> {
  final _formKey = GlobalKey<FormState>();
  final _titleController = TextEditingController();
  final _noteController = TextEditingController();
  late final LessonSessionRepository _sessions;
  late final StudentRepository _students;
  late final AssignmentRepository _assignments;

  List<LessonSession> _options = [];
  Map<String, String> _studentNames = {};
  String? _studentId;
  String? _sessionId;
  FollowUpAssignment? _followUp;
  String? _error;
  String? _selectionNotice;
  bool _saveFailed = false;
  bool _isLoading = true;
  bool _isLoadingNote = false;
  bool _isSaving = false;
  bool _isSelectionLocked = false;
  int _noteRequest = 0;

  bool get _isBusy => _isLoading || _isLoadingNote || _isSaving;
  bool get _hasAssignments => _followUp?.assignments.isNotEmpty ?? false;
  bool get _canSave => !_isBusy && _followUp != null && !_hasAssignments;
  List<LessonSession> get _studentLessons =>
      _options.where((session) => session.studentId == _studentId).toList();

  @override
  void initState() {
    super.initState();
    _sessions = injector<LessonSessionRepository>();
    _students = injector<StudentRepository>();
    _assignments = injector<AssignmentRepository>();
    _loadOptions();
  }

  Future<void> _loadOptions() async {
    final teacherId = context.read<AuthCubit>().state.session?.userId;
    if (teacherId == null || teacherId.isEmpty) {
      setState(() {
        _isLoading = false;
        _error = 'Ders notu eklemek için yeniden giriş yapın.';
      });
      return;
    }
    setState(() {
      _isLoading = true;
      _error = null;
      _saveFailed = false;
    });
    try {
      final results = await Future.wait<Object>([
        _sessions.listTeacherSessions(teacherId),
        _students.listByTeacher(teacherId),
      ]);
      if (!mounted) return;
      final sessions =
          (results[0] as List<LessonSession>)
              .where(
                (session) =>
                    session.status == 'Completed' &&
                    session.teacherUserId == teacherId &&
                    session.id.isNotEmpty,
              )
              .toList()
            ..sort((a, b) => _lessonDate(b).compareTo(_lessonDate(a)));
      final names = {
        for (final student in results[1] as List<StudentProfile>)
          student.id: student.fullName,
      };
      final initial = widget.initialContext;
      final matches = initial == null
          ? <LessonSession>[]
          : sessions.where((session) {
              if (initial.lessonSessionId != null) {
                return session.id == initial.lessonSessionId;
              }
              final sameStudent = initial.studentId != null
                  ? session.studentId == initial.studentId
                  : names[session.studentId] == initial.studentName;
              return sameStudent && session.subject == initial.lessonName;
            }).toList();
      final selected = initial == null
          ? sessions.firstOrNull
          : matches.length == 1
          ? matches.single
          : null;
      final matchedStudents = matches
          .map((session) => session.studentId)
          .toSet();
      setState(() {
        _options = sessions;
        _studentNames = names;
        _studentId =
            selected?.studentId ??
            (matchedStudents.length == 1 ? matchedStudents.single : null);
        _sessionId = selected?.id;
        _selectionNotice = initial != null && selected == null
            ? 'İstenen ders tek bir tamamlanmış oturumla eşleşmedi. Öğrenci ve ders seçin.'
            : null;
        // Legacy display-name context is locked only if it identifies one real session.
        _isSelectionLocked =
            initial?.lockSelection == true && matches.length == 1;
        _isLoading = false;
      });
      if (selected != null) await _loadNote(selected);
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _isLoading = false;
        _error = _message(error, 'Dersler yüklenemedi. Tekrar deneyin.');
      });
    }
  }

  Future<void> _loadNote(LessonSession session) async {
    final request = ++_noteRequest;
    setState(() {
      _sessionId = session.id;
      _followUp = null;
      _isLoadingNote = true;
      _error = null;
      _selectionNotice = null;
      _saveFailed = false;
      _titleController.clear();
      _noteController.clear();
    });
    try {
      final followUp = await _assignments.getFollowUp(session.id);
      if (!mounted || request != _noteRequest) return;
      final separator = followUp.summary.indexOf('\n\n');
      setState(() {
        _followUp = followUp;
        _isLoadingNote = false;
        _titleController.text = separator > 0
            ? followUp.summary.substring(0, separator)
            : '${session.subject} ders notu';
        _noteController.text = separator > 0
            ? followUp.summary.substring(separator + 2)
            : followUp.summary;
      });
    } catch (error) {
      if (!mounted || request != _noteRequest) return;
      setState(() {
        _isLoadingNote = false;
        _error = _message(error, 'Ders notu yüklenemedi. Tekrar deneyin.');
      });
    }
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
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(8, 4, 16, 4),
              child: Row(
                children: [
                  IconButton(
                    onPressed: () {
                      if (context.canPop()) {
                        context.pop();
                      } else {
                        context.go('/dashboard');
                      }
                    },
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
            ),
            Expanded(
              child: _isLoading
                  ? const Center(child: CircularProgressIndicator())
                  : ListView(
                      padding: const EdgeInsets.fromLTRB(16, 10, 16, 32),
                      children: [
                        if (_error != null) ...[
                          Text(
                            _error!,
                            style: const TextStyle(color: AppColors.accentRed),
                          ),
                          if (!_isSaving && (!_saveFailed || _canSave))
                            TextButton(
                              onPressed: _saveFailed
                                  ? _save
                                  : _sessionId == null
                                  ? _loadOptions
                                  : () => _loadNote(
                                      _options.firstWhere(
                                        (s) => s.id == _sessionId,
                                      ),
                                    ),
                              child: const Text('Tekrar dene'),
                            ),
                          const SizedBox(height: 14),
                        ],
                        if (_options.isEmpty && _error == null)
                          const Text(
                            'Not ekleyebileceğiniz tamamlanmış bir ders yok. Önce bir ders oturumunu tamamlayın.',
                          ),
                        if (_options.isNotEmpty) ...[
                          if (_selectionNotice != null) ...[
                            Text(_selectionNotice!),
                            const SizedBox(height: 14),
                          ],
                          const AppFieldLabel(text: 'Öğrenci'),
                          const SizedBox(height: 8),
                          DropdownButtonFormField<String>(
                            key: ValueKey('student-$_studentId'),
                            initialValue: _studentId,
                            isExpanded: true,
                            decoration: appInputDecoration('Öğrenci seç'),
                            items: _options
                                .map((s) => s.studentId)
                                .toSet()
                                .map(
                                  (id) => DropdownMenuItem(
                                    value: id,
                                    child: Text(
                                      _studentNames[id] ?? 'Öğrenci',
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ),
                                )
                                .toList(),
                            onChanged: _isBusy || _isSelectionLocked
                                ? null
                                : (id) {
                                    if (id == null) return;
                                    setState(() => _studentId = id);
                                    _loadNote(_studentLessons.first);
                                  },
                          ),
                          const SizedBox(height: 14),
                          const AppFieldLabel(text: 'Ders'),
                          const SizedBox(height: 8),
                          DropdownButtonFormField<String>(
                            key: ValueKey('session-$_sessionId'),
                            initialValue: _sessionId,
                            isExpanded: true,
                            decoration: appInputDecoration(
                              'Tamamlanmış ders seç',
                            ),
                            items: _studentLessons
                                .map(
                                  (s) => DropdownMenuItem(
                                    value: s.id,
                                    child: Text(
                                      _lessonLabel(s),
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ),
                                )
                                .toList(),
                            onChanged: _isBusy || _isSelectionLocked
                                ? null
                                : (id) {
                                    if (id != null) {
                                      _loadNote(
                                        _options.firstWhere((s) => s.id == id),
                                      );
                                    }
                                  },
                          ),
                          const SizedBox(height: 14),
                          if (_isLoadingNote)
                            const Center(child: CircularProgressIndicator()),
                          if (_hasAssignments) ...[
                            const Text(
                              'Bu ders için not ve ödev takibi zaten oluşturulmuş. Mevcut takip korunur; yeni not için başka bir ders seçin.',
                            ),
                            const SizedBox(height: 14),
                          ],
                          AbsorbPointer(
                            absorbing: !_canSave,
                            child: Form(
                              key: _formKey,
                              child: Column(
                                children: [
                                  AppTextField(
                                    controller: _titleController,
                                    labelText: 'Başlık',
                                    hintText: 'Ders notu başlığı',
                                    textCapitalization:
                                        TextCapitalization.sentences,
                                    validator: _required,
                                  ),
                                  const SizedBox(height: 14),
                                  AppTextField(
                                    controller: _noteController,
                                    labelText: 'Ders notu',
                                    hintText: 'Ders notunu yaz',
                                    minLines: 6,
                                    maxLines: 10,
                                    textCapitalization:
                                        TextCapitalization.sentences,
                                    validator: _required,
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ],
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
        child: AppPrimaryButton(
          label: 'Ders Notunu Kaydet',
          isLoading: _isSaving,
          onPressed: _canSave ? _save : null,
        ),
      ),
    );
  }

  Future<void> _save() async {
    if (!_canSave || !(_formKey.currentState?.validate() ?? false)) return;
    final sessionId = _sessionId!;
    final summary =
        '${_titleController.text.trim()}\n\n${_noteController.text.trim()}';
    setState(() {
      _isSaving = true;
      _error = null;
      _saveFailed = false;
    });
    try {
      // Re-read before writing so another screen's metadata/assignments are never cleared.
      final existing = await _assignments.getFollowUp(sessionId);
      if (existing.assignments.isNotEmpty) {
        if (!mounted) return;
        setState(() => _followUp = existing);
        throw const ApiException(
          message:
              'Bu dersin ödev takibi oluşturulmuş. Mevcut not ve ödevler değiştirilmedi.',
        );
      }
      final saved = await _assignments.saveFollowUp(
        FollowUpAssignment(
          lessonSessionId: sessionId,
          summary: summary,
          coveredTopics: existing.coveredTopics,
          recommendations: existing.recommendations,
          assignments: existing.assignments,
          visibility: existing.visibility,
        ),
      );
      if (!mounted) return;
      setState(() => _followUp = saved);
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Ders notu kaydedildi.')));
      if (Navigator.of(context).canPop()) Navigator.of(context).pop(true);
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _error = _message(error, 'Ders notu kaydedilemedi. Tekrar deneyin.');
        _saveFailed = true;
      });
    } finally {
      if (mounted) setState(() => _isSaving = false);
    }
  }

  static String? _required(String? value) =>
      value == null || value.trim().isEmpty ? 'Bu alan zorunlu.' : null;
  static String _message(Object error, String fallback) =>
      error is ApiException ? error.message : fallback;
  static DateTime _lessonDate(LessonSession session) =>
      session.actualEndAtUtc ??
      session.plannedStartAtUtc ??
      DateTime.fromMillisecondsSinceEpoch(0);
  static String _lessonLabel(LessonSession session) {
    final date = _lessonDate(session).toLocal();
    return '${session.subject} · ${date.day}.${date.month}.${date.year} ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }
}
