import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/paged_list_boundary.dart';
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

  String? _nextCursor;
  bool _loadingMore = false;
  String? _moreError;
  int _generation = 0;

  Future<void> _loadMore() async {
    if (_loadingMore || _loading || _nextCursor == null) return;
    final generation = _generation;
    setState(() {
      _loadingMore = true;
      _moreError = null;
    });
    try {
      final page = await _repo.listNotesPage(
        widget.studentId,
        after: _nextCursor,
      );
      if (!mounted || generation != _generation) return;
      setState(() {
        final ids = _notes.map((x) => x.id).toSet();
        _notes = [..._notes, ...page.items.where((x) => !ids.contains(x.id))];
        _nextCursor = page.nextCursor;
        _loadingMore = false;
      });
    } on ApiException catch (error) {
      if (!mounted || generation != _generation) return;
      setState(() {
        _loadingMore = false;
        _moreError = error.message;
      });
    }
  }

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _loadingMore = false;
      _moreError = null;
      _nextCursor = null;
      _error = null;
    });
    try {
      final page = await _repo.listNotesPage(widget.studentId);
      if (!mounted || generation != _generation) return;
      setState(() {
        _notes = page.items;
        _nextCursor = page.nextCursor;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (mounted && generation == _generation) {
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
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (_) =>
          _NoteEditor(studentId: widget.studentId, existing: existing),
    );
    if (saved == true) await _load();
  }

  Future<void> _delete(StudyNote note) async {
    try {
      await _repo.deleteNote(note.id);
      await _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.message), backgroundColor: AppColors.error),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: Text(AppLocalizations.of(context).p19list_myNotes)),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: AppColors.primary,
        onPressed: _openEditor,
        icon: Icon(Icons.add_rounded, color: Colors.white),
        label: Text(
          AppLocalizations.of(context).p19list_addNote,
          style: TextStyle(color: Colors.white),
        ),
      ),
      body: _loading
          ? LoadingStateView(
              message: AppLocalizations.of(context).p19list_loading,
            )
          : _error != null
          ? ErrorStateView(message: _error!, onRetry: _load)
          : _content(),
    );
  }

  Widget _content() {
    if (_notes.isEmpty) {
      return EmptyStateView(
        title: AppLocalizations.of(context).p19list_noNotes,
        subtitle: AppLocalizations.of(context).p19list_notesHint,
      );
    }
    return PagedListBoundary(
      hasMore: _nextCursor != null,
      isLoading: _loadingMore,
      errorMessage: _moreError,
      onLoadMore: _loadMore,
      builder: (controller) => ListView.separated(
        controller: controller,
                  physics: const AlwaysScrollableScrollPhysics(),
        padding: EdgeInsets.fromLTRB(16, 12, 16, 96),
        itemCount: _notes.length,
        separatorBuilder: (_, __) => SizedBox(height: 12),
        itemBuilder: (context, i) => _NoteCard(
          note: _notes[i],
          onTap: () => _openEditor(existing: _notes[i]),
          onDelete: () => _delete(_notes[i]),
        ),
      ),
    );
  }
}

class _NoteCard extends StatelessWidget {
  const _NoteCard({
    required this.note,
    required this.onTap,
    required this.onDelete,
  });

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
        padding: EdgeInsets.all(16),
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
                  child: Padding(
                    padding: EdgeInsets.all(4),
                    child: Icon(
                      Icons.delete_outline_rounded,
                      color: AppColors.accentRed,
                      size: 20,
                    ),
                  ),
                ),
              ],
            ),
            if (tag.isNotEmpty) ...<Widget>[
              SizedBox(height: 4),
              Text(
                tag,
                style: TextStyle(
                  color: AppColors.primary,
                  fontWeight: FontWeight.w600,
                  fontSize: 12,
                ),
              ),
            ],
            SizedBox(height: 8),
            Text(
              note.body,
              maxLines: 3,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(
                context,
              ).textTheme.bodyMedium?.copyWith(color: AppColors.textSecondary),
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
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.message), backgroundColor: AppColors.error),
      );
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
              _isEdit
                  ? AppLocalizations.of(context).p19list_editNote
                  : AppLocalizations.of(context).p19list_addNote,
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w800,
              ),
            ),
            SizedBox(height: 16),
            TextFormField(
              controller: _title,
              decoration: InputDecoration(
                labelText: AppLocalizations.of(context).p19list_title,
              ),
              validator: (v) => (v == null || v.trim().isEmpty)
                  ? AppLocalizations.of(context).p19list_titleRequired
                  : null,
            ),
            SizedBox(height: 12),
            Row(
              children: <Widget>[
                Expanded(
                  child: TextFormField(
                    controller: _subject,
                    decoration: InputDecoration(
                      labelText: AppLocalizations.of(
                        context,
                      ).p19list_subjectOptional,
                    ),
                  ),
                ),
                SizedBox(width: 12),
                Expanded(
                  child: TextFormField(
                    controller: _topic,
                    decoration: InputDecoration(
                      labelText: AppLocalizations.of(
                        context,
                      ).p19list_topicOptional,
                    ),
                  ),
                ),
              ],
            ),
            SizedBox(height: 12),
            TextFormField(
              controller: _body,
              maxLines: 6,
              decoration: InputDecoration(
                labelText: AppLocalizations.of(context).p19list_content,
                alignLabelWithHint: true,
              ),
              validator: (v) => (v == null || v.trim().isEmpty)
                  ? AppLocalizations.of(context).p19list_contentRequired
                  : null,
            ),
            SizedBox(height: 16),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: AppColors.primary,
                ),
                onPressed: _saving ? null : _save,
                child: _saving
                    ? SizedBox(
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
                            ? AppLocalizations.of(context).p19list_save
                            : AppLocalizations.of(context).p19list_addNote,
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
