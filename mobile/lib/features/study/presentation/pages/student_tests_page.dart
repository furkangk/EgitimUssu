import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/study_tab_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// "Testler" sekmesi — deneme girişi, sonuçlar, net grafiği ve ders bazlı
/// analiz (ogrenci_ux §8). Analiz `listTests` verisinden türetilir.
class StudentTestsPage extends StatefulWidget {
  const StudentTestsPage({super.key});

  @override
  State<StudentTestsPage> createState() => _StudentTestsPageState();
}

class _StudentTestsPageState extends State<StudentTestsPage> {
  StudyRepository get _repo => injector<StudyRepository>();

  String? _studentId;
  List<TestResult> _tests = const <TestResult>[];
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
      final session = context.read<AuthCubit>().state.session;
      final studentId = _studentId ??
          await StudentScope.resolve(
            userId: session?.userId ?? '',
            fullName: session?.fullName ?? '',
          );
      final tests = await _repo.listTests(studentId);
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _tests = tests;
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

  Future<void> _addTest() async {
    final String studentId = _studentId ?? '';
    if (studentId.isEmpty) return;
    await context.push('/study/test?studentId=$studentId');
    if (mounted) await _load();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        bottom: false,
        child: _loading
            ? const LoadingStateView(message: 'Denemelerin yükleniyor...')
            : _error != null
                ? ErrorStateView(message: _error!, onRetry: _load)
                : _content(),
      ),
      bottomNavigationBar: const StudentBottomNav(current: StudentNavTab.performance),
    );
  }

  Widget _content() {
    // En yeni önce (liste) — trend grafiği kronolojik (eski→yeni).
    final List<TestResult> byNewest = List<TestResult>.of(_tests)
      ..sort((TestResult a, TestResult b) => b.takenOnUtc.compareTo(a.takenOnUtc));
    final List<TestResult> chronological = byNewest.reversed.toList();

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
        children: <Widget>[
          const AppPageHeader(
            title: 'Testler',
            subtitle: 'Net gelişimini ve zayıf konuları gör.',
          ),
          const SizedBox(height: 20),
          if (_tests.isEmpty) ...<Widget>[
            const EmptyStateView(
              title: 'Henüz deneme yok',
              subtitle: 'İlk denemeni girerek net takibine başla.',
            ),
            const SizedBox(height: 16),
            _AddTestButton(onTap: _addTest),
          ] else ...<Widget>[
            _StatsRow(tests: _tests),
            const SizedBox(height: 16),
            _AddTestButton(onTap: _addTest),
            const SizedBox(height: 24),
            const StudySectionHeader(title: 'Net trendi'),
            const SizedBox(height: 12),
            _NetTrendChart(tests: chronological),
            const SizedBox(height: 24),
            const StudySectionHeader(title: 'Derslere göre net'),
            const SizedBox(height: 12),
            _SubjectAnalysis(tests: _tests),
            const SizedBox(height: 24),
            const StudySectionHeader(title: 'Son denemeler'),
            const SizedBox(height: 12),
            ...byNewest.take(8).map((TestResult t) => _TestTile(test: t)),
          ],
        ],
      ),
    );
  }
}

class _AddTestButton extends StatelessWidget {
  const _AddTestButton({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(20),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(18),
        decoration: BoxDecoration(
          gradient: const LinearGradient(
            colors: <Color>[AppColors.primary, AppColors.secondary],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          ),
          borderRadius: BorderRadius.circular(20),
          boxShadow: AppShadows.soft,
        ),
        child: Row(
          children: const <Widget>[
            Icon(Icons.edit_note_rounded, color: Colors.white, size: 32),
            SizedBox(width: 14),
            Expanded(
              child: Text('Deneme gir',
                  style: TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w800,
                      fontSize: 16)),
            ),
            Icon(Icons.chevron_right_rounded, color: Colors.white),
          ],
        ),
      ),
    );
  }
}

class _StatsRow extends StatelessWidget {
  const _StatsRow({required this.tests});

  final List<TestResult> tests;

  @override
  Widget build(BuildContext context) {
    final double avg =
        tests.fold<double>(0, (double s, TestResult t) => s + t.net) /
            tests.length;
    final double best = tests
        .map((TestResult t) => t.net)
        .fold<double>(tests.first.net, (double m, double n) => n > m ? n : m);
    return Row(
      children: <Widget>[
        Expanded(
          child: StudyStatTile(
            icon: Icons.assignment_turned_in_rounded,
            color: AppColors.accentBlue,
            value: '${tests.length}',
            label: 'Deneme',
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: StudyStatTile(
            icon: Icons.calculate_rounded,
            color: AppColors.accentTeal,
            value: StudyFormat.net(avg),
            label: 'Ortalama net',
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: StudyStatTile(
            icon: Icons.emoji_events_rounded,
            color: AppColors.accentGreen,
            value: StudyFormat.net(best),
            label: 'En iyi net',
          ),
        ),
      ],
    );
  }
}

class _NetTrendChart extends StatelessWidget {
  const _NetTrendChart({required this.tests});

  final List<TestResult> tests;

  @override
  Widget build(BuildContext context) {
    final List<TestResult> points =
        tests.length > 12 ? tests.sublist(tests.length - 12) : tests;
    final double maxNet = points
        .map((TestResult t) => t.net)
        .fold<double>(1, (double m, double n) => n > m ? n : m);
    return StudyCard(
      child: SizedBox(
        height: 150,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: points.map((TestResult t) {
            final double h = (t.net <= 0 ? 0 : t.net / maxNet) * 110;
            final DateTime d = t.takenOnUtc.toLocal();
            return Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.end,
                children: <Widget>[
                  Text(StudyFormat.net(t.net),
                      style: const TextStyle(
                          fontSize: 9, color: AppColors.textMuted)),
                  const SizedBox(height: 4),
                  Container(
                    margin: const EdgeInsets.symmetric(horizontal: 3),
                    height: h < 4 ? 4 : h,
                    decoration: BoxDecoration(
                      color: AppColors.accentBlue,
                      borderRadius: BorderRadius.circular(6),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text('${d.day}.${d.month}',
                      style: const TextStyle(
                          fontSize: 9, color: AppColors.textSecondary)),
                ],
              ),
            );
          }).toList(),
        ),
      ),
    );
  }
}

class _SubjectAnalysis extends StatelessWidget {
  const _SubjectAnalysis({required this.tests});

  final List<TestResult> tests;

  @override
  Widget build(BuildContext context) {
    // Ders → net değerleri (kronolojik trend için tarihe göre sıralı).
    final Map<String, List<TestResult>> bySubject = <String, List<TestResult>>{};
    for (final TestResult t in tests) {
      bySubject.putIfAbsent(t.subject, () => <TestResult>[]).add(t);
    }
    final List<MapEntry<String, List<TestResult>>> entries =
        bySubject.entries.toList()
          ..sort((MapEntry<String, List<TestResult>> a,
                  MapEntry<String, List<TestResult>> b) =>
              b.value.length.compareTo(a.value.length));

    final List<Widget> rows = <Widget>[];
    for (int i = 0; i < entries.length; i++) {
      final MapEntry<String, List<TestResult>> e = entries[i];
      final List<TestResult> list = List<TestResult>.of(e.value)
        ..sort((TestResult a, TestResult b) =>
            a.takenOnUtc.compareTo(b.takenOnUtc));
      final double avg =
          list.fold<double>(0, (double s, TestResult t) => s + t.net) /
              list.length;
      final double? delta = list.length >= 2
          ? list.last.net - list[list.length - 2].net
          : null;
      if (i > 0) {
        rows.add(const Divider(height: 20, color: AppColors.divider));
      }
      rows.add(
        Row(
          children: <Widget>[
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: AppColors.accentTeal.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(12),
              ),
              child: const Icon(Icons.menu_book_rounded,
                  color: AppColors.accentTeal, size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(e.key,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                          color: AppColors.textPrimary,
                          fontWeight: FontWeight.w600)),
                  Text('${list.length} deneme · ort. ${StudyFormat.net(avg)} net',
                      style: const TextStyle(
                          color: AppColors.textSecondary, fontSize: 12)),
                ],
              ),
            ),
            if (delta != null) ...<Widget>[
              const SizedBox(width: 8),
              _TrendChip(delta: delta),
            ],
          ],
        ),
      );
    }

    return StudyCard(child: Column(children: rows));
  }
}

class _TrendChip extends StatelessWidget {
  const _TrendChip({required this.delta});

  final double delta;

  @override
  Widget build(BuildContext context) {
    final bool up = delta >= 0;
    final Color color = up ? AppColors.accentGreen : AppColors.accentRed;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(up ? Icons.arrow_upward_rounded : Icons.arrow_downward_rounded,
              color: color, size: 14),
          const SizedBox(width: 2),
          Text('${up ? '+' : ''}${StudyFormat.net(delta)} net',
              style: TextStyle(
                  color: color, fontWeight: FontWeight.w700, fontSize: 12)),
        ],
      ),
    );
  }
}

class _TestTile extends StatelessWidget {
  const _TestTile({required this.test});

  final TestResult test;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      child: Row(
        children: <Widget>[
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Icon(Icons.fact_check_rounded,
                color: AppColors.primary, size: 20),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(test.testName ?? test.subject,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary)),
                Text('${test.subject} · D:${test.correct} Y:${test.wrong} B:${test.blank}',
                    style: const TextStyle(
                        color: AppColors.textSecondary, fontSize: 12)),
                Text(StudyFormat.date(test.takenOnUtc),
                    style: const TextStyle(
                        color: AppColors.textMuted, fontSize: 11)),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Text(StudyFormat.net(test.net),
                  style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      color: AppColors.primary,
                      fontSize: 18)),
              const Text('net',
                  style: TextStyle(color: AppColors.textMuted, fontSize: 11)),
            ],
          ),
        ],
      ),
    );
  }
}
