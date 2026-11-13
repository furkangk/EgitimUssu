import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/premium_features.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/widgets/membership_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

/// D-10: Paywall — rol bazlı Free/Premium karşılaştırması, plan kartları ve kampanyalar.
///
/// Plan kataloğu ucu **anonim**dir; ekran giriş yapılmamışken de fiyatları gösterir.
/// Deneme ve referans akışları bugün gerçekten çalışır; satın alma P09 Task 4'e bağlıdır.
class PaywallPage extends StatelessWidget {
  const PaywallPage({super.key});

  @override
  Widget build(BuildContext context) {
    final roles = context.select(
      (AuthCubit cubit) => cubit.state.session?.roles ?? const <String>[],
    );
    final role = membershipRoleFor(roles);

    return MembershipScaffold(
      title: 'Premium',
      child: BlocConsumer<MembershipCubit, MembershipState>(
        listenWhen: (previous, current) =>
            previous.errorMessage != current.errorMessage ||
            previous.successMessage != current.successMessage,
        listener: (context, state) {
          final message = state.successMessage ?? state.errorMessage;
          if (message == null) {
            return;
          }
          ScaffoldMessenger.of(context)
            ..hideCurrentSnackBar()
            ..showSnackBar(SnackBar(content: Text(message)));
          context.read<MembershipCubit>().clearMessages();
        },
        builder: (context, state) {
          if (state.isLoading) {
            return const LoadingStateView(message: 'Planlar yükleniyor...');
          }

          final cubit = context.read<MembershipCubit>();
          final plans = state.sellablePlans
              .where((plan) => plan.role == role)
              .toList();

          return ListView(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
            children: <Widget>[
              _Header(role: role, isPremium: state.isPremium),
              MembershipSectionTitle(
                '${membershipRoleLabel(role)} paketinde ne değişiyor?',
              ),
              FeatureComparisonTable(
                rows: premiumFeaturesFor(
                  role,
                  freeMaxStudents: state.isPremium
                      ? null
                      : state.entitlement.maxStudents,
                ),
              ),
              const MembershipSectionTitle('Planlar'),
              if (plans.isEmpty)
                const EmptyStateView(
                  title: 'Satışta plan yok',
                  subtitle:
                      'Bu rol için henüz bir üyelik planı tanımlanmamış. Daha sonra tekrar dene.',
                )
              else
                for (final plan in plans) ...<Widget>[
                  PlanCard(
                    plan: plan,
                    // Deneme kullanıcı başına bir kezdir ve zaten Premium olan
                    // birine gösterilmesi anlamsız olurdu.
                    canStartTrial: plan.hasTrial && !state.isPremium,
                    isBusy: state.isBusy,
                    onStartTrial: () => cubit.startTrial(plan.code),
                  ),
                  const SizedBox(height: 12),
                ],
              const MembershipSectionTitle('Kampanyalar'),
              const _CampaignsPanel(),
              const MembershipSectionTitle('Yakında Premium kapsamında'),
              _RoadmapPanel(items: premiumRoadmapFor(role)),
            ],
          );
        },
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.role, required this.isPremium});

  final String role;
  final bool isPremium;

  @override
  Widget build(BuildContext context) {
    return MembershipPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          MembershipTierBadge(isPremium: isPremium),
          const SizedBox(height: 12),
          Text(
            isPremium
                ? 'Premium üyeliğin aktif'
                : 'EğitimÜssü Premium ile derinleş',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            isPremium
                ? 'Tüm Premium hakların açık. Üyeliğini "Üyelik" ekranından yönetebilirsin.'
                : '${membershipRoleLabel(role)} paketinde limitleri kaldır, '
                      'derinlemesine analiz ve raporlara eriş.',
            style: Theme.of(
              context,
            ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
          ),
        ],
      ),
    );
  }
}

/// Bugün gerçekten çalışan iki kampanya (M17-3): ilk ay ücretsiz + referans.
class _CampaignsPanel extends StatelessWidget {
  const _CampaignsPanel();

  @override
  Widget build(BuildContext context) {
    return const MembershipPanel(
      child: Column(
        children: <Widget>[
          _CampaignRow(
            icon: Icons.card_giftcard_rounded,
            title: 'İlk ay ücretsiz',
            subtitle:
                'Aylık planı ilk kez denerken 30 gün ücretsiz. Kullanıcı başına bir kez.',
          ),
          Divider(height: 20, color: AppColors.border),
          _CampaignRow(
            icon: Icons.group_add_outlined,
            title: 'Arkadaşını getir',
            subtitle:
                'Referans kodunu paylaş; kodu kullanana da sana da 1 ay Premium yazılır. '
                'Kodun "Üyelik" ekranında.',
          ),
        ],
      ),
    );
  }
}

class _CampaignRow extends StatelessWidget {
  const _CampaignRow({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  final IconData icon;
  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Icon(icon, color: AppColors.accentGreen, size: 22),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                title,
                style: Theme.of(context).textTheme.titleSmall?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 3),
              Text(
                subtitle,
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: AppColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// PRD §9'un henüz kapısı olmayan vaatleri — "var" gibi satılmaz, açıkça yakında denir.
class _RoadmapPanel extends StatelessWidget {
  const _RoadmapPanel({required this.items});

  final List<String> items;

  @override
  Widget build(BuildContext context) {
    return MembershipPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          for (final item in items)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                children: <Widget>[
                  const Icon(
                    Icons.schedule_rounded,
                    size: 16,
                    color: AppColors.textMuted,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      item,
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
