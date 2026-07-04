import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_primary_button.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';

class StudyHistoryPage extends StatefulWidget {
  const StudyHistoryPage({super.key, required this.studentId});

  final String studentId;

  @override
  State<StudyHistoryPage> createState() => _StudyHistoryPageState();
}

class _StudyHistoryPageState extends State<StudyHistoryPage> {
  StudyRepository get _repo => injector<StudyRepository>();

  List<StudySession> _sessions = const [];
  List<TestResult> _tests = const [];
  WeeklySummary? _weekly;
  bool _loading = true;
  String? _error;

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
      final sessions = await _repo.listSessions(widget.studentId);
      final tests = await _repo.listTests(widget.studentId);
      final weekly = await _repo.getWeeklySummary(widget.studentId);
      if (!mounted) return;
      setState(() {
        _sessions = sessions.where((s) => s.status == 'Completed').toList();
        _tests = tests;
        _weekly = weekly;
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

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 3,
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(
          title: const Text('Çalışma Geçmişi'),
          bottom: const TabBar(tabs: [
            Tab(text: 'Seanslar'),
            Tab(text: 'Denemeler'),
            Tab(text: 'Haftalık'),
          ]),
        ),
        floatingActionButton: FloatingActionButton.extended(
          onPressed: _openManualSheet,
          icon: const Icon(Icons.add),
          label: const Text('Manuel seans'),
        ),
        body: _loading
            ? const LoadingStateView()
            : _error != null
                ? ErrorStateView(message: _error!, onRetry: _load)
                : TabBarView(children: [
                    _sessionsTab(),
                    _testsTab(),
                    _weeklyTab(),
                  ]),
      ),
    );
  }

  Widget _sessionsTab() {
    if (_sessions.isEmpty) {
      return const EmptyStateView(
          title: 'Seans yok', subtitle: 'Kronometreyle ilk çalışmanı başlat.');
    }
    return ListView.builder(
      padding: const EdgeInsets.all(16),
      itemCount: _sessions.length,
      itemBuilder: (context, i) {
        final s = _sessions[i];
        return Container(
          margin: const EdgeInsets.only(bottom: 10),
          padding: const EdgeInsets.all(14),
          decoration: _cardDeco,
          child: Row(
            children: [
              CircleAvatar(
                backgroundColor: AppColors.primaryLight,
                child: Icon(s.source == 'Manual' ? Icons.edit : Icons.timer,
                    color: AppColors.primary, size: 20),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(s.topic == null ? s.subject : '${s.subject} · ${s.topic}',
                        style: const TextStyle(
                            fontWeight: FontWeight.w600, color: AppColors.textPrimary)),
                    Text(_dateLabel(s.endedAtUtc ?? s.startedAtUtc),
                        style: const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
                  ],
                ),
              ),
              Text(StudyFormat.minutes(s.effectiveMinutes),
                  style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.primary)),
            ],
          ),
        );
      },
    );
  }

  Widget _testsTab() {
    if (_tests.isEmpty) {
      return const EmptyStateView(
          title: 'Deneme yok', subtitle: 'İlk denemeni girerek net takibine başla.');
    }
    return ListView.builder(
      padding: const EdgeInsets.all(16),
      itemCount: _tests.length,
      itemBuilder: (context, i) {
        final t = _tests[i];
        return Container(
          margin: const EdgeInsets.only(bottom: 10),
          padding: const EdgeInsets.all(14),
          decoration: _cardDeco,
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(t.testName ?? t.subject,
                        style: const TextStyle(
                            fontWeight: FontWeight.w600, color: AppColors.textPrimary)),
                    Text('${t.subject} · D:${t.correct} Y:${t.wrong} B:${t.blank}',
                        style: const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
                    Text(_dateLabel(t.takenOnUtc),
                        style: const TextStyle(color: AppColors.textMuted, fontSize: 11)),
                  ],
                ),
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(StudyFormat.net(t.net),
                      style: const TextStyle(
                          fontWeight: FontWeight.w800, color: AppColors.primary, fontSize: 18)),
                  const Text('net', style: TextStyle(color: AppColors.textMuted, fontSize: 11)),
                ],
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _weeklyTab() {
    final w = _weekly;
    if (w == null || w.perDay.isEmpty) {
      return const EmptyStateView(title: 'Veri yok', subtitle: 'Bu hafta henüz çalışma yok.');
    }
    final maxMinutes = w.perDay.fold<int>(1, (m, d) => d.effectiveMinutes > m ? d.effectiveMinutes : m);
    const dayLabels = ['Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt', 'Paz'];
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Container(
          padding: const EdgeInsets.all(16),
          decoration: _cardDeco,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Bu hafta toplam: ${StudyFormat.minutes(w.totalEffectiveMinutes)}',
                  style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
              Text('${w.sessionCount} seans',
                  style: const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
              const SizedBox(height: 16),
              SizedBox(
                height: 140,
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: List.generate(w.perDay.length, (i) {
                    final d = w.perDay[i];
                    final h = (d.effectiveMinutes / maxMinutes) * 110;
                    return Expanded(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: [
                          Text(d.effectiveMinutes > 0 ? '${d.effectiveMinutes}' : '',
                              style: const TextStyle(fontSize: 10, color: AppColors.textMuted)),
                          const SizedBox(height: 4),
                          Container(
                            margin: const EdgeInsets.symmetric(horizontal: 4),
                            height: h < 4 ? 4 : h,
                            decoration: BoxDecoration(
                              color: d.effectiveMinutes > 0 ? AppColors.primary : AppColors.divider,
                              borderRadius: BorderRadius.circular(6),
                            ),
                          ),
                          const SizedBox(height: 6),
                          Text(i < dayLabels.length ? dayLabels[i] : '',
                              style: const TextStyle(fontSize: 11, color: AppColors.textSecondary)),
                        ],
                      ),
                    );
                  }),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),
        if (w.perSubject.isNotEmpty) ...[
          const Text('Ders dağılımı',
              style: TextStyle(fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
          const SizedBox(height: 8),
          ...w.perSubject.map((s) => Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Row(
                  children: [
                    Expanded(child: Text(s.subject, style: const TextStyle(color: AppColors.textPrimary))),
                    Text(StudyFormat.minutes(s.effectiveMinutes),
                        style: const TextStyle(color: AppColors.textSecondary)),
                  ],
                ),
              )),
        ],
      ],
    );
  }

  Future<void> _openManualSheet() async {
    final subject = TextEditingController();
    final topic = TextEditingController();
    final minutes = TextEditingController();
    var busy = false;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) {
        return StatefulBuilder(
          builder: (context, setSheet) {
            return Padding(
              padding: EdgeInsets.only(
                left: 16,
                right: 16,
                top: 16,
                bottom: MediaQuery.of(context).viewInsets.bottom + 16,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Text('Manuel seans ekle',
                      style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
                  const SizedBox(height: 12),
                  TextField(
                    controller: subject,
                    decoration: const InputDecoration(labelText: 'Ders *', border: OutlineInputBorder()),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: topic,
                    decoration: const InputDecoration(labelText: 'Konu (opsiyonel)', border: OutlineInputBorder()),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: minutes,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(labelText: 'Süre (dakika) *', border: OutlineInputBorder()),
                  ),
                  const SizedBox(height: 16),
                  AppPrimaryButton(
                    label: 'Ekle',
                    isLoading: busy,
                    onPressed: () async {
                      final subj = subject.text.trim();
                      final mins = int.tryParse(minutes.text.trim()) ?? 0;
                      if (subj.isEmpty || mins <= 0) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(content: Text('Ders ve süre zorunlu.')),
                        );
                        return;
                      }
                      setSheet(() => busy = true);
                      try {
                        await _repo.createManualSession(
                          widget.studentId,
                          subject: subj,
                          topic: topic.text.trim().isEmpty ? null : topic.text.trim(),
                          effectiveMinutes: mins,
                          studiedOnUtc: DateTime.now().toUtc(),
                        );
                        if (sheetContext.mounted) Navigator.of(sheetContext).pop();
                        await _load();
                      } on ApiException catch (e) {
                        setSheet(() => busy = false);
                        if (sheetContext.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(content: Text(e.message)),
                          );
                        }
                      }
                    },
                  ),
                ],
              ),
            );
          },
        );
      },
    );
    subject.dispose();
    topic.dispose();
    minutes.dispose();
  }

  static final BoxDecoration _cardDeco = BoxDecoration(
    color: AppColors.surface,
    borderRadius: BorderRadius.circular(14),
    border: Border.all(color: AppColors.border),
  );

  String _dateLabel(DateTime utc) {
    final d = utc.toLocal();
    return '${d.day.toString().padLeft(2, '0')}.${d.month.toString().padLeft(2, '0')}.${d.year} ${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
  }
}
