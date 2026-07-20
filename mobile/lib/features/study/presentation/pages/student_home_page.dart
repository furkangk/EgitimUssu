import 'dart:math' as math;

import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
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

// ── Premium ortak stil yardımcıları ────────────────────────────────────────
// Kart yüzeyi düz beyaz yerine hafif dikey cam gradyanı + katmanlı gölge
// (yakın "key" + geniş "ambient") ile derinlik kazanır; ikonlar dolu gradient
// madalyonlar olarak renkli ışıma gölgesiyle çizilir. Tümü token tabanlıdır.

/// Premium kart dekorasyonu — cam gradyanlı beyaz yüzey + iki katmanlı yumuşak gölge.
BoxDecoration _softCard({double radius = 22}) => BoxDecoration(
      gradient: const LinearGradient(
        colors: <Color>[Colors.white, Color(0xFFF8FAFE)],
        begin: Alignment.topCenter,
        end: Alignment.bottomCenter,
      ),
      borderRadius: BorderRadius.circular(radius),
      border: Border.all(color: const Color(0xFFEDF1F7)),
      boxShadow: const <BoxShadow>[
        BoxShadow(color: Color(0x0D101828), blurRadius: 2, offset: Offset(0, 1)),
        BoxShadow(color: Color(0x14082B4F), blurRadius: 24, offset: Offset(0, 14)),
      ],
    );

/// Dolu gradient ikon madalyonu — renk tonundan koyuya degrade + renkli ışıma
/// gölgesi. Soluk tonlu düz kutulara göre çok daha premium okunur.
class _IconChip extends StatelessWidget {
  const _IconChip({
    required this.icon,
    required this.color,
    this.size = 46,
    this.iconSize = 22,
  });

  final IconData icon;
  final Color color;
  final double size;
  final double iconSize;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: <Color>[
            color,
            Color.lerp(color, const Color(0xFF000000), 0.20) ?? color,
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(size * 0.31),
        boxShadow: <BoxShadow>[
          BoxShadow(
            color: color.withValues(alpha: 0.35),
            blurRadius: 12,
            offset: const Offset(0, 6),
          ),
        ],
      ),
      child: Icon(icon, color: Colors.white, size: iconSize),
    );
  }
}

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
                  // 2+3) Premium hero: bugünkü hedef halkası + günlük seri rozeti
                  // tek şık gradient blokta (ux §3 hiyerarşi + §5/§12 motivasyon).
                  _HeroSummary(
                    todayMinutes: d.todayEffectiveMinutes,
                    goalMinutes: d.todayGoalMinutes,
                    met: d.todayGoalMet,
                    currentStreakDays: d.currentStreakDays,
                    studiedToday: d.todayEffectiveMinutes > 0,
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
      bottomNavigationBar: const StudentBottomNav(current: StudentNavTab.work),
    );
  }
}

/// Basılıyken hafifçe küçülen (scale 0.97) dokunma geri bildirimi — premium
/// his için birincil CTA, plan satırları ve hızlı işlem kutularında kullanılır
/// (`animations.md` §4 mikro-etkileşim standardı).
class _Pressable extends StatefulWidget {
  const _Pressable({required this.child, required this.onTap});

  final Widget child;
  final VoidCallback? onTap;

  @override
  State<_Pressable> createState() => _PressableState();
}

class _PressableState extends State<_Pressable> {
  bool _down = false;

  @override
  Widget build(BuildContext context) {
    final bool enabled = widget.onTap != null;
    return GestureDetector(
      onTapDown: enabled ? (_) => setState(() => _down = true) : null,
      onTapUp: enabled ? (_) => setState(() => _down = false) : null,
      onTapCancel: enabled ? () => setState(() => _down = false) : null,
      onTap: widget.onTap,
      child: AnimatedScale(
        scale: _down ? 0.97 : 1.0,
        duration: const Duration(milliseconds: 120),
        curve: Curves.easeOut,
        child: widget.child,
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        // İnce vurgu çubuğu — bölüm başlıklarına premium ritim katar.
        Container(
          width: 4,
          height: 18,
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              colors: <Color>[AppColors.primary, AppColors.secondary],
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
            ),
            borderRadius: BorderRadius.circular(999),
          ),
        ),
        const SizedBox(width: 10),
        Text(
          title,
          style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w800,
              ),
        ),
      ],
    );
  }
}

/// Ana sayfanın premium açılış bloğu: sol tarafta bugünkü hedefi gösteren dairesel
/// ilerleme halkası (ortada bugünkü süre), sağda başlık/ipucu ve buzlu cam seri
/// rozeti. Arka planda yumuşak ışıma daireleriyle derinlik kazanır. İki ayrı düz
/// kartın yerini alır; veri ve davranış aynıdır.
class _HeroSummary extends StatelessWidget {
  const _HeroSummary({
    required this.todayMinutes,
    required this.goalMinutes,
    required this.met,
    required this.currentStreakDays,
    required this.studiedToday,
  });

  final int todayMinutes;
  final int goalMinutes;
  final bool met;
  final int currentStreakDays;
  final bool studiedToday;

  @override
  Widget build(BuildContext context) {
    final bool hasGoal = goalMinutes > 0;
    final double progress =
        hasGoal ? (todayMinutes / goalMinutes).clamp(0.0, 1.0) : 0.0;
    final int percent = (progress * 100).round();
    final String hint;
    if (!hasGoal) {
      hint = 'Günlük hedef belirle';
    } else if (met) {
      hint = 'Günlük hedef tamam 🎉';
    } else {
      hint = '%$percent tamam · Hedef ${StudyFormat.minutes(goalMinutes)}';
    }

    return ClipRRect(
      borderRadius: BorderRadius.circular(28),
      child: Container(
        decoration: BoxDecoration(
          gradient: const LinearGradient(
            colors: <Color>[AppColors.primary, Color(0xFF0E4A86), AppColors.secondary],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          ),
          boxShadow: const <BoxShadow>[
            BoxShadow(
              color: Color(0x3D082B4F),
              blurRadius: 30,
              offset: Offset(0, 16),
            ),
          ],
        ),
        child: Stack(
          children: <Widget>[
            // Dekoratif ışıma daireleri — cam/derinlik hissi.
            Positioned(
              top: -34,
              right: -18,
              child: _glowCircle(130, 0.10),
            ),
            Positioned(
              bottom: -46,
              right: 60,
              child: _glowCircle(96, 0.06),
            ),
            Padding(
              padding: const EdgeInsets.all(20),
              child: Row(
                children: <Widget>[
                  _ProgressRing(
                    progress: progress,
                    centerValue: StudyFormat.minutes(todayMinutes),
                    centerLabel: 'bugün',
                  ),
                  const SizedBox(width: 20),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: <Widget>[
                        const Text(
                          'Bugünkü çalışma',
                          style: TextStyle(
                            color: Colors.white,
                            fontSize: 16,
                            fontWeight: FontWeight.w800,
                            letterSpacing: 0.2,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          hint,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            color: Colors.white70,
                            fontSize: 12.5,
                            height: 1.25,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                        const SizedBox(height: 14),
                        _StreakPill(
                          currentStreakDays: currentStreakDays,
                          studiedToday: studiedToday,
                        ),
                      ],
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

  static Widget _glowCircle(double size, double alpha) => Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: Colors.white.withValues(alpha: alpha),
        ),
      );
}

/// Hero içindeki buzlu cam seri rozeti — 🔥 ikon + gün sayısı + kısa durum.
class _StreakPill extends StatelessWidget {
  const _StreakPill({
    required this.currentStreakDays,
    required this.studiedToday,
  });

  final int currentStreakDays;
  final bool studiedToday;

  @override
  Widget build(BuildContext context) {
    final bool hasStreak = currentStreakDays > 0;
    final String text = hasStreak
        ? '$currentStreakDays gün seri · '
            '${studiedToday ? 'bugün de tamam' : 'bugün çalış, bozma'}'
        : 'Seriye bugün başla';
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.16),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: Colors.white.withValues(alpha: 0.22)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(Icons.local_fire_department_rounded,
              color: AppColors.amber, size: 18),
          const SizedBox(width: 6),
          Flexible(
            child: Text(
              text,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 11.5,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Bugünkü hedef ilerlemesini gösteren dairesel halka; ortasında bugünkü süre.
class _ProgressRing extends StatelessWidget {
  const _ProgressRing({
    required this.progress,
    required this.centerValue,
    required this.centerLabel,
  });

  final double progress;
  final String centerValue;
  final String centerLabel;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 96,
      height: 96,
      child: Stack(
        alignment: Alignment.center,
        children: <Widget>[
          // Halka merkezinde yumuşak iç ışıma — metrik parlıyormuş hissi.
          Container(
            width: 66,
            height: 66,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: Colors.white.withValues(alpha: 0.08),
            ),
          ),
          CustomPaint(
            size: const Size(96, 96),
            painter: _RingPainter(
              progress: progress,
              track: Colors.white.withValues(alpha: 0.20),
              fill: Colors.white,
            ),
          ),
          Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 10),
                child: FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Text(
                    centerValue,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 20,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 1),
              Text(
                centerLabel,
                style: const TextStyle(
                  color: Colors.white70,
                  fontSize: 10.5,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _RingPainter extends CustomPainter {
  _RingPainter({
    required this.progress,
    required this.track,
    required this.fill,
  });

  final double progress;
  final Color track;
  final Color fill;
  static const double stroke = 9;

  @override
  void paint(Canvas canvas, Size size) {
    final Offset center = size.center(Offset.zero);
    final double radius = (math.min(size.width, size.height) - stroke) / 2;
    final Paint trackPaint = Paint()
      ..color = track
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..strokeCap = StrokeCap.round;
    final Paint fillPaint = Paint()
      ..color = fill
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..strokeCap = StrokeCap.round;
    canvas.drawCircle(center, radius, trackPaint);
    final double sweep = 2 * math.pi * progress.clamp(0.0, 1.0);
    if (sweep > 0) {
      canvas.drawArc(
        Rect.fromCircle(center: center, radius: radius),
        -math.pi / 2,
        sweep,
        false,
        fillPaint,
      );
    }
  }

  @override
  bool shouldRepaint(_RingPainter oldDelegate) =>
      oldDelegate.progress != progress ||
      oldDelegate.fill != fill ||
      oldDelegate.track != track;
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
          decoration: _softCard(),
          child: Row(
            children: <Widget>[
              _IconChip(
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
    final int doneCount = _today.where((o) => o.completed).length;
    return Column(
      children: <Widget>[
        Row(
          children: <Widget>[
            const _SectionHeader(title: 'Bugünün planı'),
            const Spacer(),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(
                color: AppColors.primaryLight,
                borderRadius: BorderRadius.circular(999),
              ),
              child: Text(
                '$doneCount/${_today.length}',
                style: const TextStyle(
                  color: AppColors.primary,
                  fontSize: 12,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        Container(
          decoration: _softCard(),
          child: Column(
            children: <Widget>[
              for (var i = 0; i < _today.length; i++) ...<Widget>[
                if (i > 0)
                  const Divider(
                      height: 1,
                      indent: 16,
                      endIndent: 16,
                      color: Color(0xFFEDF1F7)),
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
    final bool done = occ.completed;
    return _Pressable(
      onTap: done ? null : onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        child: Row(
          children: <Widget>[
            // Saat çipi — okunabilir, premium küçük kapsül.
            Container(
              width: 54,
              padding: const EdgeInsets.symmetric(vertical: 7),
              decoration: BoxDecoration(
                color: done
                    ? AppColors.successSurface
                    : AppColors.primaryLight,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Text(
                hhmm,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontWeight: FontWeight.w800,
                  color: done ? AppColors.accentGreen : AppColors.primary,
                  fontSize: 12.5,
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  color:
                      done ? AppColors.textMuted : AppColors.textPrimary,
                ),
              ),
            ),
            const SizedBox(width: 8),
            if (done)
              const Icon(Icons.check_circle_rounded,
                  color: AppColors.accentGreen, size: 26)
            else
              Container(
                width: 36,
                height: 36,
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: <Color>[AppColors.primary, AppColors.secondary],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  shape: BoxShape.circle,
                  boxShadow: <BoxShadow>[
                    BoxShadow(
                      color: AppColors.primary.withValues(alpha: 0.32),
                      blurRadius: 10,
                      offset: const Offset(0, 5),
                    ),
                  ],
                ),
                child: const Icon(Icons.play_arrow_rounded,
                    color: Colors.white, size: 22),
              ),
          ],
        ),
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
      padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 14),
      decoration: _softCard(),
      child: Row(
        children: <Widget>[
          _IconChip(icon: icon, color: color, size: 44, iconSize: 22),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Text(value,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                        fontWeight: FontWeight.w800,
                        color: AppColors.textPrimary,
                        fontSize: 16)),
                const SizedBox(height: 2),
                Text(label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                        color: AppColors.textSecondary, fontSize: 11.5)),
              ],
            ),
          ),
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

/// Hızlı işlemler bölümünün birincil CTA'sı: tam genişlik, gradient primary zemin,
/// hafif ışıltılı gölge. "Kaldığın yerden devam et" kartıyla ikiz görünmemesi için
/// ayrı ikon (timer) ve metin kullanır.
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
    return _Pressable(
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
                child: _HeroSummary._glowCircle(90, 0.10),
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

/// Öğretmen panosundaki hızlı işlem kartlarıyla aynı düzen: yatay satır,
/// solda gradient ikon madalyonu, yanında etiket (2 sütunlu Wrap).
class _ActionTile extends StatelessWidget {
  const _ActionTile({required this.action, required this.onTap});

  final _ActionItem action;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return _Pressable(
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
        decoration: _softCard(radius: 20),
        child: Row(
          children: <Widget>[
            _IconChip(icon: action.icon, color: action.color, size: 44),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                action.label,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
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
      decoration: _softCard(),
      child: Row(
        children: [
          _IconChip(icon: Icons.quiz_rounded, color: AppColors.primary),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(testName ?? subject,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary)),
                const SizedBox(height: 2),
                Text(subject,
                    style: const TextStyle(
                        color: AppColors.textSecondary, fontSize: 12)),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: BorderRadius.circular(999),
            ),
            child: Text('${StudyFormat.net(net)} net',
                style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    color: AppColors.primary,
                    fontSize: 13)),
          ),
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
      padding: const EdgeInsets.all(12),
      decoration: _softCard(radius: 16),
      child: Row(
        children: [
          _IconChip(
              icon: Icons.menu_book_rounded,
              color: AppColors.accentTeal,
              size: 38,
              iconSize: 19),
          const SizedBox(width: 12),
          Expanded(
            child: Text(topic == null ? subject : '$subject · $topic',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w600)),
          ),
          const SizedBox(width: 8),
          Text(StudyFormat.minutes(minutes),
              style: const TextStyle(
                  fontWeight: FontWeight.w800, color: AppColors.textSecondary)),
        ],
      ),
    );
  }
}
