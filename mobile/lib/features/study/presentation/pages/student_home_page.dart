import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_home_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_home_state.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class StudentHomePage extends StatelessWidget {
  const StudentHomePage({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider<StudyHomeCubit>(
      create: (_) => StudyHomeCubit.create()..load(
          userId: context.read<AuthCubit>().state.session?.userId ?? '',
          fullName: context.read<AuthCubit>().state.session?.fullName ?? ''),
      child: const _StudentHomeView(),
    );
  }
}

class _StudentHomeView extends StatelessWidget {
  const _StudentHomeView();

  void _reload(BuildContext context) {
    final session = context.read<AuthCubit>().state.session;
    context.read<StudyHomeCubit>().refresh(
          userId: session?.userId ?? '',
          fullName: session?.fullName ?? '',
        );
  }

  /// Pozitif dil ilkesine (ogrenci_ux §2) uygun, güne özel motivasyon metni.
  String _motivationSubtitle(StudyDashboard d) {
    if (d.todayGoalMinutes <= 0) {
      return 'Bugün de bir adım daha atalım.';
    }
    if (d.todayGoalMet) {
      return 'Bugün hedefini tamamladın, harikasın! 🎉';
    }
    final remaining = d.todayGoalMinutes - d.todayEffectiveMinutes;
    if (remaining <= 0) {
      return 'Bugün de bir adım daha atalım.';
    }
    return 'Bugün ${StudyFormat.minutes(remaining)} daha çalışırsan '
        'hedefini tamamlayacaksın.';
  }

  @override
  Widget build(BuildContext context) {
    final session = context.read<AuthCubit>().state.session;
    final firstName = (session?.fullName.trim().isNotEmpty ?? false)
        ? session!.fullName.trim().split(' ').first
        : null;
    final greeting = firstName != null ? 'Merhaba, $firstName 👋' : 'Merhaba 👋';

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        bottom: false,
        child: BlocBuilder<StudyHomeCubit, StudyHomeState>(
          builder: (context, state) {
            if (state.status == StudyHomeStatus.loading ||
                state.status == StudyHomeStatus.initial) {
              return const LoadingStateView(
                message: 'Çalışma panosu yükleniyor...',
              );
            }
            if (state.status == StudyHomeStatus.error) {
              return ErrorStateView(
                message: state.errorMessage ?? 'Bir hata oluştu.',
                onRetry: () => _reload(context),
              );
            }
            final d = state.dashboard!;
            final studentId = state.studentId!;
            return RefreshIndicator(
              color: AppColors.primary,
              onRefresh: () async => _reload(context),
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
                children: <Widget>[
                  // 1) Karşılama — pozitif, güne özel motivasyon (ux §5)
                  AppPageHeader(
                    title: greeting,
                    subtitle: _motivationSubtitle(d),
                  ),
                  const SizedBox(height: 20),
                  // 2+3) Bugünkü çalışma + streak — yan yana eşit özet kartları
                  // (ux §3 bilgi hiyerarşisi + §5/§12 motivasyon).
                  SizedBox(
                    height: 156,
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: <Widget>[
                        Expanded(
                          child: _TodayGoalCard(
                            todayMinutes: d.todayEffectiveMinutes,
                            goalMinutes: d.todayGoalMinutes,
                            met: d.todayGoalMet,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: _StreakBanner(
                            currentStreakDays: d.currentStreakDays,
                            studiedToday: d.todayEffectiveMinutes > 0,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 24),
                  // Ç-06) Bugünün planı — bugünkü occurrence'lar; dokununca sayaç derse bağlı başlar.
                  _TodayPlanCard(studentId: studentId),
                  // 4) Hızlı işlemler — özet kartlarının hemen altında (ux §14)
                  const _SectionHeader(title: 'Hızlı işlemler'),
                  const SizedBox(height: 12),
                  _ActionGrid(studentId: studentId),
                  // Yaklaşan Ders — varsa (ux §5); ders yoksa kendini gizler
                  _UpcomingLessonCard(studentId: studentId),
                  const SizedBox(height: 24),
                  // 6) İlerleme (ux §3)
                  const _SectionHeader(title: 'İlerlemen'),
                  const SizedBox(height: 12),
                  Row(
                    children: <Widget>[
                      Expanded(
                        child: _StatTile(
                          icon: Icons.timelapse_rounded,
                          color: AppColors.accentTeal,
                          value: StudyFormat.minutes(d.weekEffectiveMinutes),
                          label: 'Bu hafta',
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: _StatTile(
                          icon: Icons.emoji_events_rounded,
                          color: AppColors.accentGreen,
                          value: '${d.longestStreakDays} gün',
                          label: 'Rekor seri',
                        ),
                      ),
                    ],
                  ),
                  // 7) Geçmiş — hiçbir zaman ilk odakta olmaz (ux §3)
                  if (d.lastTest != null) ...<Widget>[
                    const SizedBox(height: 24),
                    const _SectionHeader(title: 'Son deneme'),
                    const SizedBox(height: 12),
                    _LastTestCard(
                      subject: d.lastTest!.subject,
                      net: d.lastTest!.net,
                      testName: d.lastTest!.testName,
                    ),
                  ],
                  if (d.recentSessions.isNotEmpty) ...<Widget>[
                    const SizedBox(height: 24),
                    const _SectionHeader(title: 'Son çalışmalar'),
                    const SizedBox(height: 12),
                    ...d.recentSessions.take(5).map(
                          (s) => _SessionTile(
                            subject: s.subject,
                            topic: s.topic,
                            minutes: s.effectiveMinutes,
                          ),
                        ),
                  ],
                ],
              ),
            );
          },
        ),
      ),
      bottomNavigationBar: const StudentBottomNav(current: StudentNavTab.home),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Text(
      title,
      style: Theme.of(context).textTheme.titleLarge?.copyWith(
        color: AppColors.textPrimary,
        fontWeight: FontWeight.w800,
      ),
    );
  }
}

class _TodayGoalCard extends StatelessWidget {
  const _TodayGoalCard({
    required this.todayMinutes,
    required this.goalMinutes,
    required this.met,
  });

  final int todayMinutes;
  final int goalMinutes;
  final bool met;

  @override
  Widget build(BuildContext context) {
    final hasGoal = goalMinutes > 0;
    final progress =
        hasGoal ? (todayMinutes / goalMinutes).clamp(0.0, 1.0) : 0.0;
    final percent = (progress * 100).round();
    final String hint;
    if (!hasGoal) {
      hint = 'Hedef belirle';
    } else if (met) {
      hint = 'Hedef tamam 🎉';
    } else {
      hint = '%$percent · Hedef ${StudyFormat.minutes(goalMinutes)}';
    }
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [AppColors.primary, AppColors.secondary],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(20),
        boxShadow: AppShadows.soft,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.16),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: const Icon(Icons.track_changes_rounded,
                    color: Colors.white, size: 20),
              ),
              const SizedBox(width: 8),
              const Expanded(
                child: Text('Bugünkü çalışma',
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                        color: Colors.white,
                        fontSize: 12.5,
                        height: 1.15,
                        fontWeight: FontWeight.w700)),
              ),
            ],
          ),
          const Spacer(),
          Text(
            StudyFormat.minutes(todayMinutes),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
                color: Colors.white, fontSize: 24, fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 2),
          Text(hint,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(color: Colors.white70, fontSize: 11.5)),
          const SizedBox(height: 10),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: progress,
              minHeight: 7,
              backgroundColor: Colors.white24,
              valueColor: const AlwaysStoppedAnimation(Colors.white),
            ),
          ),
        ],
      ),
    );
  }
}

/// Öğrencinin bir sonraki dersini gösterir. Güvenli öğrenci-kapsamlı endpoint'ten
/// (IStudentDirectory ile IDOR korumalı) çeker; ders yoksa veya hata olursa
/// kendini gizler (ana sayfa akışını bozmaz).
class _UpcomingLessonCard extends StatefulWidget {
  const _UpcomingLessonCard({required this.studentId});

  final String studentId;

  @override
  State<_UpcomingLessonCard> createState() => _UpcomingLessonCardState();
}

class _UpcomingLessonCardState extends State<_UpcomingLessonCard> {
  LessonSchedule? _next;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final lessons = await injector<SchedulingRepository>()
          .listStudentLessons(studentId: widget.studentId);
      final now = DateTime.now().toUtc();
      final upcoming = lessons
          .where((LessonSchedule l) =>
              l.status != 'Cancelled' && l.startAtUtc.isAfter(now))
          .toList()
        ..sort((LessonSchedule a, LessonSchedule b) =>
            a.startAtUtc.compareTo(b.startAtUtc));
      if (!mounted) return;
      setState(() => _next = upcoming.isEmpty ? null : upcoming.first);
    } on Object {
      // Sessizce gizle — yaklaşan ders ana sayfa için opsiyonel bir bölüm.
    }
  }

  @override
  Widget build(BuildContext context) {
    final LessonSchedule? lesson = _next;
    if (lesson == null) return const SizedBox.shrink();
    final bool isOnline = lesson.lessonFormat == 'Online';
    return Column(
      children: <Widget>[
        const SizedBox(height: 24),
        const _SectionHeader(title: 'Yaklaşan ders'),
        const SizedBox(height: 12),
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: AppColors.skyBorder),
            boxShadow: AppShadows.soft,
          ),
          child: Row(
            children: <Widget>[
              Container(
                width: 46,
                height: 46,
                decoration: BoxDecoration(
                  color: AppColors.primaryLight,
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Icon(
                    isOnline
                        ? Icons.videocam_rounded
                        : Icons.location_on_rounded,
                    color: AppColors.primary),
              ),
              const SizedBox(width: 12),
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
                    Text(StudyFormat.date(lesson.startAtUtc),
                        style: const TextStyle(
                            color: AppColors.textSecondary, fontSize: 12)),
                  ],
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// Ç-06: "Bugünün planı" — bugünkü takvim occurrence'ları. Her satıra dokununca çalışma sayacı
/// o derse bağlı (lessonId) başlar; çalışılmış occurrence ✓ ile işaretlenir. Plan yoksa gizlenir.
class _TodayPlanCard extends StatefulWidget {
  const _TodayPlanCard({required this.studentId});

  final String studentId;

  @override
  State<_TodayPlanCard> createState() => _TodayPlanCardState();
}

class _TodayPlanCardState extends State<_TodayPlanCard> {
  List<CalendarOccurrence> _today = const <CalendarOccurrence>[];
  bool _loaded = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final now = DateTime.now();
      final startUtc = DateTime.utc(now.year, now.month, now.day);
      final endUtc = startUtc.add(const Duration(days: 1));
      final occ = await injector<SchedulingRepository>().getStudentCalendar(
        studentId: widget.studentId,
        startAtUtc: startUtc,
        endAtUtc: endUtc,
      );
      final sorted = <CalendarOccurrence>[...occ]
        ..sort((a, b) => a.startAtUtc.compareTo(b.startAtUtc));
      if (!mounted) return;
      setState(() {
        _today = sorted;
        _loaded = true;
      });
    } on Object {
      if (!mounted) return;
      setState(() => _loaded = true);
    }
  }

  void _startFromPlan(CalendarOccurrence occ) {
    final params = <String, String>{
      'studentId': widget.studentId,
      'lessonId': occ.entryId,
      'subject': occ.subject,
      if ((occ.topic ?? '').isNotEmpty) 'topic': occ.topic!,
    };
    final query = params.entries
        .map((e) => '${e.key}=${Uri.encodeComponent(e.value)}')
        .join('&');
    context.push('/study/timer?$query');
  }

  @override
  Widget build(BuildContext context) {
    if (!_loaded || _today.isEmpty) return const SizedBox.shrink();
    return Column(
      children: <Widget>[
        const _SectionHeader(title: 'Bugünün planı'),
        const SizedBox(height: 12),
        Container(
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: AppColors.border),
            boxShadow: AppShadows.soft,
          ),
          child: Column(
            children: <Widget>[
              for (var i = 0; i < _today.length; i++) ...<Widget>[
                if (i > 0)
                  const Divider(height: 1, color: AppColors.divider),
                _TodayPlanRow(
                  occ: _today[i],
                  onTap: () => _startFromPlan(_today[i]),
                ),
              ],
            ],
          ),
        ),
        const SizedBox(height: 24),
      ],
    );
  }
}

class _TodayPlanRow extends StatelessWidget {
  const _TodayPlanRow({required this.occ, required this.onTap});

  final CalendarOccurrence occ;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final String title = (occ.topic ?? '').isEmpty
        ? occ.subject
        : '${occ.subject} · ${occ.topic}';
    final DateTime local = occ.startAtUtc.toLocal();
    final String hhmm =
        '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
    return InkWell(
      onTap: occ.completed ? null : onTap,
      borderRadius: BorderRadius.circular(18),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        child: Row(
          children: <Widget>[
            Text(
              hhmm,
              style: const TextStyle(
                fontWeight: FontWeight.w800,
                color: AppColors.textSecondary,
                fontSize: 12,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
            ),
            const SizedBox(width: 8),
            if (occ.completed)
              const Icon(Icons.check_circle_rounded,
                  color: AppColors.accentGreen, size: 22)
            else
              const Icon(Icons.play_circle_fill_rounded,
                  color: AppColors.primary, size: 26),
          ],
        ),
      ),
    );
  }
}

class _StreakBanner extends StatelessWidget {
  const _StreakBanner({
    required this.currentStreakDays,
    required this.studiedToday,
  });

  final int currentStreakDays;
  final bool studiedToday;

  @override
  Widget build(BuildContext context) {
    final hasStreak = currentStreakDays > 0;
    final subtitle = hasStreak
        ? (studiedToday ? 'Bugün de tamam!' : 'Bugün çalış, bozma')
        : 'İlk günü yakala';
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            AppColors.amber.withValues(alpha: 0.18),
            AppColors.accentOrange.withValues(alpha: 0.08),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(20),
        border:
            Border.all(color: AppColors.accentOrange.withValues(alpha: 0.20)),
        boxShadow: AppShadows.soft,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 38,
                height: 38,
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: [AppColors.accentOrange, AppColors.amber],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: const Icon(Icons.local_fire_department_rounded,
                    color: Colors.white, size: 20),
              ),
              const SizedBox(width: 8),
              const Expanded(
                child: Text('Günlük seri',
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 12.5,
                        height: 1.15,
                        fontWeight: FontWeight.w700)),
              ),
            ],
          ),
          const Spacer(),
          Text(
            hasStreak ? '$currentStreakDays gün' : '0 gün',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
                color: AppColors.accentOrange,
                fontSize: 24,
                fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 2),
          Text(subtitle,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                  color: AppColors.textSecondary, fontSize: 11.5)),
        ],
      ),
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({
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
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 10),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      child: Column(
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(icon, color: color, size: 22),
          ),
          const SizedBox(height: 8),
          Text(value,
              style: const TextStyle(
                  fontWeight: FontWeight.w800,
                  color: AppColors.textPrimary,
                  fontSize: 15)),
          const SizedBox(height: 2),
          Text(label,
              textAlign: TextAlign.center,
              style: const TextStyle(
                  color: AppColors.textSecondary, fontSize: 11)),
        ],
      ),
    );
  }
}

class _ActionGrid extends StatelessWidget {
  const _ActionGrid({required this.studentId});

  final String studentId;

  void _go(BuildContext context, String route) =>
      context.push('$route?studentId=$studentId');

  @override
  Widget build(BuildContext context) {
    // İkincil kısayollar — birincil eylem (Kronometre) yukarıdaki hero CTA'da.
    // 4 öğe temiz bir 2×2 ızgaraya oturur; artakalan tek kart sorunu olmaz.
    final secondary = <_ActionItem>[
      _ActionItem(
          'Deneme Gir', Icons.edit_note_rounded, AppColors.accentBlue, '/study/test'),
      _ActionItem('Hedefler', Icons.flag_rounded, AppColors.accentGreen, '/study/goals'),
      _ActionItem('Geçmiş', Icons.history_rounded, AppColors.accentTeal, '/study/history'),
      _ActionItem('Rozetler', Icons.emoji_events_rounded, AppColors.accentOrange,
          '/study/achievements'),
    ];
    final halfWidth = (MediaQuery.of(context).size.width - 44) / 2;
    return Column(
      children: <Widget>[
        // Birincil eylem: bilgi hiyerarşisinde öne çıkan tek CTA (ux §14).
        _PrimaryActionCard(
          icon: Icons.timer_rounded,
          label: 'Kronometre',
          subtitle: 'Çalışma süreni tut',
          onTap: () => _go(context, '/study/timer'),
        ),
        const SizedBox(height: 12),
        // İkincil eylemler: öğretmen panosuyla aynı yatay kart stili, 2×2.
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: <Widget>[
            for (final a in secondary)
              SizedBox(
                width: halfWidth,
                child: _ActionTile(
                  action: a,
                  onTap: () => _go(context, a.route),
                ),
              ),
          ],
        ),
      ],
    );
  }
}

/// Hızlı işlemler bölümünün birincil CTA'sı: tam genişlik, dolu primary zemin.
/// "Kaldığın yerden devam et" kartıyla ikiz görünmemesi için ayrı ikon (timer)
/// ve metin kullanır.
class _PrimaryActionCard extends StatelessWidget {
  const _PrimaryActionCard({
    required this.icon,
    required this.label,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: AppColors.primary,
          borderRadius: BorderRadius.circular(22),
          boxShadow: AppShadows.soft,
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 48,
              height: 48,
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.16),
                borderRadius: BorderRadius.circular(15),
              ),
              child: Icon(icon, color: Colors.white, size: 26),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    label,
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                          color: Colors.white,
                          fontWeight: FontWeight.w800,
                        ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: const TextStyle(color: Colors.white70, fontSize: 12),
                  ),
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

/// Öğretmen panosundaki hızlı işlem kartlarıyla aynı düzen: yatay satır,
/// solda yumuşak ikon, yanında etiket (2 sütunlu Wrap).
class _ActionTile extends StatelessWidget {
  const _ActionTile({required this.action, required this.onTap});

  final _ActionItem action;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(22),
          border: Border.all(color: AppColors.skyBorder),
          boxShadow: AppShadows.soft,
        ),
        child: Row(
          children: <Widget>[
            Container(
              width: 44,
              height: 44,
              decoration: BoxDecoration(
                color: action.color.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(14),
              ),
              child: Icon(action.icon, color: action.color, size: 22),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                action.label,
                style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w700,
                    ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ActionItem {
  const _ActionItem(this.label, this.icon, this.color, this.route);
  final String label;
  final IconData icon;
  final Color color;
  final String route;
}

class _LastTestCard extends StatelessWidget {
  const _LastTestCard({required this.subject, required this.net, this.testName});

  final String subject;
  final double net;
  final String? testName;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      child: Row(
        children: [
          Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: BorderRadius.circular(14),
            ),
            child: const Icon(Icons.quiz_rounded, color: AppColors.primary),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(testName ?? subject,
                    style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary)),
                Text(subject,
                    style: const TextStyle(
                        color: AppColors.textSecondary, fontSize: 12)),
              ],
            ),
          ),
          Text('${StudyFormat.net(net)} net',
              style: const TextStyle(
                  fontWeight: FontWeight.w800, color: AppColors.primary)),
        ],
      ),
    );
  }
}

class _SessionTile extends StatelessWidget {
  const _SessionTile({required this.subject, required this.minutes, this.topic});

  final String subject;
  final String? topic;
  final int minutes;

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
        children: [
          const Icon(Icons.menu_book_rounded,
              color: AppColors.textSecondary, size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Text(topic == null ? subject : '$subject · $topic',
                style: const TextStyle(color: AppColors.textPrimary)),
          ),
          Text(StudyFormat.minutes(minutes),
              style: const TextStyle(
                  fontWeight: FontWeight.w700, color: AppColors.textSecondary)),
        ],
      ),
    );
  }
}
