import 'dart:io';

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_client.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/lesson_resources_cubit.dart';
import 'package:egitim_ussu_mobile/features/assignments/presentation/cubit/lesson_resources_state.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:url_launcher/url_launcher.dart';

/// M06-1 (P04): Ders kaynakları bölümü — hem öğretmen (ekle/sil) hem öğrenci (görüntüle) ekranlarında.
///
/// Yetki sunucuda zorlanır; [canManage] yalnız arayüzü sadeleştirir.
class LessonResourcesSection extends StatefulWidget {
  const LessonResourcesSection({
    super.key,
    this.teacherUserId,
    this.studentId,
    this.lessonSessionId,
    this.canManage = false,
    this.cubit,
  });

  /// Öğretmen görünümü: kendi kaynakları.
  final String? teacherUserId;

  /// Öğrenci görünümü: kendisine açık kaynaklar.
  final String? studentId;

  final String? lessonSessionId;

  /// Öğretmen ise ekleme/silme eylemleri gösterilir.
  final bool canManage;

  /// Testlerde hazır cubit verilebilir.
  final LessonResourcesCubit? cubit;

  @override
  State<LessonResourcesSection> createState() => _LessonResourcesSectionState();
}

class _LessonResourcesSectionState extends State<LessonResourcesSection> {
  late final LessonResourcesCubit _cubit;

  @override
  void initState() {
    super.initState();
    _cubit = widget.cubit ?? LessonResourcesCubit.create();
    _cubit.load(
      teacherUserId: widget.teacherUserId,
      studentId: widget.studentId,
      lessonSessionId: widget.lessonSessionId,
    );
  }

  @override
  void dispose() {
    if (widget.cubit == null) {
      _cubit.close();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return BlocProvider<LessonResourcesCubit>.value(
      value: _cubit,
      child: BlocBuilder<LessonResourcesCubit, LessonResourcesState>(
        builder: (context, state) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  const Icon(
                    Icons.folder_shared_rounded,
                    size: 18,
                    color: AppColors.primary,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Ders kaynakları',
                      style: Theme.of(context).textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w800,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                  if (widget.canManage)
                    TextButton.icon(
                      onPressed: state.isSaving ? null : _showAddSheet,
                      icon: const Icon(Icons.add_rounded, size: 18),
                      label: const Text('Kaynak ekle'),
                    ),
                ],
              ),
              const SizedBox(height: 8),
              if (state.isLoading)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 16),
                  child: Center(child: CircularProgressIndicator()),
                )
              else if (state.errorMessage != null)
                _InfoText(text: state.errorMessage!, isError: true)
              else if (state.resources.isEmpty)
                _InfoText(
                  text: widget.canManage
                      ? 'Henüz kaynak eklemedin.'
                      : 'Bu ders için paylaşılan kaynak yok.',
                )
              else
                ...state.resources.map(
                  (resource) => _ResourceTile(
                    resource: resource,
                    canManage: widget.canManage,
                    onOpen: () => _openResource(resource),
                    onArchive: () => _archive(resource),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }

  Future<void> _openResource(LessonResource resource) async {
    if (!resource.isFile) {
      final url = resource.url;
      if (url == null) return;
      final uri = Uri.tryParse(url);
      if (uri == null || !await launchUrl(uri, mode: LaunchMode.externalApplication)) {
        _notify('Bağlantı açılamadı: $url');
      }
      return;
    }

    final downloadUrl = resource.downloadUrl;
    if (downloadUrl == null) return;

    try {
      // Dosya yetkili uçtan indirilir (public URL yok), geçici dizine yazılır.
      final bytes = await injector<ApiClient>().getBytes(downloadUrl);
      if (bytes == null) {
        _notify('Dosya bulunamadı.');
        return;
      }

      final file = File(
        '${Directory.systemTemp.path}/${resource.fileName ?? 'kaynak'}',
      );
      await file.writeAsBytes(bytes);
      _notify('İndirildi: ${file.path}');
    } catch (error) {
      _notify('Dosya indirilemedi: $error');
    }
  }

  Future<void> _archive(LessonResource resource) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Kaynağı sil'),
        content: Text('"${resource.title}" kaynağı listeden kaldırılsın mı?'),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Vazgeç'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Sil'),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      await _cubit.archiveResource(resource.id);
    }
  }

  void _showAddSheet() {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      builder: (sheetContext) => Padding(
        padding: const EdgeInsets.fromLTRB(20, 6, 20, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            ListTile(
              leading: const Icon(Icons.attach_file_rounded),
              title: const Text('Dosya yükle'),
              subtitle: const Text('En fazla 10 MB; pdf, görsel, ofis dosyaları'),
              onTap: () {
                Navigator.of(sheetContext).pop();
                _addFileResource();
              },
            ),
            ListTile(
              leading: const Icon(Icons.link_rounded),
              title: const Text('Bağlantı ekle'),
              subtitle: const Text('Video, doküman veya site adresi'),
              onTap: () {
                Navigator.of(sheetContext).pop();
                _addLinkResource();
              },
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _addFileResource() async {
    final picked = await FilePicker.platform.pickFiles();
    final path = picked?.files.single.path;
    if (path == null) return;

    final title = await _askText(
      title: 'Kaynak başlığı',
      initialValue: picked!.files.single.name,
    );
    if (title == null || title.trim().isEmpty) return;

    final ok = await _cubit.addResource(title: title.trim(), filePath: path);
    _notify(ok ? 'Kaynak eklendi.' : _cubit.state.errorMessage ?? 'Kaynak eklenemedi.');
  }

  Future<void> _addLinkResource() async {
    final url = await _askText(title: 'Bağlantı adresi', initialValue: 'https://');
    if (url == null || url.trim().isEmpty) return;

    final title = await _askText(title: 'Kaynak başlığı', initialValue: '');
    if (title == null || title.trim().isEmpty) return;

    final ok = await _cubit.addResource(title: title.trim(), url: url.trim());
    _notify(ok ? 'Kaynak eklendi.' : _cubit.state.errorMessage ?? 'Kaynak eklenemedi.');
  }

  Future<String?> _askText({
    required String title,
    required String initialValue,
  }) {
    final controller = TextEditingController(text: initialValue);
    return showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(title),
        content: TextField(controller: controller, autofocus: true),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Vazgeç'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(controller.text),
            child: const Text('Tamam'),
          ),
        ],
      ),
    );
  }

  void _notify(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }
}

class _ResourceTile extends StatelessWidget {
  const _ResourceTile({
    required this.resource,
    required this.canManage,
    required this.onOpen,
    required this.onArchive,
  });

  final LessonResource resource;
  final bool canManage;
  final VoidCallback onOpen;
  final VoidCallback onArchive;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.border),
      ),
      child: ListTile(
        onTap: onOpen,
        leading: Icon(
          resource.isFile ? Icons.description_rounded : Icons.link_rounded,
          color: AppColors.primary,
        ),
        title: Text(
          resource.title,
          style: const TextStyle(fontWeight: FontWeight.w700),
        ),
        subtitle: Text(
          resource.isFile
              ? '${resource.fileName ?? ''} · ${_formatSize(resource.sizeBytes)}'
              : (resource.url ?? ''),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
        ),
        trailing: canManage
            ? IconButton(
                icon: const Icon(Icons.delete_outline_rounded),
                onPressed: onArchive,
                tooltip: 'Kaynağı sil',
              )
            : null,
      ),
    );
  }

  static String _formatSize(int? bytes) {
    if (bytes == null || bytes <= 0) return '';
    if (bytes < 1024) return '$bytes B';
    if (bytes < 1024 * 1024) return '${(bytes / 1024).toStringAsFixed(0)} KB';
    return '${(bytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }
}

class _InfoText extends StatelessWidget {
  const _InfoText({required this.text, this.isError = false});

  final String text;
  final bool isError;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Text(
        text,
        style: Theme.of(context).textTheme.bodySmall?.copyWith(
          color: isError ? AppColors.error : AppColors.textSecondary,
        ),
      ),
    );
  }
}
