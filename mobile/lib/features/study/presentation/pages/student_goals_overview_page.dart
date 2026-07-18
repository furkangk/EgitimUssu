import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/study_tab_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// "Hedefler" ekranı — günlük/haftalık/net hedef ilerlemesi (ogrenci_ux §9).
/// "Diğer" hub'ından açılır (alt menüde artık Takvim var). Aylık ve üniversite
/// hedefi backend'de henüz alan olmadığından "yakında".
class StudentGoalsOverviewPage extends StatefulWidget {
  const StudentGoalsOverviewPage({super.key});

  @override
  State<StudentGoalsOverviewPage> createState() =>
      _StudentGoalsOverviewPageState();
}

class _StudentGoalsOverviewPageState extends State<StudentGoalsOverviewPage> {
  StudyRepository get _repo => injector<StudyRepository>();

  String? _studentId;
  StudyDashboard? _dashboard;
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
      final dashboard = await _repo.getDashboard(studentId);
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _dashboard = dashboard;
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

  Future<void> _editGoals() async {
    final String studentId = _studentId ?? '';
    if (studentId.isEmpty) return;
    await context.push('/study/goals?studentId=$studentId');
    if (mounted) await _load();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Hedefler')),
      body: SafeArea(
        bottom: false,
        child: _loading
            ? const LoadingStateView(message: 'Hedeflerin yükleniyor...')
            : _error != null
                ? ErrorStateView(message: _error!, onRetry: _load)
                : _content(),
      ),
    );
  }

  Widget _content() {
    final StudyDashboard d = _dashboard!;
    final StudyGoal? goal = d.activeGoal;
    final int dailyGoal = d.todayGoalMinutes;
    final int? weeklyGoal = goal?.weeklyGoalMinutes;
    final double? targetNet = goal?.targetNet;
    final double? currentNet = d.lastTest?.net;

    return RefreshIndicator(
      color: AppColors.primary,
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 14, 16, 24),
        children: <Widget>[
          Text(
            'Küçük hedefler, büyük ilerleme.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: AppColors.textSecondary,
                  fontWeight: FontWeight.w600,
                ),
          ),
          const SizedBox(height: 16),
          _GoalCard(
            icon: Icons.today_rounded,
            color: AppColors.primary,
            title: 'Günlük hedef',
            current: d.todayEffectiveMinutes.toDouble(),
            target: dailyGoal.toDouble(),
            valueLabel: StudyFormat.minutes(d.todayEffectiveMinutes),
            targetLabel:
                dailyGoal > 0 ? StudyFormat.minutes(dailyGoal) : 'belirlenmedi',
          ),
          const SizedBox(height: 12),
          _GoalCard(
            icon: Icons.calendar_view_week_rounded,
            color: AppColors.accentTeal,
            title: 'Haftalık hedef',
            current: d.weekEffectiveMinutes.toDouble(),
            target: (weeklyGoal ?? 0).toDouble(),
            valueLabel: StudyFormat.minutes(d.weekEffectiveMinutes),
            targetLabel: (weeklyGoal != null && weeklyGoal > 0)
                ? StudyFormat.minutes(weeklyGoal)
                : 'belirlenmedi',
          ),
          const SizedBox(height: 12),
          _GoalCard(
            icon: Icons.track_changes_rounded,
            color: AppColors.accentGreen,
            title: 'Net hedefi',
            current: currentNet ?? 0,
            target: targetNet ?? 0,
            valueLabel:
                currentNet != null ? '${StudyFormat.net(currentNet)} net' : '—',
            targetLabel: (targetNet != null && targetNet > 0)
                ? '${StudyFormat.net(targetNet)} net'
                : 'belirlenmedi',
          ),
          const SizedBox(height: 16),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton.icon(
              onPressed: _editGoals,
              icon: const Icon(Icons.tune_rounded),
              label: const Text('Hedefleri düzenle'),
              style: OutlinedButton.styleFrom(
                foregroundColor: AppColors.primary,
                side: const BorderSide(color: AppColors.primary),
                padding: const EdgeInsets.symmetric(vertical: 14),
                shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(14)),
              ),
            ),
          ),
          const SizedBox(height: 24),
          const StudySectionHeader(title: 'Uzun vadeli hedefler'),
          const SizedBox(height: 12),
          const StudyComingSoonCard(
            icon: Icons.calendar_month_rounded,
            title: 'Aylık hedef',
            message: 'Aylık çalışma ve net hedefleri yakında burada olacak.',
          ),
          const SizedBox(height: 12),
          const StudyComingSoonCard(
            icon: Icons.school_rounded,
            title: 'Üniversite hedefi',
            message: 'Hedef bölüm ve sıralaman için takip yakında geliyor.',
          ),
        ],
      ),
    );
  }
}

class _GoalCard extends StatelessWidget {
  const _GoalCard({
    required this.icon,
    required this.color,
    required this.title,
    required this.current,
    required this.target,
    required this.valueLabel,
    required this.targetLabel,
  });

  final IconData icon;
  final Color color;
  final String title;
  final double current;
  final double target;
  final String valueLabel;
  final String targetLabel;

  @override
  Widget build(BuildContext context) {
    final bool hasTarget = target > 0;
    final double progress =
        hasTarget ? (current / target).clamp(0.0, 1.0) : 0.0;
    final bool met = hasTarget && current >= target;
    return StudyCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(icon, color: color, size: 22),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(title,
                    style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                        fontSize: 15)),
              ),
              if (met)
                const Icon(Icons.check_circle_rounded,
                    color: AppColors.accentGreen, size: 20),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: <Widget>[
              Text(valueLabel,
                  style: TextStyle(
                      color: color,
                      fontWeight: FontWeight.w800,
                      fontSize: 18)),
              Text('Hedef: $targetLabel',
                  style: const TextStyle(
                      color: AppColors.textSecondary, fontSize: 12)),
            ],
          ),
          const SizedBox(height: 10),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: hasTarget ? progress : 0,
              minHeight: 8,
              backgroundColor: AppColors.divider,
              valueColor: AlwaysStoppedAnimation<Color>(color),
            ),
          ),
        ],
      ),
    );
  }
}
