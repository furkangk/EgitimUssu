import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:flutter/material.dart';

class TestEntryPage extends StatefulWidget {
  const TestEntryPage({super.key, required this.studentId});

  final String studentId;

  @override
  State<TestEntryPage> createState() => _TestEntryPageState();
}

class _TestEntryPageState extends State<TestEntryPage> {
  final _subject = TextEditingController();
  final _topic = TextEditingController();
  final _testName = TextEditingController();
  final _total = TextEditingController();
  final _correct = TextEditingController();
  final _wrong = TextEditingController();
  final _blank = TextEditingController();
  final _duration = TextEditingController();
  String _testType = 'General';
  bool _busy = false;

  static const _types = <String, String>{
    'General': 'Genel deneme',
    'Branch': 'Branş denemesi',
    'Subject': 'Ders denemesi',
    'Topic': 'Konu testi',
  };

  @override
  void dispose() {
    for (final c in [_subject, _topic, _testName, _total, _correct, _wrong, _blank, _duration]) {
      c.dispose();
    }
    super.dispose();
  }

  int _n(TextEditingController c) => int.tryParse(c.text.trim()) ?? 0;

  double get _netPreview {
    final penalty = 4;
    return _n(_correct) - (_n(_wrong) / penalty);
  }

  Future<void> _submit() async {
    final subject = _subject.text.trim();
    final total = _n(_total);
    final correct = _n(_correct);
    final wrong = _n(_wrong);
    final blank = _n(_blank);
    if (subject.isEmpty) {
      _snack('Ders alanı zorunlu.');
      return;
    }
    if (total <= 0 || correct + wrong + blank != total) {
      _snack('Doğru + yanlış + boş, toplam soruya eşit olmalı.');
      return;
    }
    setState(() => _busy = true);
    try {
      final result = await injector<StudyRepository>().recordTest(
        widget.studentId,
        subject: subject,
        topic: _topic.text.trim().isEmpty ? null : _topic.text.trim(),
        testType: _testType,
        testName: _testName.text.trim().isEmpty ? null : _testName.text.trim(),
        totalQuestions: total,
        correct: correct,
        wrong: wrong,
        blank: blank,
        penaltyDivisor: 4,
        durationMinutes: _n(_duration) > 0 ? _n(_duration) : null,
        takenOnUtc: DateTime.now().toUtc(),
      );
      if (!mounted) return;
      _snack('Deneme kaydedildi. Net: ${StudyFormat.net(result.net)}', success: true);
      Navigator.of(context).pop(true);
    } on ApiException catch (e) {
      if (mounted) _snack(e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _snack(String message, {bool success = false}) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(message),
      backgroundColor: success ? AppColors.accentGreen : AppColors.error,
    ));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Deneme Gir')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _field(_subject, 'Ders *', hint: 'Matematik'),
          _field(_topic, 'Konu (opsiyonel)'),
          _field(_testName, 'Deneme adı (opsiyonel)', hint: '3D TYT-5'),
          const SizedBox(height: 8),
          DropdownButtonFormField<String>(
            initialValue: _testType,
            decoration: const InputDecoration(labelText: 'Tür', border: OutlineInputBorder()),
            items: _types.entries
                .map((e) => DropdownMenuItem(value: e.key, child: Text(e.value)))
                .toList(),
            onChanged: (v) => setState(() => _testType = v ?? 'General'),
          ),
          const SizedBox(height: 8),
          Row(children: [
            Expanded(child: _field(_total, 'Toplam soru', number: true)),
            const SizedBox(width: 10),
            Expanded(child: _field(_duration, 'Süre (dk)', number: true)),
          ]),
          Row(children: [
            Expanded(child: _field(_correct, 'Doğru', number: true)),
            const SizedBox(width: 10),
            Expanded(child: _field(_wrong, 'Yanlış', number: true)),
            const SizedBox(width: 10),
            Expanded(child: _field(_blank, 'Boş', number: true)),
          ]),
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('Hesaplanan net',
                    style: TextStyle(color: AppColors.primary, fontWeight: FontWeight.w600)),
                Text(StudyFormat.net(_netPreview),
                    style: const TextStyle(
                        color: AppColors.primary, fontWeight: FontWeight.w800, fontSize: 20)),
              ],
            ),
          ),
          const SizedBox(height: 20),
          AppPrimaryButton(label: 'Kaydet', isLoading: _busy, onPressed: _submit),
        ],
      ),
    );
  }

  Widget _field(TextEditingController c, String label, {String? hint, bool number = false}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: TextField(
        controller: c,
        keyboardType: number ? TextInputType.number : TextInputType.text,
        onChanged: number ? (_) => setState(() {}) : null,
        decoration: InputDecoration(
          labelText: label,
          hintText: hint,
          border: const OutlineInputBorder(),
        ),
      ),
    );
  }
}
