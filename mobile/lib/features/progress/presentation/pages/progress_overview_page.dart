import 'package:egitim_ussu_mobile/core/di/injector.dart';
import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/progress/domain/progress_contracts.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';

/// Öğrencinin konu bazlı gelişimi (M10). Genel dağılım + eksik konular + güçlü konular + tüm konu listesi.
/// Veri M08 çalışma/test olaylarından türetilir; çalışıp deneme girdikçe konular buraya düşer.
class ProgressOverviewPage extends StatefulWidget {
  const ProgressOverviewPage({super.key, required this.studentId});

  final String studentId;

  @override
  State<ProgressOverviewPage> createState() => _ProgressOverviewPageState();
}

class _ProgressOverviewPageState extends State<ProgressOverviewPage> {
  final ProgressRepository _repo = injector<ProgressRepository>();

  bool _loading = true;
  String? _error;
  ProgressOverview? _overview;
  List<TopicMastery> _all = <TopicMastery>[];

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
      final overview = await _repo.getOverview(widget.studentId);
      final all = await _repo.listMastery(widget.studentId);
      if (!mounted) return;
      setState(() {
        _overview = overview;
        _all = all;
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
      appBar: AppBar(title: const Text('Gelişimim')),
      body: _loading
          ? const LoadingStateView(message: 'Yükleniyor...')
          : _error != null
              ? ErrorStateView(message: _error!, onRetry: _load)
              : _content(),
    );
  }

  Widget _content() {
    final overview = _overview!;
    if (overview.trackedTopics == 0 && overview.notStartedCount == 0) {
      return const EmptyStateView(
        title: 'Henüz gelişim verisi yok',
        subtitle:
            'Kronometreyle çalışıp deneme girdikçe konularındaki gelişimin burada oluşur.',
      );
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: <Widget>[
          _DistributionCard(overview: overview),
          if (overview.weakSpots.isNotEmpty) ...<Widget>[
            const SizedBox(height: 18),
            const _SectionTitle('Eksik konular', color: AppColors.accentRed),
            const SizedBox(height: 8),
            ...overview.weakSpots.map((m) => _MasteryTile(mastery: m)),
          ],
          if (overview.strengths.isNotEmpty) ...<Widget>[
            const SizedBox(height: 18),
            const _SectionTitle('Güçlü konular', color: AppColors.accentGreen),
            const SizedBox(height: 8),
            ...overview.strengths.map((m) => _MasteryTile(mastery: m)),
          ],
          if (_all.isNotEmpty) ...<Widget>[
            const SizedBox(height: 18),
            const _SectionTitle('Tüm konular', color: AppColors.primary),
            const SizedBox(height: 8),
            ..._all.map((m) => _MasteryTile(mastery: m)),
          ],
        ],
      ),
    );
  }
}

class _DistributionCard extends StatelessWidget {
  const _DistributionCard({required this.overview});

  final ProgressOverview overview;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Konu dağılımı',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w800,
                ),
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: <Widget>[
              _Stat(label: 'Uzman', value: overview.masteredCount, color: AppColors.accentGreen),
              _Stat(label: 'Yeterli', value: overview.proficientCount, color: AppColors.accentBlue),
              _Stat(label: 'Gelişen', value: overview.developingCount, color: AppColors.accentOrange),
              _Stat(label: 'Zayıf', value: overview.weakCount, color: AppColors.accentRed),
            ],
          ),
          if (overview.activeGoalCount > 0) ...<Widget>[
            const SizedBox(height: 12),
            Text(
              '${overview.activeGoalCount} aktif konu hedefi',
              style: const TextStyle(color: AppColors.primary, fontWeight: FontWeight.w600),
            ),
          ],
        ],
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.label, required this.value, required this.color});

  final String label;
  final int value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 74,
      padding: const EdgeInsets.symmetric(vertical: 12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        children: <Widget>[
          Text(
            '$value',
            style: TextStyle(color: color, fontWeight: FontWeight.w800, fontSize: 20),
          ),
          const SizedBox(height: 2),
          Text(label, style: TextStyle(color: color, fontSize: 12)),
        ],
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text, {required this.color});

  final String text;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: Theme.of(context).textTheme.titleSmall?.copyWith(
            color: color,
            fontWeight: FontWeight.w800,
          ),
    );
  }
}

class _MasteryTile extends StatelessWidget {
  const _MasteryTile({required this.mastery});

  final TopicMastery mastery;

  @override
  Widget build(BuildContext context) {
    final (String levelLabel, Color color) = switch (mastery.masteryLevel) {
      'Mastered' => ('Uzman', AppColors.accentGreen),
      'Proficient' => ('Yeterli', AppColors.accentBlue),
      'Developing' => ('Gelişen', AppColors.accentOrange),
      'Weak' => ('Zayıf', AppColors.accentRed),
      _ => ('Başlanmadı', AppColors.textSecondary),
    };
    final trendIcon = switch (mastery.trend) {
      'Improving' => Icons.trending_up_rounded,
      'Declining' => Icons.trending_down_rounded,
      _ => Icons.trending_flat_rounded,
    };
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.skyBorder),
        boxShadow: AppShadows.soft,
      ),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  mastery.topic,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  mastery.subject,
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
          Icon(trendIcon, size: 18, color: color),
          const SizedBox(width: 10),
          _LevelPill(label: levelLabel, color: color, score: mastery.masteryScore),
        ],
      ),
    );
  }
}

class _LevelPill extends StatelessWidget {
  const _LevelPill({required this.label, required this.color, required this.score});

  final String label;
  final Color color;
  final double score;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        '$label · ${score.round()}',
        style: TextStyle(color: color, fontWeight: FontWeight.w700, fontSize: 12),
      ),
    );
  }
}
