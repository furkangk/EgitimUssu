import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';

/// Ders/konu kataloğu yönetimi. Öğrenci derslerini (örn. Matematik) ve her dersin
/// altına konularını (Türev, Limit, Olasılık) tanımlar. Bu katalog kronometre,
/// deneme girişi ve takvim formunda tutarlı ders/konu seçimi sağlar.
class SubjectCatalogPage extends StatefulWidget {
  const SubjectCatalogPage({super.key, required this.studentId});

  final String studentId;

  @override
  State<SubjectCatalogPage> createState() => _SubjectCatalogPageState();
}

class _SubjectCatalogPageState extends State<SubjectCatalogPage> {
  final StudyRepository _repo = injector<StudyRepository>();

  bool _loading = true;
  String? _error;
  List<SubjectCatalog> _subjects = <SubjectCatalog>[];

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
      final subjects = await _repo.listSubjects(widget.studentId);
      if (!mounted) return;
      setState(() {
        _subjects = subjects;
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

  Future<void> _addSubject() async {
    final name = await _promptText(
      title: 'Ders ekle',
      hint: 'Örn. Matematik',
    );
    if (name == null || name.trim().isEmpty) return;
    try {
      await _repo.createSubject(widget.studentId, name: name.trim());
      await _load();
    } on ApiException catch (e) {
      _snack(e.message);
    }
  }

  Future<void> _addTopic(SubjectCatalog subject) async {
    final name = await _promptText(
      title: '${subject.name} · konu ekle',
      hint: 'Örn. Türev',
    );
    if (name == null || name.trim().isEmpty) return;
    try {
      await _repo.addTopic(subject.id, name: name.trim());
      await _load();
    } on ApiException catch (e) {
      _snack(e.message);
    }
  }

  Future<void> _deleteSubject(SubjectCatalog subject) async {
    final ok = await _confirm(
      'Ders silinsin mi?',
      '"${subject.name}" ve altındaki konular kaldırılacak. Geçmiş çalışma/deneme kayıtların etkilenmez.',
    );
    if (ok != true) return;
    try {
      await _repo.deleteSubject(subject.id);
      await _load();
    } on ApiException catch (e) {
      _snack(e.message);
    }
  }

  Future<void> _deleteTopic(TopicCatalog topic) async {
    try {
      await _repo.deleteTopic(topic.id);
      await _load();
    } on ApiException catch (e) {
      _snack(e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Dersler & Konular')),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: AppColors.primary,
        onPressed: _addSubject,
        icon: const Icon(Icons.add_rounded, color: Colors.white),
        label: const Text('Ders ekle', style: TextStyle(color: Colors.white)),
      ),
      body: _loading
          ? const LoadingStateView(message: 'Yükleniyor...')
          : _error != null
              ? ErrorStateView(message: _error!, onRetry: _load)
              : _content(),
    );
  }

  Widget _content() {
    if (_subjects.isEmpty) {
      return const EmptyStateView(
        title: 'Henüz ders yok',
        subtitle:
            'Ders ekle, sonra altına konularını tanımla. Kronometre, deneme ve takvimde bu listeden seçebileceksin.',
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 96),
      itemCount: _subjects.length,
      separatorBuilder: (_, __) => const SizedBox(height: 12),
      itemBuilder: (context, i) => _SubjectCard(
        subject: _subjects[i],
        onAddTopic: () => _addTopic(_subjects[i]),
        onDeleteSubject: () => _deleteSubject(_subjects[i]),
        onDeleteTopic: _deleteTopic,
      ),
    );
  }

  void _snack(String message, {bool success = false}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(message),
      backgroundColor: success ? AppColors.accentGreen : AppColors.error,
    ));
  }

  Future<String?> _promptText({
    required String title,
    required String hint,
  }) {
    final controller = TextEditingController();
    return showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        backgroundColor: AppColors.surface,
        title: Text(title),
        content: TextField(
          controller: controller,
          autofocus: true,
          textCapitalization: TextCapitalization.sentences,
          decoration: InputDecoration(hintText: hint),
          onSubmitted: (v) => Navigator.of(dialogContext).pop(v),
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.primary),
            onPressed: () => Navigator.of(dialogContext).pop(controller.text),
            child: const Text('Ekle'),
          ),
        ],
      ),
    );
  }

  Future<bool?> _confirm(String title, String message) {
    return showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        backgroundColor: AppColors.surface,
        title: Text(title),
        content: Text(message),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: AppColors.accentRed),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Sil'),
          ),
        ],
      ),
    );
  }
}

class _SubjectCard extends StatelessWidget {
  const _SubjectCard({
    required this.subject,
    required this.onAddTopic,
    required this.onDeleteSubject,
    required this.onDeleteTopic,
  });

  final SubjectCatalog subject;
  final VoidCallback onAddTopic;
  final VoidCallback onDeleteSubject;
  final ValueChanged<TopicCatalog> onDeleteTopic;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      padding: const EdgeInsets.fromLTRB(16, 14, 8, 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  subject.name,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w800,
                      ),
                ),
              ),
              IconButton(
                onPressed: onAddTopic,
                icon: const Icon(Icons.add_rounded, color: AppColors.primary),
                tooltip: 'Konu ekle',
              ),
              IconButton(
                onPressed: onDeleteSubject,
                icon: const Icon(Icons.delete_outline_rounded,
                    color: AppColors.accentRed),
                tooltip: 'Dersi sil',
              ),
            ],
          ),
          if (subject.topics.isEmpty)
            Padding(
              padding: const EdgeInsets.only(left: 2, top: 2, bottom: 4),
              child: Text(
                'Konu ekleyerek başla',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
              ),
            )
          else
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: subject.topics
                  .map((t) => _TopicChip(topic: t, onDelete: () => onDeleteTopic(t)))
                  .toList(),
            ),
        ],
      ),
    );
  }
}

class _TopicChip extends StatelessWidget {
  const _TopicChip({required this.topic, required this.onDelete});

  final TopicCatalog topic;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.only(left: 12, right: 4, top: 4, bottom: 4),
      decoration: BoxDecoration(
        color: AppColors.primaryLight,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Text(
            topic.name,
            style: const TextStyle(
              color: AppColors.primary,
              fontWeight: FontWeight.w700,
            ),
          ),
          InkWell(
            borderRadius: BorderRadius.circular(999),
            onTap: onDelete,
            child: const Padding(
              padding: EdgeInsets.all(4),
              child: Icon(Icons.close_rounded, size: 16, color: AppColors.primary),
            ),
          ),
        ],
      ),
    );
  }
}
