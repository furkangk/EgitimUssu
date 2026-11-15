import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/students/domain/student_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/study_tab_widgets.dart';
import 'package:egitim_ussu_mobile/l10n/app_localizations.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_page_header.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class StudentProfilePage extends StatefulWidget {
  const StudentProfilePage({super.key});

  @override
  State<StudentProfilePage> createState() => _StudentProfilePageState();
}

class _StudentProfilePageState extends State<StudentProfilePage> {
  StudyRepository get _repo => injector<StudyRepository>();
  StudentRepository get _studentRepo => injector<StudentRepository>();

  String? _studentId;
  StudentProfile? _profile;
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
      final userId = session?.userId ?? '';
      final studentId =
          _studentId ??
          await StudentScope.resolve(
            userId: userId,
            fullName: session?.fullName ?? '',
          );
      final streak = await _repo.getStreak(studentId);
      final sessions = await _repo.listSessions(studentId);
      final tests = await _repo.listTests(studentId);
      final achievements = await _repo.getAchievements(studentId);
      final profile = await _studentRepo.getByUser(userId);
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _profile = profile;
        _streak = streak;
        _sessions = sessions
            .where((StudySession s) => s.status == 'Completed')
            .toList();
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
      body: SafeArea(
        bottom: false,
        child: _loading
            ? LoadingStateView(
                message: AppLocalizations.of(context).study_loadingYourProfile,
              )
            : _error != null
            ? ErrorStateView(message: _error!, onRetry: _load)
            : _content(),
      ),
      bottomNavigationBar: const StudentBottomNav(
        current: StudentNavTab.profile,
      ),
    );
  }

  Widget _content() {
    final StudyStreak streak = _streak!;
    final String studentId = _studentId!;
    final session = context.read<AuthCubit>().state.session;
    final String name = (session?.fullName.trim().isNotEmpty ?? false)
        ? session!.fullName.trim()
        : AppLocalizations.of(context).finance_ogrenci;
    final String gradeLevel = _profile?.gradeLevel.trim().isNotEmpty ?? false
        ? _profile!.gradeLevel.trim()
        : AppLocalizations.of(context).study_gradeNotProvided;
    final String? goalSummary = _profile?.goalSummary;

    final int totalMinutes = _sessions.fold<int>(
      0,
      (int s, StudySession x) => s + x.effectiveMinutes,
    );
    final double totalNet = _tests.fold<double>(
      0,
      (double s, TestResult t) => s + t.net,
    );
    final String mostSubject = _mostStudiedSubject();
    final int earnedBadges = _achievements
        .where((StudyAchievement a) => a.earned)
        .length;

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: <Widget>[
          AppPageHeader(
            title: AppLocalizations.of(context).study_profile,
            subtitle: AppLocalizations.of(
              context,
            ).study_yourDetailsSettingsAndStatistics,
          ),
          const SizedBox(height: 16),
          _ProfileHero(
            name: name,
            gradeLevel: gradeLevel,
            goalSummary: goalSummary,
            streakDays: streak.currentStreakDays,
          ),
          const SizedBox(height: 20),
          Row(
            children: <Widget>[
              Expanded(
                child: StudyStatTile(
                  icon: Icons.timelapse_rounded,
                  color: AppColors.accentTeal,
                  value: studyMinutes(context, totalMinutes),
                  label: AppLocalizations.of(context).study_totalStudyTime,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.event_available_rounded,
                  color: AppColors.accentGreen,
                  value: '${streak.totalStudyDays}',
                  label: AppLocalizations.of(context).study_daysStudied,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.emoji_events_rounded,
                  color: AppColors.accentOrange,
                  value: '${streak.longestStreakDays}',
                  label: AppLocalizations.of(context).study_bestStreak,
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
                  label: AppLocalizations.of(context).study_totalPracticeExams,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.calculate_rounded,
                  color: AppColors.primary,
                  value: StudyFormat.net(totalNet),
                  label: AppLocalizations.of(context).study_totalNetScore,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: StudyStatTile(
                  icon: Icons.menu_book_rounded,
                  color: AppColors.accentTeal,
                  value: mostSubject,
                  label: AppLocalizations.of(context).study_mostStudied,
                ),
              ),
            ],
          ),
          const SizedBox(height: 24),
          StudySectionHeader(
            title: AppLocalizations.of(context).study_badges,
            action: StudySectionAction(
              label: AppLocalizations.of(context).p19list_all,
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
          const SizedBox(height: 24),
          StudySectionHeader(
            title: AppLocalizations.of(context).study_settings,
          ),
          const SizedBox(height: 12),

          _ProfileMenuTile(
            icon: Icons.school_rounded,
            color: AppColors.accentTeal,
            title: AppLocalizations.of(context).study_myTeachers,
            subtitle: AppLocalizations.of(
              context,
            ).study_yourTeachersAndContactDetails,
            onTap: () => context.push('/student/teacher'),
          ),
          // D-08 (M16): Gelen kutusu — öğretmenlerinle sohbet.
          _ProfileMenuTile(
            icon: Icons.chat_bubble_rounded,
            color: AppColors.accentBlue,
            title: AppLocalizations.of(context).more_messagesTitle,
            subtitle: AppLocalizations.of(context).study_chatWithYourTeachers,
            onTap: () => context.push('/messages'),
          ),
          _ProfileMenuTile(
            icon: Icons.flag_rounded,
            color: AppColors.accentGreen,
            title: AppLocalizations.of(context).study_addGoal,
            subtitle: AppLocalizations.of(context).study_createANewStudyGoal,
            onTap: () => context.push('/study/goals?studentId=$studentId'),
          ),

          const SizedBox(height: 12),
          _ProfileMenuTile(
            icon: Icons.logout_rounded,
            color: AppColors.accentRed,
            title: AppLocalizations.of(context).more_logoutTitle,
            subtitle: AppLocalizations.of(context).more_logoutSubtitle,
            titleColor: AppColors.accentRed,
            onTap: () => _confirmLogout(context),
          ),
        ],
      ),
    );
  }

  void _confirmLogout(BuildContext context) {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (BuildContext sheetContext) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 6, 20, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                AppLocalizations.of(context).more_logoutTitle,
                style: Theme.of(sheetContext).textTheme.titleLarge?.copyWith(
                  color: AppColors.primary,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 10),
              Text(
                AppLocalizations.of(context).study_areYouSureYouWantToSignOut,
                style: Theme.of(sheetContext).textTheme.bodyMedium?.copyWith(
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 18),
              Row(
                children: <Widget>[
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () => Navigator.of(sheetContext).pop(),
                      child: Text(AppLocalizations.of(context).finance_vazgec),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: FilledButton(
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColors.accentRed,
                      ),
                      onPressed: () {
                        Navigator.of(sheetContext).pop();
                        context.read<AuthCubit>().logout();
                      },
                      child: Text(
                        AppLocalizations.of(context).more_logoutTitle,
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
      },
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

/// Profil hero kartı — premium'da altın gradyan kenarlık + "Premium" rozeti,
/// free'de sade kart + "Yükselt" ipucu. Fotoğraf alanı henüz backend'de yok
/// → avatar her zaman baş harflerle gösterilir.
class _ProfileHero extends StatelessWidget {
  const _ProfileHero({
    required this.name,
    required this.gradeLevel,
    required this.goalSummary,
    required this.streakDays,
  });
  final String name;
  final String gradeLevel;
  final String? goalSummary;
  final int streakDays;

  @override
  Widget build(BuildContext context) {
    final initials = name
        .trim()
        .split(RegExp(r'\s+'))
        .take(2)
        .where((p) => p.isNotEmpty)
        .map((p) => p[0].toUpperCase())
        .join();
    return StudyCard(
      child: Row(
        children: <Widget>[
          _Avatar(initials: initials),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(name, style: Theme.of(context).textTheme.titleLarge),
                Text(gradeLevel),
                if (goalSummary?.trim().isNotEmpty ?? false) Text(goalSummary!),
                Text(
                  streakDays > 0
                      ? AppLocalizations.of(
                          context,
                        ).study_dayStreak2((streakDays).toString())
                      : AppLocalizations.of(context).study_startStudyingToday,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Avatar extends StatelessWidget {
  const _Avatar({required this.initials});

  final String initials;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 60,
      height: 60,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: AppColors.primaryLight,
        shape: BoxShape.circle,
      ),
      child: Text(
        initials,
        style: const TextStyle(
          color: AppColors.primary,
          fontWeight: FontWeight.w800,
          fontSize: 20,
        ),
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
              child: const Icon(
                Icons.workspace_premium_rounded,
                color: AppColors.accentOrange,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    AppLocalizations.of(
                      context,
                    ).study_badgesEarned(earned, total),
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 8),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(6),
                    child: LinearProgressIndicator(
                      value: ratio,
                      minHeight: 8,
                      backgroundColor: AppColors.divider,
                      valueColor: const AlwaysStoppedAnimation<Color>(
                        AppColors.accentOrange,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
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

class _ProfileMenuTile extends StatelessWidget {
  const _ProfileMenuTile({
    required this.icon,
    required this.color,
    required this.title,
    required this.subtitle,
    required this.onTap,
    this.titleColor,
  });

  final IconData icon;
  final Color color;
  final String title;
  final String subtitle;
  final VoidCallback? onTap;
  final Color? titleColor;

  @override
  Widget build(BuildContext context) {
    final bool enabled = onTap != null;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: InkWell(
        borderRadius: BorderRadius.circular(18),
        onTap: onTap,
        child: Opacity(
          opacity: enabled ? 1 : 0.55,
          child: Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(18),
              border: Border.all(color: AppColors.border),
            ),
            child: Row(
              children: <Widget>[
                Container(
                  width: 44,
                  height: 44,
                  alignment: Alignment.center,
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
                        style: TextStyle(
                          fontWeight: FontWeight.w800,
                          color: titleColor ?? AppColors.textPrimary,
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
                if (enabled)
                  const Icon(
                    Icons.chevron_right_rounded,
                    color: AppColors.textSecondary,
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Alt sayfa (bottom sheet) içinde kullanılan basit seçenek satırı —
/// [_ProfileMenuTile]'ın daha sade sürümü (kart çerçevesiz).
