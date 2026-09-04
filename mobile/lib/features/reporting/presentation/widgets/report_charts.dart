import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/reporting/domain/reporting_contracts.dart';
import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

/// Rapor ekranlarının ortak kart kabuğu (design_system.md: 20 radius + soft gölge).
class ReportCard extends StatelessWidget {
  const ReportCard({
    super.key,
    required this.title,
    required this.child,
    this.subtitle,
    this.trailing,
  });

  final String title;
  final String? subtitle;
  final Widget? trailing;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 14),
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
          Row(
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      title,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            color: AppColors.textPrimary,
                            fontWeight: FontWeight.w800,
                          ),
                    ),
                    if (subtitle != null) ...<Widget>[
                      const SizedBox(height: 2),
                      Text(
                        subtitle!,
                        style: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
              if (trailing != null) trailing!,
            ],
          ),
          const SizedBox(height: 14),
          child,
        ],
      ),
    );
  }
}

/// M14-2: Haftalık çalışma süresi çubuk grafiği (dakika).
class WeeklyStudyBarChart extends StatelessWidget {
  const WeeklyStudyBarChart({super.key, required this.weeks});

  final List<StudentStudyWeek> weeks;

  @override
  Widget build(BuildContext context) {
    if (weeks.isEmpty) {
      return const SizedBox.shrink();
    }
    final sorted = <StudentStudyWeek>[...weeks]
      ..sort((a, b) => a.weekStart.compareTo(b.weekStart));
    // Son 12 hafta: daha uzun seride çubuklar okunmaz hâle geliyor.
    final visible = sorted.length > 12
        ? sorted.sublist(sorted.length - 12)
        : sorted;
    final maxMinutes = visible
        .map((w) => w.studyMinutes)
        .reduce((a, b) => a > b ? a : b);
    final labelFormat = DateFormat('d MMM', 'tr_TR');

    return SizedBox(
      height: 180,
      child: BarChart(
        BarChartData(
          alignment: BarChartAlignment.spaceAround,
          maxY: (maxMinutes <= 0 ? 60 : maxMinutes * 1.2).toDouble(),
          gridData: const FlGridData(show: false),
          borderData: FlBorderData(show: false),
          barTouchData: BarTouchData(
            enabled: true,
            touchTooltipData: BarTouchTooltipData(
              getTooltipColor: (_) => AppColors.primary,
              getTooltipItem: (group, groupIndex, rod, rodIndex) =>
                  BarTooltipItem(
                '${visible[groupIndex].studyMinutes} dk\n'
                '${visible[groupIndex].sessionCount} oturum',
                const TextStyle(color: Colors.white, fontSize: 11),
              ),
            ),
          ),
          titlesData: FlTitlesData(
            topTitles: const AxisTitles(),
            rightTitles: const AxisTitles(),
            leftTitles: const AxisTitles(),
            bottomTitles: AxisTitles(
              sideTitles: SideTitles(
                showTitles: true,
                reservedSize: 30,
                getTitlesWidget: (value, meta) {
                  final index = value.toInt();
                  if (index < 0 || index >= visible.length) {
                    return const SizedBox.shrink();
                  }
                  return SideTitleWidget(
                    meta: meta,
                    child: Text(
                      labelFormat.format(visible[index].weekStart),
                      style: const TextStyle(
                        color: AppColors.textMuted,
                        fontSize: 9,
                      ),
                    ),
                  );
                },
              ),
            ),
          ),
          barGroups: <BarChartGroupData>[
            for (var i = 0; i < visible.length; i++)
              BarChartGroupData(
                x: i,
                barRods: <BarChartRodData>[
                  BarChartRodData(
                    toY: visible[i].studyMinutes.toDouble(),
                    width: 14,
                    borderRadius: BorderRadius.circular(6),
                    gradient: const LinearGradient(
                      begin: Alignment.bottomCenter,
                      end: Alignment.topCenter,
                      colors: <Color>[AppColors.primary, AppColors.accentTeal],
                    ),
                  ),
                ],
              ),
          ],
        ),
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeOut,
      ),
    );
  }
}

/// M14-3: Ders bazlı ortalama net oranı (0–1 → %) yatay çubuklar.
///
/// Aynı ders birden çok ay taşıyorsa **en güncel ay** gösterilir; delta o ayın
/// bir önceki aya göre skor farkıdır (sunucudan gelir).
class SubjectNetChart extends StatelessWidget {
  const SubjectNetChart({super.key, required this.performance});

  final List<StudentPerformanceMonth> performance;

  @override
  Widget build(BuildContext context) {
    final latest = <String, StudentPerformanceMonth>{};
    for (final row in performance) {
      final current = latest[row.subject];
      final isNewer = current == null ||
          row.year > current.year ||
          (row.year == current.year && row.month > current.month);
      if (isNewer) {
        latest[row.subject] = row;
      }
    }
    if (latest.isEmpty) {
      return const SizedBox.shrink();
    }

    final rows = latest.values.toList()
      ..sort((a, b) => b.averageNet.compareTo(a.averageNet));

    return Column(
      children: <Widget>[
        for (final row in rows) _SubjectNetRow(row: row),
      ],
    );
  }
}

class _SubjectNetRow extends StatelessWidget {
  const _SubjectNetRow({required this.row});

  final StudentPerformanceMonth row;

  @override
  Widget build(BuildContext context) {
    final ratio = row.averageNet.clamp(0, 1).toDouble();
    final color = ratio >= 0.75
        ? AppColors.accentGreen
        : ratio >= 0.5
            ? AppColors.accentBlue
            : ratio >= 0.25
                ? AppColors.accentOrange
                : AppColors.accentRed;

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  row.subject,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w700,
                    fontSize: 13,
                  ),
                ),
              ),
              if (row.delta != 0) ...<Widget>[
                Icon(
                  row.delta > 0
                      ? Icons.trending_up_rounded
                      : Icons.trending_down_rounded,
                  size: 16,
                  color: row.delta > 0
                      ? AppColors.accentGreen
                      : AppColors.accentRed,
                ),
                const SizedBox(width: 3),
                Text(
                  '${row.delta > 0 ? '+' : ''}${row.delta}',
                  style: TextStyle(
                    color: row.delta > 0
                        ? AppColors.accentGreen
                        : AppColors.accentRed,
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(width: 10),
              ],
              Text(
                '%${(ratio * 100).round()}',
                style: TextStyle(
                  color: color,
                  fontWeight: FontWeight.w800,
                  fontSize: 13,
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          // Renk tek başına durum göstergesi değil: yüzde metni yanında duruyor
          // (design_system.md erişilebilirlik ilkesi).
          TweenAnimationBuilder<double>(
            tween: Tween<double>(begin: 0, end: ratio),
            duration: const Duration(milliseconds: 250),
            curve: Curves.easeOut,
            builder: (context, value, _) => ClipRRect(
              borderRadius: BorderRadius.circular(999),
              child: LinearProgressIndicator(
                value: value,
                minHeight: 8,
                backgroundColor: AppColors.divider,
                valueColor: AlwaysStoppedAnimation<Color>(color),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
