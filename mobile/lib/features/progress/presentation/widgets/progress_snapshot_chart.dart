import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/progress/domain/progress_contracts.dart';
import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

/// M10-1: Ders bazlı hâkimiyet skorunun dönemler boyunca çizgi grafiği.
///
/// Her ders bir çizgidir; renkler `design_system.md` aksan token'larından sırayla
/// atanır. Tek noktalı seriler de çizilir (nokta görünür) — "veri var ama trend
/// henüz yok" durumunu boş grafikten ayırt etmek için.
class ProgressSnapshotChart extends StatelessWidget {
  const ProgressSnapshotChart({super.key, required this.seriesBySubject});

  final Map<String, List<ProgressSnapshot>> seriesBySubject;

  /// Ders sayısı token sayısını aşarsa baştan döner; renk yine deterministiktir.
  static const List<Color> _palette = <Color>[
    AppColors.primary,
    AppColors.accentGreen,
    AppColors.accentOrange,
    AppColors.purple,
    AppColors.accentTeal,
    AppColors.accentRed,
  ];

  @override
  Widget build(BuildContext context) {
    final subjects = seriesBySubject.keys.toList()..sort();
    final periods = <DateTime>{
      for (final series in seriesBySubject.values)
        for (final snapshot in series) snapshot.periodStart,
    }.toList()
      ..sort();

    if (periods.isEmpty) {
      return const SizedBox.shrink();
    }

    final labelFormat = DateFormat('d MMM', 'tr_TR');

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        SizedBox(
          height: 190,
          child: LineChart(
            LineChartData(
              minY: 0,
              maxY: 100,
              minX: 0,
              maxX: (periods.length - 1).toDouble(),
              gridData: FlGridData(
                show: true,
                drawVerticalLine: false,
                horizontalInterval: 25,
                getDrawingHorizontalLine: (_) => const FlLine(
                  color: AppColors.divider,
                  strokeWidth: 1,
                ),
              ),
              borderData: FlBorderData(show: false),
              titlesData: FlTitlesData(
                topTitles: const AxisTitles(),
                rightTitles: const AxisTitles(),
                leftTitles: AxisTitles(
                  sideTitles: SideTitles(
                    showTitles: true,
                    interval: 25,
                    reservedSize: 32,
                    getTitlesWidget: (value, meta) => SideTitleWidget(
                      meta: meta,
                      child: Text(
                        value.toInt().toString(),
                        style: const TextStyle(
                          color: AppColors.textMuted,
                          fontSize: 10,
                        ),
                      ),
                    ),
                  ),
                ),
                bottomTitles: AxisTitles(
                  sideTitles: SideTitles(
                    showTitles: true,
                    interval: 1,
                    reservedSize: 28,
                    getTitlesWidget: (value, meta) {
                      final index = value.round();
                      if (index < 0 || index >= periods.length) {
                        return const SizedBox.shrink();
                      }
                      return SideTitleWidget(
                        meta: meta,
                        child: Text(
                          labelFormat.format(periods[index]),
                          style: const TextStyle(
                            color: AppColors.textMuted,
                            fontSize: 10,
                          ),
                        ),
                      );
                    },
                  ),
                ),
              ),
              lineTouchData: const LineTouchData(enabled: false),
              lineBarsData: <LineChartBarData>[
                for (var i = 0; i < subjects.length; i++)
                  _barFor(
                    seriesBySubject[subjects[i]]!,
                    periods,
                    _palette[i % _palette.length],
                  ),
              ],
            ),
            // animations.md: kart/veri geçişleri 250ms + easeOut.
            duration: const Duration(milliseconds: 250),
            curve: Curves.easeOut,
          ),
        ),
        const SizedBox(height: 10),
        Wrap(
          spacing: 12,
          runSpacing: 6,
          children: <Widget>[
            for (var i = 0; i < subjects.length; i++)
              _LegendDot(
                label: subjects[i],
                color: _palette[i % _palette.length],
              ),
          ],
        ),
      ],
    );
  }

  static LineChartBarData _barFor(
    List<ProgressSnapshot> series,
    List<DateTime> periods,
    Color color,
  ) {
    return LineChartBarData(
      isCurved: series.length > 2,
      color: color,
      barWidth: 3,
      dotData: FlDotData(
        show: true,
        getDotPainter: (spot, percent, bar, index) => FlDotCirclePainter(
          radius: 3,
          color: color,
          strokeWidth: 0,
        ),
      ),
      belowBarData: BarAreaData(
        show: true,
        color: color.withValues(alpha: 0.08),
      ),
      spots: <FlSpot>[
        for (final snapshot in series)
          FlSpot(
            periods.indexOf(snapshot.periodStart).toDouble(),
            snapshot.masteryScore.toDouble(),
          ),
      ],
    );
  }
}

class _LegendDot extends StatelessWidget {
  const _LegendDot({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 10,
          height: 10,
          decoration: BoxDecoration(color: color, shape: BoxShape.circle),
        ),
        const SizedBox(width: 6),
        Text(
          label,
          style: const TextStyle(color: AppColors.textSecondary, fontSize: 12),
        ),
      ],
    );
  }
}
