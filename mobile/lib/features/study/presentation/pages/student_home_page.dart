import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/assignments/domain/assignment_contracts.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/scheduling/domain/scheduling_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_home_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_home_state.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/home/dashboard_stats.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/study_tab_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// Dekoratif ışıma dairesi — birincil CTA'nın gradient zemininde derinlik katar.
Widget _glowCircle(double size, double alpha) => Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: Colors.white.withValues(alpha: alpha),
      ),
    );

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
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 28),
                children: <Widget>[
                  // 1) Karşılama — pozitif, güne özel motivasyon (ux §5)
                  AppPageHeader(
                    title: greeting,
                    subtitle: _motivationSubtitle(d),
                  ),
                  const SizedBox(height: 18),
                  // 2) İstatistik ızgarası — seri + bugünkü çalışma + haftalık demo
                  // ödev/ders özetleri (ux §3 hiyerarşi).
                  _StatGrid(dashboard: d),
                  const SizedBox(height: 18),
                  // 3) Birincil eylem: sayacı başlat (0 tık ilkesi).
                  _PrimaryActionCard(
                    icon: Icons.timer_rounded,
                    label: 'Çalışmaya Başla',
                    subtitle: 'Kronometreyi başlat, serini büyüt',
                    onTap: () =>
                        context.push('/study/timer?studentId=$studentId'),
                  ),
                  const SizedBox(height: 24),
                  // 4) Hızlı erişim — Derslerim/Ödevlerim/Hedeflerim/Performansım.
                  const StudySectionHeader(title: 'Hızlı erişim'),
                  const SizedBox(height: 12),
                  _QuickAccessGrid(studentId: studentId),
                  // 5) Yaklaşanlar — dersler + ödevler.
                  _UpcomingLessonCard(studentId: studentId),
                  _UpcomingAssignmentsCard(studentId: studentId),
                ],
              ),
            );
          },
        ),
      ),
      bottomNavigationBar: const StudentBottomNav(current: StudentNavTab.work),
    );
  }
}

/// 2×2 istatistik ızgarası: seri, bugünkü çalışma (+ hedef ilerleme çubuğu) ve
/// backend'i henüz olmayan haftalık ödev/ders özetleri (demo rozetli).
class _StatGrid extends StatelessWidget {
  const _StatGrid({required this.dashboard});

  final StudyDashboard dashboard;

  @override
  Widget build(BuildContext context) {
    final hw = DashboardStats.demoWeeklyHomework();
    final ls = DashboardStats.demoWeeklyLessons();
    return GridView.count(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      crossAxisCount: 2,
      childAspectRatio: 1.35,
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      children: <Widget>[
        StudyStatTile(
          icon: Icons.local_fire_department_rounded,
          color: AppColors.accentOrange,
          value: '${dashboard.currentStreakDays}',
          label: 'Gün seri',
        ),
        _TodayMinutesTile(dashboard: dashboard),
        _DemoStatTile(
          icon: Icons.assignment_turned_in_rounded,
          color: AppColors.accentBlue,
          value: '${hw.done}/${hw.given}',
          label: 'Ödev (hafta)',
        ),
        _DemoStatTile(
          icon: Icons.menu_book_rounded,
          color: AppColors.accentTeal,
          value: '${ls.done}/${ls.given}',
          label: 'Ders (hafta)',
        ),
      ],
    );
  }
}

/// Bugünkü çalışma süresi + günlük hedefe göre ilerleme çubuğu.
class _TodayMinutesTile extends StatelessWidget {
  const _TodayMinutesTile({required this.dashboard});

  final StudyDashboard dashboard;

  @override
  Widget build(BuildContext context) {
    final int goal = dashboard.todayGoalMinutes;
    final int today = dashboard.todayEffectiveMinutes;
    final double progress = goal == 0 ? 0.0 : today / goal;
    return StudyCard(
      padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 10),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: <Widget>[
          StudyIconChip(
              icon: Icons.timer_rounded, color: AppColors.primary, size: 32),
          const SizedBox(height: 4),
          Text(StudyFormat.minutes(today),
              textAlign: TextAlign.center,
              style: const TextStyle(
                  fontWeight: FontWeight.w800,
                  color: AppColors.textPrimary,
                  fontSize: 15)),
          const SizedBox(height: 1),
          Text('Bugün',
              textAlign: TextAlign.center,
              style:
                  const TextStyle(color: AppColors.textSecondary, fontSize: 11)),
          const SizedBox(height: 6),
          StudyProgressBar(
            value: progress,
            trailingLabel: 'Hedef $goal dk',
          ),
        ],
      ),
    );
  }
}

/// Backend'i henüz olmayan haftalık istatistik kutusu — sağ üstte "Demo" rozeti.
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

/// Hızlı erişim ızgarası: Derslerim · Ödevlerim · Hedeflerim · Performansım.
class _QuickAccessGrid extends StatelessWidget {
  const _QuickAccessGrid({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context) {
    return GridView.count(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      crossAxisCount: 2,
      childAspectRatio: 1.6,
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      children: <Widget>[
        StudyQuickAccessCard(
          icon: Icons.menu_book_rounded,
          color: AppColors.primary,
          label: 'Derslerim',
          onTap: () => context.go('/student/lessons'),
        ),
        StudyQuickAccessCard(
          icon: Icons.assignment_rounded,
          color: AppColors.accentBlue,
          label: 'Ödevlerim',
          onTap: () => context.push('/student/assignments?studentId=$studentId'),
        ),
        StudyQuickAccessCard(
          icon: Icons.flag_rounded,
          color: AppColors.accentGreen,
          label: 'Hedeflerim',
          onTap: () =>
              context.push('/student/goals-overview?studentId=$studentId'),
        ),
        StudyQuickAccessCard(
          icon: Icons.insights_rounded,
          color: AppColors.accentOrange,
          label: 'Performansım',
          onTap: () => context.go('/student/performance'),
        ),
      ],
    );
  }
}

/// Hızlı işlemler bölümünün birincil CTA'sı: tam genişlik, gradient primary zemin,
/// hafif ışıltılı gölge.
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
    return StudyPressable(
      onTap: onTap,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(24),
        child: Container(
          padding: const EdgeInsets.all(18),
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              colors: <Color>[AppColors.primary, Color(0xFF0E4A86), AppColors.secondary],
              begin: Alignment.centerLeft,
              end: Alignment.centerRight,
            ),
            boxShadow: <BoxShadow>[
              BoxShadow(
                color: AppColors.primary.withValues(alpha: 0.34),
                blurRadius: 22,
                offset: const Offset(0, 12),
              ),
            ],
          ),
          child: Stack(
            children: <Widget>[
              Positioned(
                top: -30,
                right: -10,
                child: _glowCircle(90, 0.10),
              ),
              Row(
                children: <Widget>[
                  Container(
                    width: 50,
                    height: 50,
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.18),
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(
                          color: Colors.white.withValues(alpha: 0.18)),
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
                          style: Theme.of(context)
                              .textTheme
                              .titleMedium
                              ?.copyWith(
                                color: Colors.white,
                                fontWeight: FontWeight.w800,
                              ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          subtitle,
                          style: const TextStyle(
                              color: Colors.white70, fontSize: 12),
                        ),
                      ],
                    ),
                  ),
                  Container(
                    width: 32,
                    height: 32,
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.16),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Icon(Icons.chevron_right_rounded,
                        color: Colors.white, size: 22),
                  ),
                ],
              ),
            ],
          ),
        ),
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
        const StudySectionHeader(title: 'Yaklaşan dersler'),
        const SizedBox(height: 12),
        StudyCard(
          child: Row(
            children: <Widget>[
              StudyIconChip(
                icon: isOnline
                    ? Icons.videocam_rounded
                    : Icons.location_on_rounded,
                color: AppColors.primary,
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
                    const SizedBox(height: 2),
                    Text(StudyFormat.date(lesson.startAtUtc),
                        style: const TextStyle(
                            color: AppColors.textSecondary, fontSize: 12)),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right_rounded,
                  color: AppColors.textMuted),
            ],
          ),
        ),
      ],
    );
  }
}

/// Teslim tarihi yaklaşan ödevleri gösterir (`AssignmentRepository.listByStudent`).
/// Veri yoksa veya çekilemezse "yakında" kartı + Demo rozeti gösterilir; bu
/// bölümün özel bir "yaklaşan ödev" sorgusu henüz backend'de yoktur.
class _UpcomingAssignmentsCard extends StatefulWidget {
  const _UpcomingAssignmentsCard({required this.studentId});

  final String studentId;

  @override
  State<_UpcomingAssignmentsCard> createState() =>
      _UpcomingAssignmentsCardState();
}

class _UpcomingAssignmentsCardState extends State<_UpcomingAssignmentsCard> {
  List<AssignmentItem> _upcoming = const <AssignmentItem>[];
  bool _loaded = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final items =
          await injector<AssignmentRepository>().listByStudent(widget.studentId);
      final now = DateTime.now().toUtc();
      final upcoming = items
          .where((AssignmentItem a) =>
              a.status != 'Completed' &&
              a.status != 'Cancelled' &&
              (a.dueDateUtc == null || a.dueDateUtc!.isAfter(now)))
          .toList()
        ..sort((AssignmentItem a, AssignmentItem b) {
          final ad = a.dueDateUtc;
          final bd = b.dueDateUtc;
          if (ad == null && bd == null) return 0;
          if (ad == null) return 1;
          if (bd == null) return -1;
          return ad.compareTo(bd);
        });
      if (!mounted) return;
      setState(() {
        _upcoming = upcoming.take(3).toList();
        _loaded = true;
      });
    } on Object {
      if (!mounted) return;
      setState(() => _loaded = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (!_loaded) return const SizedBox.shrink();
    if (_upcoming.isEmpty) {
      return Column(
        children: <Widget>[
          const SizedBox(height: 24),
          Row(
            children: <Widget>[
              const Expanded(
                  child: StudySectionHeader(title: 'Yaklaşan ödevler')),
              const StudyDemoBadge(),
            ],
          ),
          const SizedBox(height: 12),
          const StudyComingSoonCard(
            icon: Icons.assignment_late_rounded,
            title: 'Ödev takibi',
            message: 'Teslim tarihi yaklaşan ödevlerin burada listelenecek.',
          ),
        ],
      );
    }
    return Column(
      children: <Widget>[
        const SizedBox(height: 24),
        const StudySectionHeader(title: 'Yaklaşan ödevler'),
        const SizedBox(height: 12),
        StudyCard(
          padding: EdgeInsets.zero,
          child: Column(
            children: <Widget>[
              for (var i = 0; i < _upcoming.length; i++) ...<Widget>[
                if (i > 0)
                  const Divider(
                      height: 1,
                      indent: 16,
                      endIndent: 16,
                      color: Color(0xFFEDF1F7)),
                _AssignmentRow(item: _upcoming[i]),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class _AssignmentRow extends StatelessWidget {
  const _AssignmentRow({required this.item});

  final AssignmentItem item;

  @override
  Widget build(BuildContext context) {
    final due = item.dueDateUtc;
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      child: Row(
        children: <Widget>[
          StudyIconChip(
              icon: Icons.assignment_rounded,
              color: AppColors.accentBlue,
              size: 38),
          const SizedBox(width: 12),
          Expanded(
            child: Text(item.title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    color: AppColors.textPrimary)),
          ),
          const SizedBox(width: 8),
          Text(
            due == null ? 'Süresiz' : StudyFormat.date(due),
            style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 12,
                fontWeight: FontWeight.w600),
          ),
        ],
      ),
    );
  }
}
