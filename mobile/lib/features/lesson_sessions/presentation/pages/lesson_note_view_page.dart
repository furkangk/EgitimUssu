import 'dart:io';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

class LessonNoteViewPayload {
  const LessonNoteViewPayload({
    required this.title,
    required this.meta,
    required this.noteText,
    required this.accent,
    this.sourceFilePath,
  });

  final String title;
  final String meta;
  final String noteText;
  final Color accent;
  final String? sourceFilePath;
}

class LessonNoteViewPage extends StatelessWidget {
  const LessonNoteViewPage({super.key, this.payload});

  final LessonNoteViewPayload? payload;

  static const _navy = Color(0xFF062B52);
  static const _text = Color(0xFF10233D);
  static const _slate = Color(0xFF7A8494);
  static const _background = Color(0xFFF4F8FC);
  static const _border = Color(0xFFE5EAF0);

  LessonNoteViewPayload get _note =>
      payload ??
      const LessonNoteViewPayload(
        title: 'Polinomlar Konu Anlatim.pdf',
        meta: '1.2 MB  20 Mayis',
        noteText:
            'Polinomlarda temel kavramlar, dereceler ve carpma-toplama kurallari anlatildi. Ders sonunda kisa bir soru cozum tekrarina yer verildi.',
        accent: Color(0xFF3D8BFF),
        sourceFilePath: null,
      );

  @override
  Widget build(BuildContext context) {
    final note = _note;

    return Scaffold(
      backgroundColor: _background,
      appBar: AppBar(
        backgroundColor: _background,
        surfaceTintColor: Colors.transparent,
        title: const Text('Ders Notu'),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
          children: <Widget>[
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(24),
                border: Border.all(color: _border),
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
                  Container(
                    width: 54,
                    height: 54,
                    decoration: BoxDecoration(
                      color: note.accent.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Icon(
                      Icons.description_rounded,
                      color: note.accent,
                      size: 26,
                    ),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          note.title,
                          style: Theme.of(context).textTheme.titleMedium
                              ?.copyWith(
                                color: _text,
                                fontWeight: FontWeight.w800,
                              ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          note.meta,
                          style: Theme.of(
                            context,
                          ).textTheme.bodySmall?.copyWith(color: _slate),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(24),
                border: Border.all(color: _border),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'Ders Notu Icerigi',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: _text,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 10),
                  Text(
                    note.noteText,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: _slate,
                      height: 1.5,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),
            SizedBox(
              width: double.infinity,
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: OutlinedButton.icon(
                      style: OutlinedButton.styleFrom(
                        foregroundColor: _navy,
                        side: BorderSide(color: _navy.withValues(alpha: 0.18)),
                        backgroundColor: _navy.withValues(alpha: 0.04),
                        padding: const EdgeInsets.symmetric(vertical: 14),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(16),
                        ),
                      ),
                      onPressed: () => _downloadFile(context, note),
                      icon: const Icon(Icons.download_rounded),
                      label: const Text('Indir'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: FilledButton(
                      style: FilledButton.styleFrom(
                        backgroundColor: _navy,
                        foregroundColor: Colors.white,
                        padding: const EdgeInsets.symmetric(vertical: 14),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(16),
                        ),
                      ),
                      onPressed: () => Navigator.of(context).pop(),
                      child: const Text('Kapat'),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _downloadFile(
    BuildContext context,
    LessonNoteViewPayload note,
  ) async {
    final sourcePath = note.sourceFilePath;
    if (sourcePath == null || sourcePath.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Bu not icin indirilebilir dosya yok.')),
      );
      return;
    }

    final sourceFile = File(sourcePath);
    if (!await sourceFile.exists()) {
      if (!context.mounted) {
        return;
      }
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Kaynak dosya bulunamadi.')));
      return;
    }

    try {
      final savePath = await FilePicker.platform.saveFile(
        dialogTitle: 'Ders notunu kaydet',
        fileName: note.title,
      );
      if (savePath == null || savePath.isEmpty) {
        return;
      }

      await sourceFile.copy(savePath);

      if (!context.mounted) {
        return;
      }
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('Dosya indirildi.')));
    } catch (_) {
      if (!context.mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Dosya indirilirken bir hata olustu.')),
      );
    }
  }
}
