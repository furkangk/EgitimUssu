import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/performance/personal_records.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/study_tab_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// "Performans" sekmesi (Task 7, 2026-08-19): özet istatistik + hedef net takibi
/// (`StudyGoal.targetNet`, yoksa demo) + net gelişim grafiği (ders/hafta-ay
/// filtreli, `listTests`) + konu bazlı iki ayrı bölüm (test istatistikleri =
/// `listSessions` ders→konu kırılımı, deneme istatistikleri = `listTests` ders
/// analizi) + konu eksiği (mastery alanı yok → demo harita) + haftalık/aylık
/// analiz (`getWeeklySummary` + `listSessions`den türetilen aylık kova) +
/// kişisel rekorlar (`getStreak` + `listTests` + `listSessions`den türetilir) +
/// Analiz & Gelişim girişleri (ogrenci_ux §8, spec 2026-07-21).
class StudentTestsPage extends StatefulWidget {
  const StudentTestsPage({super.key});

  @override
  State<StudentTestsPage> createState() => _StudentTestsPageState();
}

class _StudentTestsPageState extends State<StudentTestsPage> {
  StudyRepository get _repo => injector<StudyRepository>();

  String? _studentId;
  List<TestResult> _tests = const <TestResult>[];
  WeeklySummary? _weekly;
  List<StudySession> _sessions = const <StudySession>[];
  StudyGoal? _goal;
  StudyStreak? _streak;
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
      final studentId =
          _studentId ??
          await StudentScope.resolve(
            userId: session?.userId ?? '',
            fullName: session?.fullName ?? '',
          );
      final tests = await _repo.listTests(studentId);
      final weekly = await _repo.getWeeklySummary(studentId);
      final sessions = await _repo.listSessions(studentId);
      final goal = await _repo.getGoals(studentId);
      final streak = await _repo.getStreak(studentId);
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _tests = tests;
        _weekly = weekly;
        _sessions = sessions
            .where((StudySession s) => s.status == 'Completed')
            .toList();
        _goal = goal;
        _streak = streak;
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
      bottomNavigationBar: const StudentBottomNav(
        current: StudentNavTab.performance,
      ),
    );
  }

  Widget _content() {
    // En yeni önce (liste) — trend grafiği kronolojik (eski→yeni).
    final List<TestResult> byNewest = List<TestResult>.of(_tests)
      ..sort(
        (TestResult a, TestResult b) => b.takenOnUtc.compareTo(a.takenOnUtc),
      );
    final List<TestResult> chronological = byNewest.reversed.toList();

    final List<double> nets = _tests.map((TestResult t) => t.net).toList();
    final double avg = averageNet(nets);
    final double best = bestNet(nets);

    final double? realTarget = _goal?.targetNet;
    final bool targetIsDemo = realTarget == null;
    final double targetNet = realTarget ?? _demoTargetNet(best, avg);
    final double remaining = targetNet - avg < 0 ? 0 : targetNet - avg;
    final double ratio = targetNet <= 0 ? 0 : (avg / targetNet).clamp(0, 1);

    final _PersonalRecords records = _computeRecords(
      tests: _tests,
      sessions: _sessions,
      streak: _streak,
    );

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
        children: <Widget>[
          const AppPageHeader(
            title: 'Performans',
            subtitle:
                'Net gelişimini, çalışma analizini ve eksik konuları gör.',
          ),
          const SizedBox(height: 20),

          // 2) Özet istatistik satırı.
          Row(
            children: <Widget>[
              Expanded(
                child: StudyStatTile(
                  icon: Icons.assignment_turned_in_rounded,
                  color: AppColors.accentBlue,
                  value: '${_tests.length}',
                  label: 'Deneme',
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.calculate_rounded,
                  color: AppColors.accentTeal,
                  value: StudyFormat.net(avg),
                  label: 'Ort. net',
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.emoji_events_rounded,
                  color: AppColors.accentGreen,
                  value: StudyFormat.net(best),
                  label: 'En iyi net',
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: targetIsDemo
                    ? _DemoStatTile(
                        icon: Icons.flag_rounded,
                        color: AppColors.accentOrange,
                        value: StudyFormat.net(remaining),
                        label: 'Hedefe kalan',
                      )
                    : StudyStatTile(
                        icon: Icons.flag_rounded,
                        color: AppColors.accentOrange,
                        value: StudyFormat.net(remaining),
                        label: 'Hedefe kalan',
                      ),
              ),
            ],
          ),
          const SizedBox(height: 16),

          // 3) Test / Deneme gir.
          _AddTestButton(onTap: _addTest),
          const SizedBox(height: 24),

          // 4) Hedef net takibi.
          Row(
            children: <Widget>[
              const Expanded(
                child: StudySectionHeader(title: 'Hedef net takibi'),
              ),
              if (targetIsDemo) const StudyDemoBadge(),
            ],
          ),
          const SizedBox(height: 12),
          StudyCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: <Widget>[
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        const Text(
                          'Hedef net',
                          style: TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 12,
                          ),
                        ),
                        Text(
                          StudyFormat.net(targetNet),
                          style: const TextStyle(
                            fontWeight: FontWeight.w800,
                            color: AppColors.textPrimary,
                            fontSize: 20,
                          ),
                        ),
                      ],
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: <Widget>[
                        const Text(
                          'Ortalama net',
                          style: TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 12,
                          ),
                        ),
                        Text(
                          StudyFormat.net(avg),
                          style: const TextStyle(
                            fontWeight: FontWeight.w800,
                            color: AppColors.primary,
                            fontSize: 20,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
                const SizedBox(height: 14),
                StudyProgressBar(
                  value: ratio,
                  color: _targetProgressColor(ratio),
                  trailingLabel: remaining <= 0
                      ? 'Hedefine ulaştın!'
                      : '${StudyFormat.net(remaining)} net kaldı',
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),

          // 5) Net gelişim grafiği.
          const StudySectionHeader(title: 'Net gelişim grafiği'),
          const SizedBox(height: 12),
          _NetTrendSection(tests: chronological),
          const SizedBox(height: 24),

          // 6) Konu bazlı — iki ayrı bölüm.
          const StudySectionHeader(title: 'Test istatistikleri'),
          const SizedBox(height: 4),
          const Text(
            'Tüm zamanlar · derse ve konuya göre çalışma süresi',
            style: TextStyle(color: AppColors.textSecondary, fontSize: 12),
          ),
          const SizedBox(height: 12),
          _sessions.isEmpty
              ? const StudyCard(
                  child: Text(
                    'Henüz çalışma seansı yok.',
                    style: TextStyle(color: AppColors.textSecondary),
                  ),
                )
              : _LessonBreakdown(lessons: _aggregateLessons(_sessions)),
          const SizedBox(height: 24),

          const StudySectionHeader(title: 'Deneme istatistikleri'),
          const SizedBox(height: 12),
          _tests.isEmpty
              ? const StudyCard(
                  child: Text(
                    'Henüz deneme yok.',
                    style: TextStyle(color: AppColors.textSecondary),
                  ),
                )
              : _SubjectAnalysis(tests: _tests),
          const SizedBox(height: 24),

          // 7) Konu eksiği.
          Row(
            children: <Widget>[
              const Expanded(child: StudySectionHeader(title: 'Konu eksiği')),
              const StudyDemoBadge(),
            ],
          ),
          const SizedBox(height: 12),
          _WeakTopicsCard(studentId: _studentId ?? ''),
          const SizedBox(height: 24),

          // 8) Haftalık/Aylık analiz.
          const StudySectionHeader(title: 'Haftalık/Aylık analiz'),
          const SizedBox(height: 12),
          _PeriodAnalysisSection(weekly: _weekly, sessions: _sessions),
          const SizedBox(height: 24),

          // 9) Kişisel rekorlar.
          const StudySectionHeader(title: 'Kişisel rekorlar'),
          const SizedBox(height: 12),
          _RecordsGrid(records: records),
          const SizedBox(height: 24),

          // 10) Alt linkler.
          const StudySectionHeader(title: 'Analiz & Gelişim'),
          const SizedBox(height: 12),
          _PerfLink(
            icon: Icons.query_stats_rounded,
            color: AppColors.accentTeal,
            title: 'Detaylı analiz',
            subtitle: 'Haftalık geçmiş, manuel seanslar',
            onTap: () {
              final id = _studentId ?? '';
              if (id.isNotEmpty) context.push('/study/history?studentId=$id');
            },
          ),
          const SizedBox(height: 12),
          _PerfLink(
            icon: Icons.insights_rounded,
            color: AppColors.accentBlue,
            title: 'Gelişimim',
            subtitle: 'Konu bazlı hâkimiyet, eksik/güçlü konular',
            onTap: () {
              final id = _studentId ?? '';
              if (id.isNotEmpty) {
                context.push('/student/progress?studentId=$id');
              }
            },
          ),
        ],
      ),
    );
  }
}

/// Gerçek `StudyGoal.targetNet` yokken kullanılan makul demo hedef:
/// en iyi netin biraz üstü (motive edici, ulaşılabilir); hiç deneme yoksa
/// ortalamaya göre, o da yoksa sabit bir değere düşer.
double _demoTargetNet(double best, double avg) {
  if (best > 0) return best + 10;
  if (avg > 0) return avg + 15;
  return 90;
}

/// Hedef net ilerleme çubuğu renk kuralı: hedefe ulaşıldıysa yeşil, iyi
/// gidiyorsa teal, geride kalındıysa turuncu.
Color _targetProgressColor(double ratio) {
  if (ratio >= 1) return AppColors.accentGreen;
  if (ratio >= 0.6) return AppColors.accentTeal;
  return AppColors.accentOrange;
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
              child: Text(
                'Test / Deneme gir',
                style: TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.w800,
                  fontSize: 16,
                ),
              ),
            ),
            Icon(Icons.chevron_right_rounded, color: Colors.white),
          ],
        ),
      ),
    );
  }
}

/// Backend'i henüz olmayan/istemci tahmini istatistik kutusu — sağ üstte
/// "Demo" rozeti (`student_home_page.dart::_DemoStatTile` ile aynı desen).
class _DemoStatTile extends StatelessWidget {
  const _DemoStatTile({
    required this.icon,
    required this.color,
    required this.value,
    required this.label,
  });

  final IconData icon;
  final Color color;
  final String value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: <Widget>[
        StudyStatTile(icon: icon, color: color, value: value, label: label),
        const Positioned(top: 8, right: 8, child: StudyDemoBadge()),
      ],
    );
  }
}

enum _TrendPeriod { week, month }

/// Hafta/Ay segment anahtarı — hem net trendi hem çalışma analizinde kullanılır.
class _PeriodToggle extends StatelessWidget {
  const _PeriodToggle({required this.value, required this.onChanged});

  final _TrendPeriod value;
  final ValueChanged<_TrendPeriod> onChanged;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.skyBorder),
      ),
      child: Row(
        children: _TrendPeriod.values.map((_TrendPeriod p) {
          final bool selected = p == value;
          return Expanded(
            child: InkWell(
              borderRadius: BorderRadius.circular(10),
              onTap: () => onChanged(p),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                height: 36,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: selected ? AppColors.primary : Colors.transparent,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Text(
                  p == _TrendPeriod.week ? 'Hafta' : 'Ay',
                  style: TextStyle(
                    color: selected ? Colors.white : AppColors.textSecondary,
                    fontWeight: FontWeight.w700,
                    fontSize: 13,
                  ),
                ),
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}

/// Net gelişim grafiği — Genel/ders çipleri + Hafta/Ay filtresiyle
/// `listTests`ten filtrelenen kronolojik listeyi `_NetTrendChart`e besler.
class _NetTrendSection extends StatefulWidget {
  const _NetTrendSection({required this.tests});

  /// Kronolojik (eski→yeni) tüm denemeler.
  final List<TestResult> tests;

  @override
  State<_NetTrendSection> createState() => _NetTrendSectionState();
}

class _NetTrendSectionState extends State<_NetTrendSection> {
  static const String _general = 'Genel';
  String _subject = _general;
  _TrendPeriod _period = _TrendPeriod.month;

  @override
  Widget build(BuildContext context) {
    if (widget.tests.isEmpty) {
      return const StudyCard(
        child: Text(
          'Henüz deneme yok. İlk denemeni girerek net gelişimini takip et.',
          style: TextStyle(color: AppColors.textSecondary),
        ),
      );
    }
    final List<String> subjects = <String>[
      _general,
      ...<String>{for (final TestResult t in widget.tests) t.subject},
    ];
    if (!subjects.contains(_subject)) _subject = _general;

    final DateTime now = DateTime.now().toUtc();
    final DateTime cutoff = _period == _TrendPeriod.week
        ? now.subtract(const Duration(days: 7))
        : now.subtract(const Duration(days: 30));

    final List<TestResult> filtered = widget.tests.where((TestResult t) {
      final bool subjectOk = _subject == _general || t.subject == _subject;
      final bool periodOk = t.takenOnUtc.isAfter(cutoff);
      return subjectOk && periodOk;
    }).toList();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: subjects.map((String s) {
              final bool selected = s == _subject;
              return Padding(
                padding: const EdgeInsets.only(right: 8),
                child: ChoiceChip(
                  label: Text(s),
                  selected: selected,
                  onSelected: (_) => setState(() => _subject = s),
                  selectedColor: AppColors.primaryLight,
                  labelStyle: TextStyle(
                    color: selected
                        ? AppColors.primary
                        : AppColors.textSecondary,
                    fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                    fontSize: 12,
                  ),
                  backgroundColor: AppColors.surface,
                  side: const BorderSide(color: AppColors.skyBorder),
                ),
              );
            }).toList(),
          ),
        ),
        const SizedBox(height: 10),
        _PeriodToggle(
          value: _period,
          onChanged: (_TrendPeriod p) => setState(() => _period = p),
        ),
        const SizedBox(height: 12),
        filtered.isEmpty
            ? const StudyCard(
                child: Text(
                  'Bu aralıkta deneme yok.',
                  style: TextStyle(color: AppColors.textSecondary),
                ),
              )
            : _NetTrendChart(tests: filtered),
      ],
    );
  }
}

class _NetTrendChart extends StatelessWidget {
  const _NetTrendChart({required this.tests});

  final List<TestResult> tests;

  @override
  Widget build(BuildContext context) {
    final List<TestResult> points = tests.length > 12
        ? tests.sublist(tests.length - 12)
        : tests;
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
                  Text(
                    StudyFormat.net(t.net),
                    style: const TextStyle(
                      fontSize: 9,
                      color: AppColors.textMuted,
                    ),
                  ),
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
                  Text(
                    '${d.day}.${d.month}',
                    style: const TextStyle(
                      fontSize: 9,
                      color: AppColors.textSecondary,
                    ),
                  ),
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
    final Map<String, List<TestResult>> bySubject =
        <String, List<TestResult>>{};
    for (final TestResult t in tests) {
      bySubject.putIfAbsent(t.subject, () => <TestResult>[]).add(t);
    }
    final List<MapEntry<String, List<TestResult>>> entries =
        bySubject.entries.toList()..sort(
          (
            MapEntry<String, List<TestResult>> a,
            MapEntry<String, List<TestResult>> b,
          ) => b.value.length.compareTo(a.value.length),
        );

    final List<Widget> rows = <Widget>[];
    for (int i = 0; i < entries.length; i++) {
      final MapEntry<String, List<TestResult>> e = entries[i];
      final List<TestResult> list = List<TestResult>.of(e.value)
        ..sort(
          (TestResult a, TestResult b) => a.takenOnUtc.compareTo(b.takenOnUtc),
        );
      final double avg = averageNet(list.map((TestResult t) => t.net).toList());
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
              child: const Icon(
                Icons.menu_book_rounded,
                color: AppColors.accentTeal,
                size: 20,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    e.key,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  Text(
                    '${list.length} deneme · ort. ${StudyFormat.net(avg)} net',
                    style: const TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 12,
                    ),
                  ),
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
          Icon(
            up ? Icons.arrow_upward_rounded : Icons.arrow_downward_rounded,
            color: color,
            size: 14,
          ),
          const SizedBox(width: 2),
          Text(
            '${up ? '+' : ''}${StudyFormat.net(delta)} net',
            style: TextStyle(
              color: color,
              fontWeight: FontWeight.w700,
              fontSize: 12,
            ),
          ),
        ],
      ),
    );
  }
}

/// Backend'de konu hâkimiyet/skor alanı yok (Task 5'te de yoktu) — sabit demo
/// harita üzerinden `weakTopics` ile eşik altı konular gösterilir.
const Map<String, double> _demoTopicScores = <String, double>{
  'Paragraf': 38,
  'Türev': 42,
  'İntegral': 55,
  'Elektrik ve Manyetizma': 58,
  'Fonksiyonlar': 68,
  'Hücre Bölünmesi': 74,
};

class _WeakTopicsCard extends StatelessWidget {
  const _WeakTopicsCard({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context) {
    final List<String> weak = weakTopics(_demoTopicScores);
    if (weak.isEmpty) {
      return const StudyCard(
        child: Text(
          'Eksik konu tespit edilmedi.',
          style: TextStyle(color: AppColors.textSecondary),
        ),
      );
    }
    return StudyCard(
      child: Column(
        children: <Widget>[
          for (int i = 0; i < weak.length; i++) ...<Widget>[
            if (i > 0) const Divider(height: 20, color: AppColors.divider),
            _WeakTopicRow(
              topic: weak[i],
              score: _demoTopicScores[weak[i]] ?? 0,
              onStudy: studentId.isEmpty
                  ? null
                  : () => context.push('/study/timer?studentId=$studentId'),
            ),
          ],
        ],
      ),
    );
  }
}

class _WeakTopicRow extends StatelessWidget {
  const _WeakTopicRow({
    required this.topic,
    required this.score,
    required this.onStudy,
  });

  final String topic;
  final double score;
  final VoidCallback? onStudy;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Container(
          width: 36,
          height: 36,
          decoration: BoxDecoration(
            color: AppColors.accentRed.withValues(alpha: 0.12),
            borderRadius: BorderRadius.circular(10),
          ),
          child: const Icon(
            Icons.error_outline_rounded,
            color: AppColors.accentRed,
            size: 18,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                topic,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
              ),
              Text(
                'Skor: ${StudyFormat.net(score)}',
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 12,
                ),
              ),
            ],
          ),
        ),
        TextButton.icon(
          onPressed: onStudy,
          icon: const Icon(Icons.play_circle_outline_rounded, size: 18),
          label: const Text('Çalış'),
          style: TextButton.styleFrom(foregroundColor: AppColors.primary),
        ),
      ],
    );
  }
}

/// Haftalık/Aylık analiz segmenti: Hafta = gerçek `getWeeklySummary`
/// (`_WeeklyBars`), Ay = `listSessions`ten türetilen son 28 günün haftalık
/// kovaları (`_MonthlyBars`) — ikisi de gerçek veriden, demo değil.
class _PeriodAnalysisSection extends StatefulWidget {
  const _PeriodAnalysisSection({required this.weekly, required this.sessions});

  final WeeklySummary? weekly;
  final List<StudySession> sessions;

  @override
  State<_PeriodAnalysisSection> createState() => _PeriodAnalysisSectionState();
}

class _PeriodAnalysisSectionState extends State<_PeriodAnalysisSection> {
  _TrendPeriod _period = _TrendPeriod.week;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        _PeriodToggle(
          value: _period,
          onChanged: (_TrendPeriod p) => setState(() => _period = p),
        ),
        const SizedBox(height: 12),
        if (_period == _TrendPeriod.week)
          widget.weekly == null
              ? const StudyCard(
                  child: Text(
                    'Bu hafta henüz çalışma yok.',
                    style: TextStyle(color: AppColors.textSecondary),
                  ),
                )
              : _WeeklyBars(weekly: widget.weekly!)
        else
          _MonthlyBars(sessions: widget.sessions),
      ],
    );
  }
}

class _WeeklyBars extends StatelessWidget {
  const _WeeklyBars({required this.weekly});

  final WeeklySummary weekly;

  static const List<String> _dayLabels = <String>[
    'Pzt',
    'Sal',
    'Çar',
    'Per',
    'Cum',
    'Cmt',
    'Paz',
  ];

  @override
  Widget build(BuildContext context) {
    final List<DayMinutes> days = weekly.perDay;
    if (days.isEmpty) {
      return const StudyCard(
        child: Text(
          'Bu hafta henüz çalışma yok.',
          style: TextStyle(color: AppColors.textSecondary),
        ),
      );
    }
    final int maxMinutes = days.fold<int>(
      1,
      (int m, DayMinutes d) => d.effectiveMinutes > m ? d.effectiveMinutes : m,
    );
    return StudyCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Toplam: ${StudyFormat.minutes(weekly.totalEffectiveMinutes)}',
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
          Text(
            '${weekly.sessionCount} seans',
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 16),
          SizedBox(
            height: 150,
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: List<Widget>.generate(days.length, (int i) {
                final DayMinutes d = days[i];
                // Çubuk, metin ve boşluklardan artakalan alanı orantılı doldurur;
                // sabit yükseklik yerine Expanded kullanıldığı için font ölçeği
                // ne olursa olsun taşma olmaz.
                final double factor = (d.effectiveMinutes / maxMinutes).clamp(
                  0.0,
                  1.0,
                );
                return Expanded(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: <Widget>[
                      Text(
                        d.effectiveMinutes > 0 ? '${d.effectiveMinutes}' : '',
                        maxLines: 1,
                        style: const TextStyle(
                          fontSize: 10,
                          color: AppColors.textMuted,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Expanded(
                        child: FractionallySizedBox(
                          alignment: Alignment.bottomCenter,
                          heightFactor: factor < 0.03 ? 0.03 : factor,
                          child: Container(
                            margin: const EdgeInsets.symmetric(horizontal: 4),
                            decoration: BoxDecoration(
                              color: d.effectiveMinutes > 0
                                  ? AppColors.primary
                                  : AppColors.divider,
                              borderRadius: BorderRadius.circular(6),
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        i < _dayLabels.length ? _dayLabels[i] : '',
                        maxLines: 1,
                        style: const TextStyle(
                          fontSize: 11,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                );
              }),
            ),
          ),
        ],
      ),
    );
  }
}

/// Son 28 günü 4 haftalık kovaya toplar (`listSessions`ten türetilir —
/// gerçek veri, backend'de ayrı bir aylık özet endpoint'i yok).
class _MonthlyBars extends StatelessWidget {
  const _MonthlyBars({required this.sessions});

  final List<StudySession> sessions;

  static const List<String> _bucketLabels = <String>[
    '4h önce',
    '3h önce',
    '2h önce',
    'Bu hafta',
  ];

  @override
  Widget build(BuildContext context) {
    final DateTime now = DateTime.now();
    final DateTime today = DateTime(now.year, now.month, now.day);
    final List<int> bucketMinutes = List<int>.filled(4, 0);
    final List<int> bucketSessions = List<int>.filled(4, 0);
    for (final StudySession s in sessions) {
      final DateTime d = s.startedAtUtc.toLocal();
      final DateTime day = DateTime(d.year, d.month, d.day);
      final int daysAgo = today.difference(day).inDays;
      if (daysAgo < 0 || daysAgo >= 28) continue;
      final int bucket = 3 - (daysAgo ~/ 7);
      bucketMinutes[bucket] += s.effectiveMinutes;
      bucketSessions[bucket] += 1;
    }
    final int total = bucketMinutes.fold<int>(0, (int a, int b) => a + b);
    final int totalSessions = bucketSessions.fold<int>(
      0,
      (int a, int b) => a + b,
    );
    if (total == 0) {
      return const StudyCard(
        child: Text(
          'Son 28 günde çalışma yok.',
          style: TextStyle(color: AppColors.textSecondary),
        ),
      );
    }
    final int maxMinutes = bucketMinutes.fold<int>(
      1,
      (int m, int v) => v > m ? v : m,
    );
    return StudyCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Toplam: ${StudyFormat.minutes(total)}',
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
          Text(
            '$totalSessions seans · son 28 gün',
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 16),
          SizedBox(
            height: 150,
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: List<Widget>.generate(4, (int i) {
                final double factor = (bucketMinutes[i] / maxMinutes).clamp(
                  0.0,
                  1.0,
                );
                return Expanded(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: <Widget>[
                      Text(
                        bucketMinutes[i] > 0 ? '${bucketMinutes[i]}' : '',
                        maxLines: 1,
                        style: const TextStyle(
                          fontSize: 10,
                          color: AppColors.textMuted,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Expanded(
                        child: FractionallySizedBox(
                          alignment: Alignment.bottomCenter,
                          heightFactor: factor < 0.03 ? 0.03 : factor,
                          child: Container(
                            margin: const EdgeInsets.symmetric(horizontal: 6),
                            decoration: BoxDecoration(
                              color: bucketMinutes[i] > 0
                                  ? AppColors.accentTeal
                                  : AppColors.divider,
                              borderRadius: BorderRadius.circular(6),
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        _bucketLabels[i],
                        maxLines: 1,
                        style: const TextStyle(
                          fontSize: 11,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                );
              }),
            ),
          ),
        ],
      ),
    );
  }
}

/// Tamamlanmış tüm seansları önce derse, sonra konuya göre gruplar; süreye
/// göre azalan sırada döndürür (en çok çalışılan ders/konu üstte).
List<_LessonStat> _aggregateLessons(List<StudySession> sessions) {
  final Map<String, _LessonStat> map = <String, _LessonStat>{};
  for (final StudySession s in sessions) {
    map.putIfAbsent(s.subject, () => _LessonStat(s.subject)).add(s);
  }
  final List<_LessonStat> list = map.values.toList()
    ..sort((_LessonStat a, _LessonStat b) => b.minutes.compareTo(a.minutes));
  return list;
}

/// Bir dersin toplam süresi + konu kırılımı.
class _LessonStat {
  _LessonStat(this.subject);

  final String subject;
  int minutes = 0;
  int sessionCount = 0;
  final Map<String, _TopicStat> _topics = <String, _TopicStat>{};

  /// Süreye göre azalan sıralı konu listesi.
  List<_TopicStat> get topics {
    final List<_TopicStat> list = _topics.values.toList()
      ..sort((_TopicStat a, _TopicStat b) => b.minutes.compareTo(a.minutes));
    return list;
  }

  void add(StudySession s) {
    minutes += s.effectiveMinutes;
    sessionCount += 1;
    final String label = (s.topic == null || s.topic!.trim().isEmpty)
        ? 'Konu belirtilmemiş'
        : s.topic!.trim();
    _topics.putIfAbsent(label, () => _TopicStat(label)).add(s.effectiveMinutes);
  }
}

class _TopicStat {
  _TopicStat(this.topic);

  final String topic;
  int minutes = 0;
  int sessionCount = 0;

  void add(int m) {
    minutes += m;
    sessionCount += 1;
  }
}

/// Açılır ders→konu kırılımı: her ders bir satır, dokununca konuları açılır.
class _LessonBreakdown extends StatelessWidget {
  const _LessonBreakdown({required this.lessons});

  final List<_LessonStat> lessons;

  @override
  Widget build(BuildContext context) {
    return StudyCard(
      padding: const EdgeInsets.symmetric(vertical: 4, horizontal: 12),
      child: Column(
        children: <Widget>[
          for (int i = 0; i < lessons.length; i++) ...<Widget>[
            if (i > 0) const Divider(height: 1, color: AppColors.divider),
            _LessonRow(lesson: lessons[i], initiallyExpanded: i == 0),
          ],
        ],
      ),
    );
  }
}

class _LessonRow extends StatefulWidget {
  const _LessonRow({required this.lesson, this.initiallyExpanded = false});

  final _LessonStat lesson;
  final bool initiallyExpanded;

  @override
  State<_LessonRow> createState() => _LessonRowState();
}

class _LessonRowState extends State<_LessonRow> {
  late bool _expanded = widget.initiallyExpanded;

  @override
  Widget build(BuildContext context) {
    final _LessonStat lesson = widget.lesson;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        InkWell(
          borderRadius: BorderRadius.circular(12),
          onTap: () => setState(() => _expanded = !_expanded),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 2),
            child: Row(
              children: <Widget>[
                AnimatedRotation(
                  duration: const Duration(milliseconds: 250),
                  curve: Curves.easeInOut,
                  turns: _expanded ? 0.25 : 0.0,
                  child: const Icon(
                    Icons.chevron_right_rounded,
                    color: AppColors.textMuted,
                    size: 22,
                  ),
                ),
                const SizedBox(width: 6),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        lesson.subject,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      Text(
                        '${lesson.topics.length} konu · ${lesson.sessionCount} seans',
                        style: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                Text(
                  StudyFormat.minutes(lesson.minutes),
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    color: AppColors.primary,
                  ),
                ),
              ],
            ),
          ),
        ),
        AnimatedSize(
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeInOut,
          alignment: Alignment.topCenter,
          child: _expanded
              ? _TopicList(topics: lesson.topics)
              : const SizedBox(width: double.infinity),
        ),
      ],
    );
  }
}

class _TopicList extends StatelessWidget {
  const _TopicList({required this.topics});

  final List<_TopicStat> topics;

  @override
  Widget build(BuildContext context) {
    final int maxMinutes = topics.fold<int>(
      1,
      (int m, _TopicStat t) => t.minutes > m ? t.minutes : m,
    );
    return Padding(
      padding: const EdgeInsets.only(left: 34, bottom: 12),
      child: Column(
        children: topics.map((_TopicStat t) {
          final double ratio = (t.minutes / maxMinutes).clamp(0.0, 1.0);
          return Padding(
            padding: const EdgeInsets.only(bottom: 10),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Row(
                  children: <Widget>[
                    Expanded(
                      child: Text(
                        t.topic,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: AppColors.textPrimary,
                          fontSize: 13,
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Text(
                      StudyFormat.minutes(t.minutes),
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                ClipRRect(
                  borderRadius: BorderRadius.circular(6),
                  child: LinearProgressIndicator(
                    value: ratio,
                    minHeight: 6,
                    backgroundColor: AppColors.divider,
                    valueColor: const AlwaysStoppedAnimation<Color>(
                      AppColors.accentTeal,
                    ),
                  ),
                ),
              ],
            ),
          );
        }).toList(),
      ),
    );
  }
}

/// Kişisel rekorlar — `listTests`/`listSessions`/`getStreak`ten türetilir
/// (backend verisi var olduğu sürece gerçek; yalnız veri yoksa nötr `—`).
class _PersonalRecords {
  const _PersonalRecords({
    required this.bestNetValue,
    required this.longestStreakDays,
    required this.longestSessionMinutes,
    required this.mostStudiedDay,
    required this.mostStudiedMinutes,
    required this.mostEfficientSubject,
    required this.mostEfficientSubjectAvgNet,
  });

  final double bestNetValue;
  final int? longestStreakDays;
  final int longestSessionMinutes;
  final DateTime? mostStudiedDay;
  final int mostStudiedMinutes;
  final String? mostEfficientSubject;
  final double? mostEfficientSubjectAvgNet;
}

_PersonalRecords _computeRecords({
  required List<TestResult> tests,
  required List<StudySession> sessions,
  required StudyStreak? streak,
}) {
  final double best = bestNet(tests.map((TestResult t) => t.net).toList());

  int longestSession = 0;
  DateTime? bestDay;
  int bestDayMinutes = 0;
  if (sessions.isNotEmpty) {
    final Map<DateTime, int> byDay = <DateTime, int>{};
    for (final StudySession s in sessions) {
      if (s.effectiveMinutes > longestSession) {
        longestSession = s.effectiveMinutes;
      }
      final DateTime d = s.startedAtUtc.toLocal();
      final DateTime key = DateTime(d.year, d.month, d.day);
      byDay[key] = (byDay[key] ?? 0) + s.effectiveMinutes;
    }
    for (final MapEntry<DateTime, int> e in byDay.entries) {
      if (e.value > bestDayMinutes) {
        bestDayMinutes = e.value;
        bestDay = e.key;
      }
    }
  }

  String? bestSubject;
  double? bestSubjectAvg;
  if (tests.isNotEmpty) {
    final Map<String, List<double>> bySubject = <String, List<double>>{};
    for (final TestResult t in tests) {
      bySubject.putIfAbsent(t.subject, () => <double>[]).add(t.net);
    }
    for (final MapEntry<String, List<double>> e in bySubject.entries) {
      final double avg = averageNet(e.value);
      if (bestSubjectAvg == null || avg > bestSubjectAvg) {
        bestSubjectAvg = avg;
        bestSubject = e.key;
      }
    }
  }

  return _PersonalRecords(
    bestNetValue: best,
    longestStreakDays: streak?.longestStreakDays,
    longestSessionMinutes: longestSession,
    mostStudiedDay: bestDay,
    mostStudiedMinutes: bestDayMinutes,
    mostEfficientSubject: bestSubject,
    mostEfficientSubjectAvgNet: bestSubjectAvg,
  );
}

class _RecordsGrid extends StatelessWidget {
  const _RecordsGrid({required this.records});

  final _PersonalRecords records;

  @override
  Widget build(BuildContext context) {
    final DateTime? day = records.mostStudiedDay;
    return GridView.count(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      crossAxisCount: 2,
      childAspectRatio: 1.35,
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      children: <Widget>[
        StudyStatTile(
          icon: Icons.emoji_events_rounded,
          color: AppColors.accentGreen,
          value: StudyFormat.net(records.bestNetValue),
          label: 'En iyi net',
        ),
        StudyStatTile(
          icon: Icons.local_fire_department_rounded,
          color: AppColors.accentOrange,
          value: '${records.longestStreakDays ?? 0}',
          label: 'En uzun seri (gün)',
        ),
        StudyStatTile(
          icon: Icons.hourglass_bottom_rounded,
          color: AppColors.accentBlue,
          value: StudyFormat.minutes(records.longestSessionMinutes),
          label: 'En uzun tek seans',
        ),
        StudyStatTile(
          icon: Icons.event_available_rounded,
          color: AppColors.accentTeal,
          value: day == null ? '—' : '${day.day}.${day.month}',
          label: 'En çok çalışılan gün',
        ),
        StudyStatTile(
          icon: Icons.trending_up_rounded,
          color: AppColors.primary,
          value: records.mostEfficientSubject ?? '—',
          label: 'En verimli ders',
        ),
      ],
    );
  }
}

/// Performans sekmesi giriş satırı — tonlu ikon + başlık/alt + ok.
class _PerfLink extends StatelessWidget {
  const _PerfLink({
    required this.icon,
    required this.color,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final Color color;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: StudyCard(
        child: Row(
          children: <Widget>[
            Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: color.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(14),
              ),
              child: Icon(icon, color: color, size: 22),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    title,
                    style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 12,
                    ),
                  ),
                ],
              ),
            ),
            const Icon(
              Icons.chevron_right_rounded,
              color: AppColors.textSecondary,
            ),
          ],
        ),
      ),
    );
  }
}
