import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';

/// Öğrencinin kendi ders notları. Öğretmenin ders notundan ayrı; öğrenci kendi dersleri için
/// başlık + içerik (+ opsiyonel ders/konu) ile not ekler, düzenler, siler.
class StudyNotesPage extends StatefulWidget {
  const StudyNotesPage({super.key, required this.studentId});

  final String studentId;

  @override
  State<StudyNotesPage> createState() => _StudyNotesPageState();
}

class _StudyNotesPageState extends State<StudyNotesPage> {
  final StudyRepository _repo = injector<StudyRepository>();

  bool _loading = true;
  String? _error;
  List<StudyNote> _notes = <StudyNote>[];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final notes = await _repo.listNotes(widget.studentId);
      if (!mounted) return;
      setState(() {
        _notes = notes;
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

  Future<void> _openEditor({StudyNote? existing}) async {
    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (_) => _NoteEditor(studentId: widget.studentId, existing: existing),
    );
    if (saved == true) await _load();
  }

  Future<void> _delete(StudyNote note) async {
    try {
      await _repo.deleteNote(note.id);
      await _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(
          content: Text(e.message),
          backgroundColor: AppColors.error,
        ));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Notlarım')),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: AppColors.primary,
        onPressed: _openEditor,
        icon: const Icon(Icons.add_rounded, color: Colors.white),
        label: const Text('Not ekle', style: TextStyle(color: Colors.white)),
      ),
      body: _loading
          ? const LoadingStateView(message: 'Yükleniyor...')
          : _error != null
              ? ErrorStateView(message: _error!, onRetry: _load)
              : _content(),
    );
  }

  Widget _content() {
    if (_notes.isEmpty) {
      return const EmptyStateView(
        title: 'Henüz not yok',
        subtitle: 'Kendi derslerin için not ekle; istersen ders ve konu ile ilişkilendir.',
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
      itemCount: _notes.length,
      separatorBuilder: (_, __) => const SizedBox(height: 12),
      itemBuilder: (context, i) => _NoteCard(
        note: _notes[i],
        onTap: () => _openEditor(existing: _notes[i]),
        onDelete: () => _delete(_notes[i]),
      ),
    );
  }
}

class _NoteCard extends StatelessWidget {
  const _NoteCard({required this.note, required this.onTap, required this.onDelete});

  final StudyNote note;
  final VoidCallback onTap;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final tag = <String>[
      if (note.subject != null && note.subject!.isNotEmpty) note.subject!,
      if (note.topic != null && note.topic!.isNotEmpty) note.topic!,
    ].join(' · ');
    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: onTap,
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(color: AppColors.skyBorder),
          boxShadow: AppShadows.soft,
        ),
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    note.title,
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          color: AppColors.textPrimary,
                          fontWeight: FontWeight.w800,
                        ),
                  ),
                ),
                InkWell(
                  borderRadius: BorderRadius.circular(999),
                  onTap: onDelete,
                  child: const Padding(
                    padding: EdgeInsets.all(4),
                    child: Icon(Icons.delete_outline_rounded,
                        color: AppColors.accentRed, size: 20),
                  ),
                ),
              ],
            ),
            if (tag.isNotEmpty) ...<Widget>[
              const SizedBox(height: 4),
              Text(
                tag,
                style: const TextStyle(
                  color: AppColors.primary,
                  fontWeight: FontWeight.w600,
                  fontSize: 12,
                ),
              ),
            ],
            const SizedBox(height: 8),
            Text(
              note.body,
              maxLines: 3,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: AppColors.textSecondary,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}

class _NoteEditor extends StatefulWidget {
  const _NoteEditor({required this.studentId, this.existing});

  final String studentId;
  final StudyNote? existing;

  @override
  State<_NoteEditor> createState() => _NoteEditorState();
}

class _NoteEditorState extends State<_NoteEditor> {
  final StudyRepository _repo = injector<StudyRepository>();
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _title;
  late final TextEditingController _body;
  late final TextEditingController _subject;
  late final TextEditingController _topic;
  bool _saving = false;

  bool get _isEdit => widget.existing != null;

  @override
  void initState() {
    super.initState();
    _title = TextEditingController(text: widget.existing?.title ?? '');
    _body = TextEditingController(text: widget.existing?.body ?? '');
    _subject = TextEditingController(text: widget.existing?.subject ?? '');
    _topic = TextEditingController(text: widget.existing?.topic ?? '');
  }

  @override
  void dispose() {
    _title.dispose();
    _body.dispose();
    _subject.dispose();
    _topic.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() => _saving = true);
    final subject = _subject.text.trim().isEmpty ? null : _subject.text.trim();
    final topic = _topic.text.trim().isEmpty ? null : _topic.text.trim();
    try {
      if (_isEdit) {
        await _repo.updateNote(
          widget.existing!.id,
          title: _title.text.trim(),
          body: _body.text.trim(),
          subject: subject,
          topic: topic,
        );
      } else {
        await _repo.createNote(
          widget.studentId,
          title: _title.text.trim(),
          body: _body.text.trim(),
          subject: subject,
          topic: topic,
        );
      }
      if (!mounted) return;
      Navigator.of(context).pop(true);
    } on ApiException catch (e) {
      if (!mounted) return;
      setState(() => _saving = false);
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(
          content: Text(e.message),
          backgroundColor: AppColors.error,
        ));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(
        left: 20,
        right: 20,
        top: 20,
        bottom: MediaQuery.of(context).viewInsets.bottom + 20,
      ),
      child: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(
              _isEdit ? 'Notu düzenle' : 'Not ekle',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _title,
              decoration: const InputDecoration(labelText: 'Başlık'),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'Başlık zorunlu.' : null,
            ),
            const SizedBox(height: 12),
            Row(
              children: <Widget>[
                Expanded(
                  child: TextFormField(
                    controller: _subject,
                    decoration: const InputDecoration(labelText: 'Ders (opsiyonel)'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: TextFormField(
                    controller: _topic,
                    decoration: const InputDecoration(labelText: 'Konu (opsiyonel)'),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _body,
              maxLines: 6,
              decoration: const InputDecoration(
                labelText: 'İçerik',
                alignLabelWithHint: true,
              ),
              validator: (v) =>
                  (v == null || v.trim().isEmpty) ? 'İçerik zorunlu.' : null,
            ),
            const SizedBox(height: 16),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                style: FilledButton.styleFrom(backgroundColor: AppColors.primary),
                onPressed: _saving ? null : _save,
                child: _saving
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          valueColor: AlwaysStoppedAnimation<Color>(Colors.white),
                        ),
                      )
                    : Text(_isEdit ? 'Kaydet' : 'Not ekle'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
