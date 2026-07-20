import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/study_tab_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// Profil / İstatistik ekranı (ogrenci_ux §11) — "Diğer" hub'ından açılır.
/// İstatistik odaklı: toplam çalışma, toplam test/net, en çok çalışılan ders,
/// seri gün, rozetler. Tüm değerler mevcut Study API verisinden türetilir.
class StudentProfilePage extends StatefulWidget {
  const StudentProfilePage({super.key});

  @override
  State<StudentProfilePage> createState() => _StudentProfilePageState();
}

class _StudentProfilePageState extends State<StudentProfilePage> {
  StudyRepository get _repo => injector<StudyRepository>();

  String? _studentId;
  StudyStreak? _streak;
  List<StudySession> _sessions = const <StudySession>[];
  List<TestResult> _tests = const <TestResult>[];
  List<StudyAchievement> _achievements = const <StudyAchievement>[];
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
      final sessions = await _repo.listSessions(studentId);
      final tests = await _repo.listTests(studentId);
      final achievements = await _repo.getAchievements(studentId);
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _streak = streak;
        _sessions =
            sessions.where((StudySession s) => s.status == 'Completed').toList();
        _tests = tests;
        _achievements = achievements;
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
      appBar: AppBar(title: const Text('Profil')),
      body: _loading
          ? const LoadingStateView(message: 'Profilin yükleniyor...')
          : _error != null
              ? ErrorStateView(message: _error!, onRetry: _load)
              : _content(),
      bottomNavigationBar:
          const StudentBottomNav(current: StudentNavTab.profile),
    );
  }

  Widget _content() {
    final StudyStreak streak = _streak!;
    final String studentId = _studentId!;
    final session = context.read<AuthCubit>().state.session;
    final String name = (session?.fullName.trim().isNotEmpty ?? false)
        ? session!.fullName.trim()
        : 'Öğrenci';

    final int totalMinutes = _sessions
        .fold<int>(0, (int s, StudySession x) => s + x.effectiveMinutes);
    final double totalNet =
        _tests.fold<double>(0, (double s, TestResult t) => s + t.net);
    final String mostSubject = _mostStudiedSubject();
    final int earnedBadges =
        _achievements.where((StudyAchievement a) => a.earned).length;

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: <Widget>[
          _ProfileHeader(name: name, streakDays: streak.currentStreakDays),
          const SizedBox(height: 20),
          Row(
            children: <Widget>[
              Expanded(
                child: StudyStatTile(
                  icon: Icons.timelapse_rounded,
                  color: AppColors.accentTeal,
                  value: StudyFormat.minutes(totalMinutes),
                  label: 'Toplam çalışma',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.event_available_rounded,
                  color: AppColors.accentGreen,
                  value: '${streak.totalStudyDays}',
                  label: 'Çalışılan gün',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.emoji_events_rounded,
                  color: AppColors.accentOrange,
                  value: '${streak.longestStreakDays}',
                  label: 'Rekor seri',
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: <Widget>[
              Expanded(
                child: StudyStatTile(
                  icon: Icons.assignment_turned_in_rounded,
                  color: AppColors.accentBlue,
                  value: '${_tests.length}',
                  label: 'Toplam deneme',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.calculate_rounded,
                  color: AppColors.primary,
                  value: StudyFormat.net(totalNet),
                  label: 'Toplam net',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.menu_book_rounded,
                  color: AppColors.accentTeal,
                  value: mostSubject,
                  label: 'En çok çalışılan',
                ),
              ),
            ],
          ),
          const SizedBox(height: 24),
          StudySectionHeader(
            title: 'Rozetler',
            action: StudySectionAction(
              label: 'Tümü',
              onTap: () =>
                  context.push('/study/achievements?studentId=$studentId'),
            ),
          ),
          const SizedBox(height: 12),
          _BadgeSummary(
            earned: earnedBadges,
            total: _achievements.length,
            onTap: () =>
                context.push('/study/achievements?studentId=$studentId'),
          ),
        ],
      ),
    );
  }

  String _mostStudiedSubject() {
    if (_sessions.isEmpty) return '—';
    final Map<String, int> minutes = <String, int>{};
    for (final StudySession s in _sessions) {
      minutes[s.subject] = (minutes[s.subject] ?? 0) + s.effectiveMinutes;
    }
    String best = '—';
    int bestMinutes = -1;
    minutes.forEach((String subject, int total) {
      if (total > bestMinutes) {
        bestMinutes = total;
        best = subject;
      }
    });
    return best;
  }
}

class _ProfileHeader extends StatelessWidget {
  const _ProfileHeader({required this.name, required this.streakDays});

  final String name;
  final int streakDays;

  @override
  Widget build(BuildContext context) {
    final String initials = name.trim().isEmpty
        ? '?'
        : name
            .trim()
            .split(RegExp(r'\s+'))
            .take(2)
            .map((String p) => p[0].toUpperCase())
            .join();
    return StudyCard(
      child: Row(
        children: <Widget>[
          Container(
            width: 56,
            height: 56,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: BorderRadius.circular(18),
            ),
            child: Text(initials,
                style: const TextStyle(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w800,
                    fontSize: 20)),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(name,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                        fontWeight: FontWeight.w800,
                        color: AppColors.textPrimary,
                        fontSize: 18)),
                const SizedBox(height: 2),
                Text(
                    streakDays > 0
                        ? '🔥 $streakDays gündür çalışıyorsun'
                        : 'Bugün çalışmaya başla',
                    style: const TextStyle(
                        color: AppColors.textSecondary, fontSize: 13)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _BadgeSummary extends StatelessWidget {
  const _BadgeSummary({
    required this.earned,
    required this.total,
    required this.onTap,
  });

  final int earned;
  final int total;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final double ratio = total <= 0 ? 0 : (earned / total).clamp(0.0, 1.0);
    return InkWell(
      borderRadius: BorderRadius.circular(18),
      onTap: onTap,
      child: StudyCard(
        child: Row(
          children: <Widget>[
            Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color: AppColors.accentOrange.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(14),
              ),
              child: const Icon(Icons.workspace_premium_rounded,
                  color: AppColors.accentOrange),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text('$earned / $total rozet kazanıldı',
                      style: const TextStyle(
                          fontWeight: FontWeight.w700,
                          color: AppColors.textPrimary)),
                  const SizedBox(height: 8),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(6),
                    child: LinearProgressIndicator(
                      value: ratio,
                      minHeight: 8,
                      backgroundColor: AppColors.divider,
                      valueColor: const AlwaysStoppedAnimation<Color>(
                          AppColors.accentOrange),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            const Icon(Icons.chevron_right_rounded,
                color: AppColors.textSecondary),
          ],
        ),
      ),
    );
  }
}
