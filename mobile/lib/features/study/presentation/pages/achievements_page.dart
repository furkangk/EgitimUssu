import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/study/domain/study_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';

class AchievementsPage extends StatefulWidget {
  const AchievementsPage({super.key, required this.studentId});

  final String studentId;

  @override
  State<AchievementsPage> createState() => _AchievementsPageState();
}

class _AchievementsPageState extends State<AchievementsPage> {
  late Future<List<StudyAchievement>> _future;

  static const _icons = <String, IconData>{
    'flag': Icons.flag,
    'event_repeat': Icons.event_repeat,
    'local_fire_department': Icons.local_fire_department,
    'whatshot': Icons.whatshot,
    'schedule': Icons.schedule,
    'military_tech': Icons.military_tech,
    'quiz': Icons.quiz,
    'fact_check': Icons.fact_check,
  };

  @override
  void initState() {
    super.initState();
    _future = injector<StudyRepository>().getAchievements(widget.studentId);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Rozetlerim')),
      body: FutureBuilder<List<StudyAchievement>>(
        future: _future,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const LoadingStateView();
          }
          if (snapshot.hasError) {
            return const ErrorStateView(message: 'Rozetler yüklenemedi.');
          }
          final items = snapshot.data ?? const <StudyAchievement>[];
          if (items.isEmpty) {
            return const EmptyStateView(
              title: 'Henüz rozet yok',
              subtitle: 'Çalışmaya başla, rozetler açılsın!',
            );
          }
          final earned = items.where((a) => a.earned).length;
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Text('$earned / ${items.length} rozet kazanıldı',
                  style: const TextStyle(color: AppColors.textSecondary)),
              const SizedBox(height: 12),
              ...items.map((a) => _AchievementCard(
                    achievement: a,
                    icon: _icons[a.iconKey] ?? Icons.star,
                  )),
            ],
          );
        },
      ),
    );
  }
}

class _AchievementCard extends StatelessWidget {
  const _AchievementCard({required this.achievement, required this.icon});

  final StudyAchievement achievement;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final earned = achievement.earned;
    final color = earned ? AppColors.accentOrange : AppColors.textMuted;
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: earned ? AppColors.accentOrange : AppColors.border),
      ),
      child: Row(
        children: [
          CircleAvatar(
            radius: 24,
            backgroundColor: color.withValues(alpha: 0.15),
            child: Icon(icon, color: color),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(achievement.title,
                    style: const TextStyle(
                        fontWeight: FontWeight.w700, color: AppColors.textPrimary)),
                Text(achievement.description,
                    style: const TextStyle(color: AppColors.textSecondary, fontSize: 12)),
                const SizedBox(height: 8),
                if (!earned)
                  ClipRRect(
                    borderRadius: BorderRadius.circular(6),
                    child: LinearProgressIndicator(
                      value: achievement.progress,
                      minHeight: 6,
                      backgroundColor: AppColors.divider,
                      valueColor: const AlwaysStoppedAnimation(AppColors.accentOrange),
                    ),
                  ),
                if (!earned)
                  Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text('${achievement.currentValue} / ${achievement.threshold}',
                        style: const TextStyle(color: AppColors.textMuted, fontSize: 11)),
                  ),
              ],
            ),
          ),
          if (earned) const Icon(Icons.check_circle, color: AppColors.accentGreen),
        ],
      ),
    );
  }
}
