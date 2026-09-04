import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/premium_features.dart';
import 'package:flutter/material.dart';

/// Üyelik ve paywall ekranlarının ortak iskeleti.
class MembershipScaffold extends StatelessWidget {
  const MembershipScaffold({super.key, required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: Text(title)),
      body: SafeArea(child: child),
    );
  }
}

/// Beyaz, kenarlıklı bölüm kartı.
class MembershipPanel extends StatelessWidget {
  const MembershipPanel({super.key, required this.child, this.padding});

  final Widget child;
  final EdgeInsetsGeometry? padding;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: padding ?? const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.border),
      ),
      child: child,
    );
  }
}

/// Bölüm başlığı.
class MembershipSectionTitle extends StatelessWidget {
  const MembershipSectionTitle(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(4, 18, 4, 8),
      child: Text(
        text,
        style: Theme.of(context).textTheme.titleSmall?.copyWith(
          color: AppColors.textPrimary,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

/// Free/Premium rozeti.
class MembershipTierBadge extends StatelessWidget {
  const MembershipTierBadge({super.key, required this.isPremium});

  final bool isPremium;

  @override
  Widget build(BuildContext context) {
    final color = isPremium ? AppColors.amber : AppColors.textSecondary;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: color.withValues(alpha: 0.35)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(
            isPremium
                ? Icons.workspace_premium_rounded
                : Icons.person_outline_rounded,
            size: 16,
            color: color,
          ),
          const SizedBox(width: 6),
          Text(
            isPremium ? 'Premium' : 'Ücretsiz',
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }
}

/// Paywall plan kartı: fiyat, periyot, deneme rozeti ve eylemler.
///
/// Satın alma düğmesi **bilinçli olarak pasiftir**: `POST /api/membership/checkout` ucu
/// P09 Task 4'te (iyzico, Q1'e bloke) gelecek. Deneme düğmesi ise bugün gerçekten çalışır.
class PlanCard extends StatelessWidget {
  const PlanCard({
    super.key,
    required this.plan,
    required this.canStartTrial,
    required this.isBusy,
    required this.onStartTrial,
  });

  final MembershipPlan plan;

  /// Kullanıcı denemeye hak kazanıyor mu (plan deneme taşıyor **ve** hâlâ Free).
  final bool canStartTrial;
  final bool isBusy;
  final VoidCallback onStartTrial;

  @override
  Widget build(BuildContext context) {
    final periodLabel = plan.isYearly ? 'yıllık' : 'aylık';

    return MembershipPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Text(
                  plan.isYearly ? 'Yıllık Premium' : 'Aylık Premium',
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              if (plan.hasTrial)
                Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 4,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.accentGreen.withValues(alpha: 0.14),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    plan.trialDays >= 30
                        ? 'İlk ay ücretsiz'
                        : '${plan.trialDays} gün ücretsiz',
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                      color: AppColors.accentGreen,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            '${plan.price.toStringAsFixed(0)} ${plan.currency} / $periodLabel',
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
              color: AppColors.primary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 14),
          if (canStartTrial) ...<Widget>[
            SizedBox(
              width: double.infinity,
              child: FilledButton.icon(
                onPressed: isBusy ? null : onStartTrial,
                icon: const Icon(Icons.play_circle_outline_rounded, size: 18),
                label: Text('${plan.trialDays} gün ücretsiz dene'),
              ),
            ),
            const SizedBox(height: 8),
          ],
          SizedBox(
            width: double.infinity,
            child: OutlinedButton.icon(
              // Ödeme entegrasyonu (P09 Task 4 / K-05) gelmeden düğme açılmaz —
              // olmayan bir uca istek atmak sessiz başarısızlık olurdu.
              onPressed: null,
              icon: const Icon(Icons.credit_card_outlined, size: 18),
              label: const Text('Satın al (yakında)'),
            ),
          ),
        ],
      ),
    );
  }
}

/// PRD §9'dan türeyen Free/Premium karşılaştırma tablosu.
class FeatureComparisonTable extends StatelessWidget {
  const FeatureComparisonTable({super.key, required this.rows});

  final List<PremiumFeatureRow> rows;

  @override
  Widget build(BuildContext context) {
    final labelStyle = Theme.of(
      context,
    ).textTheme.labelSmall?.copyWith(color: AppColors.textMuted);

    return MembershipPanel(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
      child: Column(
        children: <Widget>[
          Row(
            children: <Widget>[
              const Expanded(flex: 4, child: SizedBox.shrink()),
              Expanded(
                flex: 3,
                child: Text('Ücretsiz', textAlign: TextAlign.end, style: labelStyle),
              ),
              Expanded(
                flex: 3,
                child: Text('Premium', textAlign: TextAlign.end, style: labelStyle),
              ),
            ],
          ),
          for (final row in rows) ...<Widget>[
            const Divider(height: 18, color: AppColors.border),
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Expanded(
                  flex: 4,
                  child: Text(
                    row.title,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                Expanded(
                  flex: 3,
                  child: Text(
                    row.free,
                    textAlign: TextAlign.end,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
                Expanded(
                  flex: 3,
                  child: Text(
                    row.premium,
                    textAlign: TextAlign.end,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.accentGreen,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
          ],
          const SizedBox(height: 8),
        ],
      ),
    );
  }
}
