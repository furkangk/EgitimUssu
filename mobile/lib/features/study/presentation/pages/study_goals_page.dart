import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';

class StudyGoalsPage extends StatefulWidget {
  const StudyGoalsPage({super.key, required this.studentId});

  final String studentId;

  @override
  State<StudyGoalsPage> createState() => _StudyGoalsPageState();
}

class _StudyGoalsPageState extends State<StudyGoalsPage> {
  final _daily = TextEditingController();
  final _weekly = TextEditingController();
  final _targetNet = TextEditingController();
  StudySharing _sharing = StudySharing.none;
  bool _loading = true;
  bool _saving = false;
  String? _error;

  StudyRepository get _repo => injector<StudyRepository>();

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _daily.dispose();
    _weekly.dispose();
    _targetNet.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final goal = await _repo.getGoals(widget.studentId);
      final sharing = await _repo.getSharing(widget.studentId);
      if (!mounted) return;
      _daily.text = (goal?.dailyGoalMinutes ?? 60).toString();
      _weekly.text = goal?.weeklyGoalMinutes?.toString() ?? '';
      _targetNet.text = goal?.targetNet?.toString() ?? '';
      setState(() {
        _sharing = sharing;
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

  Future<void> _saveGoals() async {
    final daily = int.tryParse(_daily.text.trim()) ?? 0;
    if (daily <= 0) {
      _snack('Günlük hedef 0’dan büyük olmalı.');
      return;
    }
    setState(() => _saving = true);
    try {
      await _repo.updateGoals(
        widget.studentId,
        dailyGoalMinutes: daily,
        weeklyGoalMinutes: int.tryParse(_weekly.text.trim()),
        targetNet: double.tryParse(_targetNet.text.trim().replaceAll(',', '.')),
      );
      if (mounted) _snack('Hedefler kaydedildi.', success: true);
    } on ApiException catch (e) {
      if (mounted) _snack(e.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _updateSharing(StudySharing next) async {
    setState(() => _sharing = next);
    try {
      await _repo.updateSharing(
        widget.studentId,
        shareStudyWithParent: next.shareStudyWithParent,
        shareTestsWithParent: next.shareTestsWithParent,
        shareStudyWithTeacher: next.shareStudyWithTeacher,
        shareTestsWithTeacher: next.shareTestsWithTeacher,
      );
    } on ApiException catch (e) {
      if (mounted) _snack(e.message);
    }
  }

  void _snack(String m, {bool success = false}) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(m),
      backgroundColor: success ? AppColors.accentGreen : AppColors.error,
    ));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Hedefler & Paylaşım')),
      body: _loading
          ? const LoadingStateView()
          : _error != null
              ? ErrorStateView(message: _error!, onRetry: _load)
              : ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    const _SectionTitle('Çalışma hedefleri'),
                    _field(_daily, 'Günlük hedef (dakika)', number: true),
                    _field(_weekly, 'Haftalık hedef (dakika, opsiyonel)', number: true),
                    _field(_targetNet, 'Hedef net (opsiyonel)', number: true),
                    const SizedBox(height: 12),
                    AppPrimaryButton(label: 'Hedefleri kaydet', isLoading: _saving, onPressed: _saveGoals),
                    const SizedBox(height: 28),
                    const _SectionTitle('Veli ile paylaşım'),
                    _switch('Çalışma sürem', _sharing.shareStudyWithParent,
                        (v) => _updateSharing(_copy(studyParent: v))),
                    _switch('Deneme sonuçlarım', _sharing.shareTestsWithParent,
                        (v) => _updateSharing(_copy(testsParent: v))),
                    const SizedBox(height: 20),
                    const _SectionTitle('Öğretmen ile paylaşım'),
                    _switch('Çalışma sürem', _sharing.shareStudyWithTeacher,
                        (v) => _updateSharing(_copy(studyTeacher: v))),
                    _switch('Deneme sonuçlarım', _sharing.shareTestsWithTeacher,
                        (v) => _updateSharing(_copy(testsTeacher: v))),
                  ],
                ),
    );
  }

  StudySharing _copy({bool? studyParent, bool? testsParent, bool? studyTeacher, bool? testsTeacher}) {
    return StudySharing(
      shareStudyWithParent: studyParent ?? _sharing.shareStudyWithParent,
      shareTestsWithParent: testsParent ?? _sharing.shareTestsWithParent,
      shareStudyWithTeacher: studyTeacher ?? _sharing.shareStudyWithTeacher,
      shareTestsWithTeacher: testsTeacher ?? _sharing.shareTestsWithTeacher,
    );
  }

  Widget _field(TextEditingController c, String label, {bool number = false}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: TextField(
        controller: c,
        keyboardType: number
            ? const TextInputType.numberWithOptions(decimal: true)
            : TextInputType.text,
        decoration: InputDecoration(labelText: label, border: const OutlineInputBorder()),
      ),
    );
  }

  Widget _switch(String label, bool value, ValueChanged<bool> onChanged) {
    return SwitchListTile.adaptive(
      contentPadding: EdgeInsets.zero,
      title: Text(label, style: const TextStyle(color: AppColors.textPrimary)),
      value: value,
      onChanged: onChanged,
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);
  final String text;
  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: Text(text,
            style: const TextStyle(
                fontSize: 16, fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
      );
}
