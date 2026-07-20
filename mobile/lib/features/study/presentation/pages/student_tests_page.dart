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
  WeeklySummary? _weekly;
  List<StudySession> _sessions = const <StudySession>[];
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
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _tests = tests;
        _weekly = weekly;
        _sessions = sessions
            .where((StudySession s) => s.status == 'Completed')
            .toList();
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
            if (_weekly != null) ...<Widget>[
              const SizedBox(height: 24),
              const StudySectionHeader(title: 'Haftalık analiz'),
              const SizedBox(height: 12),
              _WeeklyBars(weekly: _weekly!),
            ],
            if (_sessions.isNotEmpty) ...<Widget>[
              const SizedBox(height: 24),
              const StudySectionHeader(title: 'Ders → konu kırılımı'),
              const SizedBox(height: 4),
              const Text(
                'Tüm zamanlar · derse ve konuya göre süre',
                style: TextStyle(color: AppColors.textSecondary, fontSize: 12),
              ),
              const SizedBox(height: 12),
              _LessonBreakdown(lessons: _aggregateLessons(_sessions)),
            ],
          ],
          const SizedBox(height: 24),
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
                'Deneme gir',
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
            child: const Icon(
              Icons.fact_check_rounded,
              color: AppColors.primary,
              size: 20,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  test.testName ?? test.subject,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                Text(
                  '${test.subject} · D:${test.correct} Y:${test.wrong} B:${test.blank}',
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 12,
                  ),
                ),
                Text(
                  StudyFormat.date(test.takenOnUtc),
                  style: const TextStyle(
                    color: AppColors.textMuted,
                    fontSize: 11,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Text(
                StudyFormat.net(test.net),
                style: const TextStyle(
                  fontWeight: FontWeight.w800,
                  color: AppColors.primary,
                  fontSize: 18,
                ),
              ),
              const Text(
                'net',
                style: TextStyle(color: AppColors.textMuted, fontSize: 11),
              ),
            ],
          ),
        ],
      ),
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
      return StudyCard(
        child: Text(
          'Bu hafta henüz çalışma yok.',
          style: const TextStyle(color: AppColors.textSecondary),
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
