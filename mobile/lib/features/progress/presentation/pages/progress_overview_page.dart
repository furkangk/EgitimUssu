import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/progress/domain/progress_contracts.dart';
import 'package:egitim_ussu_mobile/features/progress/presentation/cubit/progress_cubit.dart';
import 'package:egitim_ussu_mobile/features/progress/presentation/widgets/progress_snapshot_chart.dart';
import 'package:egitim_ussu_mobile/features/reporting/presentation/pages/student_analysis_page.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// Öğrencinin konu bazlı gelişimi (M10). Genel dağılım + dönemsel gelişim grafiği
/// (M10-1 snapshot serisi) + eksik/güçlü konular + tüm konu listesi.
///
/// Aynı gövde üç kapsamda çalışır (öğrenci kendisi / bağlı öğretmen / onaylı veli);
/// hangi ucun çağrılacağını [scope] belirler, yetki ve gizlilik **sunucudadır**.
/// Snapshot serisi yalnız öğrencinin kendi kapsamında gelir — uç diğerlerine kapalı.
class ProgressOverviewPage extends StatelessWidget {
  const ProgressOverviewPage({
    super.key,
    required this.studentId,
    this.scope = ProgressScope.self,
    this.viewerUserId,
    this.title = 'Gelişimim',
  });

  final String studentId;
  final ProgressScope scope;

  /// Öğretmen/veli kapsamında rotadaki kimlik (oturumdaki kullanıcı).
  final String? viewerUserId;
  final String title;

  @override
  Widget build(BuildContext context) {
    if (studentId.isEmpty ||
        (scope != ProgressScope.self &&
            (viewerUserId == null || viewerUserId!.isEmpty))) {
      return Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(title: Text(title)),
        body: const EmptyStateView(
          title: 'Öğrenci seçilmedi',
          subtitle: 'Gelişim verisi için bir öğrenci kimliği gerekiyor.',
        ),
      );
    }

    return BlocProvider<ProgressCubit>(
      create: (_) => _startLoad(ProgressCubit.create()),
      child: Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(title: Text(title)),
        body: BlocBuilder<ProgressCubit, ProgressState>(
          builder: (context, state) {
            final cubit = context.read<ProgressCubit>();
            if (state.isLoading || state.status == ProgressStatus.initial) {
              return const LoadingStateView(message: 'Yükleniyor...');
            }
            if (state.status == ProgressStatus.failure) {
              return ErrorStateView(
                message: state.errorMessage ?? 'Gelişim verisi yüklenemedi.',
                onRetry: cubit.refresh,
              );
            }
            if (state.isEmpty) {
              return EmptyStateView(
                title: 'Henüz gelişim verisi yok',
                subtitle: scope == ProgressScope.self
                    ? 'Kronometreyle çalışıp deneme girdikçe konularındaki '
                        'gelişimin burada oluşur.'
                    : 'Bu dönem için kayıt bulunmuyor. Öğrenci çalışma verisi '
                        'paylaşımını kapatmış da olabilir.',
              );
            }
            return _Content(
              state: state,
              studentId: studentId,
              onRefresh: cubit.refresh,
            );
          },
        ),
      ),
    );
  }

  /// Kapsam seçimi tek yerde: hangi ucun çağrılacağını burada belirleriz,
  /// ekran gövdesi üç kapsamda da aynıdır.
  ProgressCubit _startLoad(ProgressCubit cubit) {
    switch (scope) {
      case ProgressScope.self:
        cubit.loadSelf(studentId);
      case ProgressScope.teacher:
        cubit.loadForTeacher(
          teacherUserId: viewerUserId!,
          studentId: studentId,
        );
      case ProgressScope.parent:
        cubit.loadForParent(
          parentUserId: viewerUserId!,
          studentId: studentId,
        );
    }
    return cubit;
  }
}

class _Content extends StatelessWidget {
  const _Content({
    required this.state,
    required this.studentId,
    required this.onRefresh,
  });

  final ProgressState state;
  final String studentId;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    final overview = state.overview!;
    final series = state.snapshotsBySubject;

    return RefreshIndicator(
      onRefresh: onRefresh,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: <Widget>[
          _DistributionCard(overview: overview),
          if (series.isNotEmpty) ...<Widget>[
            const SizedBox(height: 18),
            const _SectionTitle('Dönemsel gelişim', color: AppColors.primary),
            const SizedBox(height: 8),
            Container(
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: AppColors.skyBorder),
                boxShadow: AppShadows.soft,
              ),
              padding: const EdgeInsets.fromLTRB(12, 16, 16, 12),
              child: ProgressSnapshotChart(seriesBySubject: series),
            ),
          ],
          if (state.scope == ProgressScope.self) ...<Widget>[
            const SizedBox(height: 12),
            _AnalysisLink(studentId: studentId),
          ],
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
          if (state.mastery.isNotEmpty) ...<Widget>[
            const SizedBox(height: 18),
            const _SectionTitle('Tüm konular', color: AppColors.primary),
            const SizedBox(height: 8),
            ...state.mastery.map((m) => _MasteryTile(mastery: m)),
          ],
        ],
      ),
    );
  }
}

/// M14-2/3: Gelişim ekranından çalışma/performans analizine köprü.
class _AnalysisLink extends StatelessWidget {
  const _AnalysisLink({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute<void>(
            builder: (_) => StudentAnalysisPage(studentId: studentId),
          ),
        ),
        child: Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: AppColors.skyBorder),
          ),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
          child: Row(
            children: <Widget>[
              const Icon(
                Icons.insights_rounded,
                color: AppColors.accentTeal,
                size: 20,
              ),
              const SizedBox(width: 12),
              const Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      'Çalışma analizi ve PDF rapor',
                      style: TextStyle(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    SizedBox(height: 2),
                    Text(
                      'Haftalık süre, ders bazlı net, indirilebilir rapor',
                      style: TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(
                Icons.chevron_right_rounded,
                color: AppColors.textMuted,
              ),
            ],
          ),
        ),
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
