import 'dart:math' as math;

import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/payments/domain/payment_contracts.dart';
import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

/// Finans (ödemeler) sayfası grafik kartları — sunucu **özetinden** beslenir
/// (`PaymentSummary`), böylece sayfalama açıkken de tüm veriye ihtiyaç duymaz.

// ── Aylık tahsilat çubuk grafiği ─────────────────────────────────────────────

/// Son aylara ait **beklenen vs tahsil edilen** tutarı hafif, bağımlılıksız
/// çubuk grafikle gösterir (arka plan = beklenen, dolu = tahsil edilen).
class MonthlyCollectionCard extends StatelessWidget {
  const MonthlyCollectionCard({super.key, required this.points});

  final List<PaymentMonthlyPoint> points;

  static const List<String> _monthShort = <String>[
    'Oca', 'Şub', 'Mar', 'Nis', 'May', 'Haz',
    'Tem', 'Ağu', 'Eyl', 'Eki', 'Kas', 'Ara',
  ];

  @override
  Widget build(BuildContext context) {
    final maxVal = points.fold<double>(0, (m, p) => math.max(m, p.expectedAmount));
    final totalCollected = points.fold<double>(
      0,
      (t, p) => t + p.collectedAmount,
    );

    return _ChartCard(
      title: 'Aylık Tahsilat',
      subtitle: 'Son ${points.length} ay · toplam ${_money(totalCollected)}',
      trailing: const _LegendDots(),
      child: SizedBox(
        height: 150,
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: List<Widget>.generate(points.length, (i) {
            final point = points[i];
            final expectedRatio = maxVal <= 0
                ? 0.0
                : point.expectedAmount / maxVal;
            final collectedRatio = maxVal <= 0
                ? 0.0
                : point.collectedAmount / maxVal;
            const barMax = 104.0;
            return Expanded(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 5),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: <Widget>[
                    Text(
                      point.collectedAmount > 0
                          ? _compact(point.collectedAmount)
                          : '',
                      maxLines: 1,
                      style: Theme.of(context).textTheme.labelSmall?.copyWith(
                        color: AppColors.textSecondary,
                        fontWeight: FontWeight.w700,
                        fontSize: 9,
                      ),
                    ),
                    const SizedBox(height: 4),
                    Container(
                      height: (barMax * expectedRatio).clamp(3, barMax),
                      width: double.infinity,
                      alignment: Alignment.bottomCenter,
                      decoration: BoxDecoration(
                        color: AppColors.primaryLight,
                        borderRadius: BorderRadius.circular(7),
                      ),
                      child: Container(
                        height: (barMax * collectedRatio).clamp(0, barMax),
                        width: double.infinity,
                        decoration: BoxDecoration(
                          gradient: const LinearGradient(
                            begin: Alignment.bottomCenter,
                            end: Alignment.topCenter,
                            colors: <Color>[
                              AppColors.primary,
                              AppColors.accentBlue,
                            ],
                          ),
                          borderRadius: BorderRadius.circular(7),
                        ),
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      point.month >= 1 && point.month <= 12
                          ? _monthShort[point.month - 1]
                          : '',
                      style: Theme.of(context).textTheme.labelSmall?.copyWith(
                        color: AppColors.textSecondary,
                        fontSize: 10,
                      ),
                    ),
                  ],
                ),
              ),
            );
          }),
        ),
      ),
    );
  }
}

class _LegendDots extends StatelessWidget {
  const _LegendDots();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: const <Widget>[
        _Dot(color: AppColors.primary),
        SizedBox(width: 4),
        _LegendText('Tahsil'),
        SizedBox(width: 10),
        _Dot(color: AppColors.primaryLight),
        SizedBox(width: 4),
        _LegendText('Beklenen'),
      ],
    );
  }
}

// ── Durum dağılımı donut grafiği ─────────────────────────────────────────────

/// Ödemelerin tutar bazında dağılımını (tahsil edilen / bekleyen / geciken)
/// donut grafikle gösterir; ortada genel **tahsilat oranı** yer alır.
class PaymentDistributionCard extends StatelessWidget {
  const PaymentDistributionCard({super.key, required this.summary});

  final PaymentSummary summary;

  @override
  Widget build(BuildContext context) {
    final collected = summary.collectedTotal;
    final pending = summary.pendingTotal;
    final overdue = summary.overdueTotal;
    final total = collected + pending + overdue;
    final rate = total <= 0 ? 0.0 : (collected / total) * 100;

    final segments = <_DonutSegment>[
      _DonutSegment(collected, AppColors.accentGreen),
      _DonutSegment(pending, AppColors.amber),
      _DonutSegment(overdue, AppColors.accentRed),
    ];

    return _ChartCard(
      title: 'Durum Dağılımı',
      subtitle: 'Tutar bazında',
      child: Row(
        children: <Widget>[
          SizedBox(
            width: 128,
            height: 128,
            child: CustomPaint(
              painter: _DonutPainter(segments: segments),
              child: Center(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      '%${rate.round()}',
                      style: Theme.of(context).textTheme.titleLarge?.copyWith(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                    Text(
                      'tahsilat',
                      style: Theme.of(context).textTheme.labelSmall?.copyWith(
                        color: AppColors.textSecondary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(width: 18),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                _LegendRow(
                  color: AppColors.accentGreen,
                  label: 'Tahsil edilen',
                  value: _money(collected),
                ),
                const SizedBox(height: 12),
                _LegendRow(
                  color: AppColors.amber,
                  label: 'Bekleyen',
                  value: _money(pending),
                ),
                const SizedBox(height: 12),
                _LegendRow(
                  color: AppColors.accentRed,
                  label: 'Geciken',
                  value: _money(overdue),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _DonutSegment {
  const _DonutSegment(this.value, this.color);
  final double value;
  final Color color;
}

class _DonutPainter extends CustomPainter {
  _DonutPainter({required this.segments});

  final List<_DonutSegment> segments;

  @override
  void paint(Canvas canvas, Size size) {
    const stroke = 18.0;
    final rect = Offset.zero & size;
    final center = rect.center;
    final radius = (math.min(size.width, size.height) - stroke) / 2;
    final arcRect = Rect.fromCircle(center: center, radius: radius);

    final track = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke
      ..color = AppColors.divider;
    canvas.drawCircle(center, radius, track);

    final total = segments.fold<double>(0, (t, s) => t + s.value);
    if (total <= 0) return;

    var start = -math.pi / 2;
    for (final seg in segments) {
      if (seg.value <= 0) continue;
      final sweep = (seg.value / total) * 2 * math.pi;
      final paint = Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = stroke
        ..strokeCap = StrokeCap.butt
        ..color = seg.color;
      canvas.drawArc(arcRect, start, sweep, false, paint);
      start += sweep;
    }
  }

  @override
  bool shouldRepaint(_DonutPainter oldDelegate) =>
      oldDelegate.segments != segments;
}

class _LegendRow extends StatelessWidget {
  const _LegendRow({
    required this.color,
    required this.label,
    required this.value,
  });

  final Color color;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        _Dot(color: color),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            label,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: AppColors.textSecondary,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
        Text(
          value,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(
            color: AppColors.textPrimary,
            fontWeight: FontWeight.w800,
          ),
        ),
      ],
    );
  }
}

// ── Ortak kart + küçük parçalar ──────────────────────────────────────────────

class _ChartCard extends StatelessWidget {
  const _ChartCard({
    required this.title,
    required this.subtitle,
    required this.child,
    this.trailing,
  });

  final String title;
  final String subtitle;
  final Widget child;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.soft,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      title,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      subtitle,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
              if (trailing != null) trailing!,
            ],
          ),
          const SizedBox(height: 16),
          child,
        ],
      ),
    );
  }
}

class _Dot extends StatelessWidget {
  const _Dot({required this.color});
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 10,
      height: 10,
      decoration: BoxDecoration(
        color: color,
        shape: BoxShape.circle,
        border: Border.all(color: AppColors.border),
      ),
    );
  }
}

class _LegendText extends StatelessWidget {
  const _LegendText(this.text);
  final String text;

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: Theme.of(context).textTheme.labelSmall?.copyWith(
        color: AppColors.textSecondary,
        fontWeight: FontWeight.w600,
      ),
    );
  }
}

String _money(double amount) {
  final formatted = NumberFormat.decimalPattern('tr_TR').format(amount.round());
  return '$formatted TRY';
}

/// Çubuk etiketleri için kısa gösterim: 1500 → "1,5B", 2.000.000 → "2M".
String _compact(double amount) {
  if (amount >= 1000000) {
    return '${(amount / 1000000).toStringAsFixed(amount % 1000000 == 0 ? 0 : 1).replaceAll('.', ',')}M';
  }
  if (amount >= 1000) {
    return '${(amount / 1000).toStringAsFixed(amount % 1000 == 0 ? 0 : 1).replaceAll('.', ',')}B';
  }
  return amount.round().toString();
}
