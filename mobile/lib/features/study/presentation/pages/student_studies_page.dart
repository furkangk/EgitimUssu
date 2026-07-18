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

/// "Çalışmalarım" sekmesi — sayaç, çalışma geçmişi, derslere göre süre ve
/// çalışma istatistikleri (ogrenci_ux §7).
class StudentStudiesPage extends StatefulWidget {
  const StudentStudiesPage({super.key});

  @override
  State<StudentStudiesPage> createState() => _StudentStudiesPageState();
}

class _StudentStudiesPageState extends State<StudentStudiesPage> {
  StudyRepository get _repo => injector<StudyRepository>();

  String? _studentId;
  StudyStreak? _streak;
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
      final studentId = _studentId ??
          await StudentScope.resolve(
            userId: session?.userId ?? '',
            fullName: session?.fullName ?? '',
          );
      final streak = await _repo.getStreak(studentId);
      final weekly = await _repo.getWeeklySummary(studentId);
      final sessions = await _repo.listSessions(studentId);
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _streak = streak;
        _weekly = weekly;
        _sessions =
            sessions.where((StudySession s) => s.status == 'Completed').toList();
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
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        bottom: false,
        child: _loading
            ? const LoadingStateView(message: 'Çalışmaların yükleniyor...')
            : _error != null
                ? ErrorStateView(message: _error!, onRetry: _load)
                : _content(),
      ),
      bottomNavigationBar: const StudentBottomNav(current: StudentNavTab.studies),
    );
  }

  Widget _content() {
    final studentId = _studentId!;
    final streak = _streak!;
    final weekly = _weekly!;
    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
        children: <Widget>[
          const AppPageHeader(
            title: 'Çalışmalarım',
            subtitle: 'Süreni ve gelişimini takip et.',
          ),
          const SizedBox(height: 20),
          Row(
            children: <Widget>[
              Expanded(
                child: StudyStatTile(
                  icon: Icons.local_fire_department_rounded,
                  color: AppColors.accentOrange,
                  value: '${streak.currentStreakDays}',
                  label: 'Güncel seri',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.timelapse_rounded,
                  color: AppColors.accentTeal,
                  value: StudyFormat.minutes(weekly.totalEffectiveMinutes),
                  label: 'Bu hafta',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.event_available_rounded,
                  color: AppColors.accentGreen,
                  value: '${streak.totalStudyDays}',
                  label: 'Toplam gün',
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          _StartButton(studentId: studentId),
          const SizedBox(height: 24),
          const StudySectionHeader(title: 'Bu hafta'),
          const SizedBox(height: 12),
          _WeeklyBars(weekly: weekly),
          if (_sessions.isNotEmpty) ...<Widget>[
            const SizedBox(height: 24),
            const StudySectionHeader(title: 'Derslerim'),
            const SizedBox(height: 4),
            const Text(
              'Tüm zamanlar · derse ve konuya göre süre',
              style: TextStyle(color: AppColors.textSecondary, fontSize: 12),
            ),
            const SizedBox(height: 12),
            _LessonBreakdown(lessons: _aggregateLessons(_sessions)),
          ],
          const SizedBox(height: 24),
          StudySectionHeader(
            title: 'Son çalışmalar',
            action: _sessions.isEmpty
                ? null
                : StudySectionAction(
                    label: 'Tümü',
                    onTap: () =>
                        context.push('/study/history?studentId=$studentId'),
                  ),
          ),
          const SizedBox(height: 12),
          if (_sessions.isEmpty)
            const EmptyStateView(
              title: 'Henüz çalışma yok',
              subtitle: 'Kronometreyle ilk seansını başlat.',
            )
          else
            ..._sessions.take(6).map(
                  (StudySession s) => StudySessionTile(
                    subject: s.subject,
                    topic: s.topic,
                    minutes: s.effectiveMinutes,
                    endedAtUtc: s.endedAtUtc ?? s.startedAtUtc,
                    isManual: s.source == 'Manual',
                  ),
                ),
        ],
      ),
    );
  }
}

class _StartButton extends StatelessWidget {
  const _StartButton({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(20),
      onTap: () => context.push('/study/timer?studentId=$studentId'),
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
          children: <Widget>[
            const Icon(Icons.play_circle_fill_rounded,
                color: Colors.white, size: 34),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: const <Widget>[
                  Text('Çalışmaya başla',
                      style: TextStyle(
                          color: Colors.white,
                          fontWeight: FontWeight.w800,
                          fontSize: 16)),
                  SizedBox(height: 2),
                  Text('Kronometreyi başlat, serini büyüt.',
                      style: TextStyle(color: Colors.white70, fontSize: 12)),
                ],
              ),
            ),
            const Icon(Icons.chevron_right_rounded, color: Colors.white),
          ],
        ),
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
        child: Text('Bu hafta henüz çalışma yok.',
            style: const TextStyle(color: AppColors.textSecondary)),
      );
    }
    final int maxMinutes = days.fold<int>(
        1, (int m, DayMinutes d) => d.effectiveMinutes > m ? d.effectiveMinutes : m);
    return StudyCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text('Toplam: ${StudyFormat.minutes(weekly.totalEffectiveMinutes)}',
              style: const TextStyle(
                  fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
          Text('${weekly.sessionCount} seans',
              style:
                  const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
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
                final double factor =
                    (d.effectiveMinutes / maxMinutes).clamp(0.0, 1.0);
                return Expanded(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: <Widget>[
                      Text(d.effectiveMinutes > 0 ? '${d.effectiveMinutes}' : '',
                          maxLines: 1,
                          style: const TextStyle(
                              fontSize: 10, color: AppColors.textMuted)),
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
                      Text(i < _dayLabels.length ? _dayLabels[i] : '',
                          maxLines: 1,
                          style: const TextStyle(
                              fontSize: 11, color: AppColors.textSecondary)),
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
                  child: const Icon(Icons.chevron_right_rounded,
                      color: AppColors.textMuted, size: 22),
                ),
                const SizedBox(width: 6),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(lesson.subject,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                              fontWeight: FontWeight.w700,
                              color: AppColors.textPrimary)),
                      Text(
                          '${lesson.topics.length} konu · ${lesson.sessionCount} seans',
                          style: const TextStyle(
                              color: AppColors.textSecondary, fontSize: 12)),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                Text(StudyFormat.minutes(lesson.minutes),
                    style: const TextStyle(
                        fontWeight: FontWeight.w800, color: AppColors.primary)),
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
        1, (int m, _TopicStat t) => t.minutes > m ? t.minutes : m);
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
                      child: Text(t.topic,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                              color: AppColors.textPrimary, fontSize: 13)),
                    ),
                    const SizedBox(width: 8),
                    Text(StudyFormat.minutes(t.minutes),
                        style: const TextStyle(
                            color: AppColors.textSecondary, fontSize: 12)),
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
                        AppColors.accentTeal),
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
