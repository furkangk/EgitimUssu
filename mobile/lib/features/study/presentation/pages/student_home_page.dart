import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_home_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/cubit/study_home_state.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/study_format.dart';
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

  @override
  Widget build(BuildContext context) {
    final session = context.read<AuthCubit>().state.session;
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Çalışma Panom'),
        actions: [
          IconButton(
            tooltip: 'Ayarlar',
            onPressed: () => context.push('/more'),
            icon: const Icon(Icons.more_horiz),
          ),
        ],
      ),
      body: BlocBuilder<StudyHomeCubit, StudyHomeState>(
        builder: (context, state) {
          if (state.status == StudyHomeStatus.loading ||
              state.status == StudyHomeStatus.initial) {
            return const LoadingStateView(message: 'Çalışma panosu yükleniyor...');
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
            onRefresh: () async => _reload(context),
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                Text(
                  'Merhaba${session?.fullName != null && session!.fullName.isNotEmpty ? ', ${session.fullName.split(' ').first}' : ''} 👋',
                  style: const TextStyle(
                      fontSize: 22, fontWeight: FontWeight.w700, color: AppColors.textPrimary),
                ),
                const SizedBox(height: 4),
                const Text('Bugün de bir adım daha atalım.',
                    style: TextStyle(color: AppColors.textSecondary)),
                const SizedBox(height: 16),
                _TodayGoalCard(
                  todayMinutes: d.todayEffectiveMinutes,
                  goalMinutes: d.todayGoalMinutes,
                  met: d.todayGoalMet,
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: _StatTile(
                        icon: Icons.local_fire_department,
                        color: AppColors.accentOrange,
                        value: '${d.currentStreakDays}',
                        label: 'Günlük seri',
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _StatTile(
                        icon: Icons.timelapse,
                        color: AppColors.accentBlue,
                        value: StudyFormat.minutes(d.weekEffectiveMinutes),
                        label: 'Bu hafta',
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: _StatTile(
                        icon: Icons.emoji_events,
                        color: AppColors.accentGreen,
                        value: '${d.longestStreakDays}',
                        label: 'Rekor seri',
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 20),
                const Text('Hızlı işlemler',
                    style: TextStyle(fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
                const SizedBox(height: 12),
                _ActionGrid(studentId: studentId),
                const SizedBox(height: 20),
                if (d.lastTest != null) ...[
                  const Text('Son deneme',
                      style: TextStyle(fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
                  const SizedBox(height: 8),
                  _LastTestCard(
                    subject: d.lastTest!.subject,
                    net: d.lastTest!.net,
                    testName: d.lastTest!.testName,
                  ),
                  const SizedBox(height: 20),
                ],
                if (d.recentSessions.isNotEmpty) ...[
                  const Text('Son çalışmalar',
                      style: TextStyle(fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
                  const SizedBox(height: 8),
                  ...d.recentSessions.take(5).map((s) => _SessionTile(
                        subject: s.subject,
                        topic: s.topic,
                        minutes: s.effectiveMinutes,
                      )),
                ],
              ],
            ),
          );
        },
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
    final progress = goalMinutes <= 0
        ? 0.0
        : (todayMinutes / goalMinutes).clamp(0.0, 1.0);
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [AppColors.primary, AppColors.secondary],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(18),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text('Bugünkü çalışma',
                  style: TextStyle(color: Colors.white70, fontSize: 13)),
              if (met)
                const Row(children: [
                  Icon(Icons.check_circle, color: Colors.white, size: 16),
                  SizedBox(width: 4),
                  Text('Hedef tamam', style: TextStyle(color: Colors.white, fontSize: 12)),
                ]),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            StudyFormat.minutes(todayMinutes),
            style: const TextStyle(color: Colors.white, fontSize: 30, fontWeight: FontWeight.w800),
          ),
          Text(
            goalMinutes > 0
                ? 'Günlük hedef: ${StudyFormat.minutes(goalMinutes)}'
                : 'Henüz hedef belirlemedin',
            style: const TextStyle(color: Colors.white70, fontSize: 13),
          ),
          const SizedBox(height: 12),
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: LinearProgressIndicator(
              value: progress,
              minHeight: 8,
              backgroundColor: Colors.white24,
              valueColor: const AlwaysStoppedAnimation(Colors.white),
            ),
          ),
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
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        children: [
          Icon(icon, color: color, size: 22),
          const SizedBox(height: 6),
          Text(value,
              style: const TextStyle(
                  fontWeight: FontWeight.w700, color: AppColors.textPrimary, fontSize: 15)),
          const SizedBox(height: 2),
          Text(label,
              textAlign: TextAlign.center,
              style: const TextStyle(color: AppColors.textSecondary, fontSize: 11)),
        ],
      ),
    );
  }
}

class _ActionGrid extends StatelessWidget {
  const _ActionGrid({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context) {
    final actions = <_ActionItem>[
      _ActionItem('Kronometre', Icons.play_circle_fill, AppColors.primary, '/study/timer'),
      _ActionItem('Deneme Gir', Icons.edit_note, AppColors.accentBlue, '/study/test'),
      _ActionItem('Hedefler', Icons.flag, AppColors.accentGreen, '/study/goals'),
      _ActionItem('Geçmiş', Icons.history, AppColors.accentTeal, '/study/history'),
      _ActionItem('Rozetler', Icons.emoji_events, AppColors.accentOrange, '/study/achievements'),
    ];
    return GridView.count(
      crossAxisCount: 3,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      childAspectRatio: 0.95,
      children: actions
          .map((a) => InkWell(
                borderRadius: BorderRadius.circular(14),
                onTap: () => context.push('${a.route}?studentId=$studentId'),
                child: Container(
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: BorderRadius.circular(14),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(a.icon, color: a.color, size: 26),
                      const SizedBox(height: 8),
                      Text(a.label,
                          textAlign: TextAlign.center,
                          style: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w600,
                              color: AppColors.textPrimary)),
                    ],
                  ),
                ),
              ))
          .toList(),
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
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          CircleAvatar(
            backgroundColor: AppColors.primaryLight,
            child: const Icon(Icons.quiz, color: AppColors.primary),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(testName ?? subject,
                    style: const TextStyle(fontWeight: FontWeight.w600, color: AppColors.textPrimary)),
                Text(subject, style: const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
              ],
            ),
          ),
          Text('${StudyFormat.net(net)} net',
              style: const TextStyle(fontWeight: FontWeight.w700, color: AppColors.primary)),
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
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          const Icon(Icons.menu_book, color: AppColors.textSecondary, size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Text(topic == null ? subject : '$subject · $topic',
                style: const TextStyle(color: AppColors.textPrimary)),
          ),
          Text(StudyFormat.minutes(minutes),
              style: const TextStyle(fontWeight: FontWeight.w600, color: AppColors.textSecondary)),
        ],
      ),
    );
  }
}
