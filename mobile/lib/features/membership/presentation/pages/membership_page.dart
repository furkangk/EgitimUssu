import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/membership/domain/membership_contracts.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/widgets/membership_widgets.dart';
import 'package:egitim_ussu_mobile/shared/widgets/premium_lock.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

/// D-10/D-15: Üyelik ekranı — mevcut plan, dönem sonu, iptal ve referans kodu.
///
/// P05'te "Yakında" etiketiyle pasif duran "Üyelik" satırının gerçek karşılığı.
class MembershipPage extends StatefulWidget {
  const MembershipPage({super.key});

  @override
  State<MembershipPage> createState() => _MembershipPageState();
}

class _MembershipPageState extends State<MembershipPage> {
  final TextEditingController _referralInput = TextEditingController();

  @override
  void initState() {
    super.initState();
    // Ekran açılışında hem durum hem de kullanıcının kendi kodu tazelenir.
    final cubit = context.read<MembershipCubit>();
    cubit.refresh();
    cubit.loadReferralCode();
  }

  @override
  void dispose() {
    _referralInput.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MembershipScaffold(
      title: 'Üyelik',
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
          if (state.isLoading && state.status == MembershipStatus.initial) {
            return const LoadingStateView(message: 'Üyelik yükleniyor...');
          }

          return RefreshIndicator(
            onRefresh: context.read<MembershipCubit>().refresh,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
              children: <Widget>[
                _StatusPanel(state: state),
                const MembershipSectionTitle('Referans kodun'),
                _ReferralPanel(state: state),
                const MembershipSectionTitle('Kod kullan'),
                _RedeemPanel(controller: _referralInput, state: state),
              ],
            ),
          );
        },
      ),
    );
  }
}

/// Mevcut üyelik durumu: seviye, plan, dönem sonu ve eylem.
class _StatusPanel extends StatelessWidget {
  const _StatusPanel({required this.state});

  final MembershipState state;

  @override
  Widget build(BuildContext context) {
    final membership = state.membership;
    final cubit = context.read<MembershipCubit>();

    return MembershipPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          MembershipTierBadge(isPremium: membership.isPremium),
          const SizedBox(height: 12),
          Text(
            _headline(membership),
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            _detail(membership),
            style: Theme.of(
              context,
            ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
          ),
          if (membership.planCode != null) ...<Widget>[
            const SizedBox(height: 10),
            _InfoRow(label: 'Plan', value: membership.planCode!),
          ],
          if (!membership.entitlement.hasUnlimitedStudents)
            _InfoRow(
              label: 'Öğrenci limiti',
              value: '${membership.entitlement.maxStudents}',
            ),
          const SizedBox(height: 14),
          if (!membership.isPremium)
            SizedBox(
              width: double.infinity,
              child: FilledButton.icon(
                onPressed: () => context.push(kPaywallRoute),
                icon: const Icon(Icons.workspace_premium_rounded, size: 18),
                label: const Text("Premium'a geç"),
              ),
            )
          else ...<Widget>[
            SizedBox(
              width: double.infinity,
              child: OutlinedButton.icon(
                onPressed: () => context.push(kPaywallRoute),
                icon: const Icon(Icons.list_alt_rounded, size: 18),
                label: const Text('Planları gör'),
              ),
            ),
            // İptal yalnız hâlâ yürüyen bir abonelikte anlamlıdır; iptal edilmiş
            // aboneliği tekrar iptal etmek sunucudan 404 alırdı.
            if (membership.hasSubscription && !membership.isCancelled) ...<Widget>[
              const SizedBox(height: 8),
              SizedBox(
                width: double.infinity,
                child: TextButton(
                  onPressed: state.isBusy
                      ? null
                      : () => _confirmCancel(context, cubit),
                  style: TextButton.styleFrom(
                    foregroundColor: AppColors.accentRed,
                  ),
                  child: const Text('Aboneliği iptal et'),
                ),
              ),
            ],
          ],
        ],
      ),
    );
  }

  String _headline(MembershipSnapshot membership) {
    if (!membership.isPremium) {
      return 'Ücretsiz üyelik';
    }
    if (membership.isTrialing) {
      return 'Ücretsiz deneme sürüyor';
    }
    if (membership.isCancelled) {
      return 'Abonelik iptal edildi';
    }
    return 'Premium üyelik aktif';
  }

  String _detail(MembershipSnapshot membership) {
    final until = membership.effectiveUntilUtc;
    if (!membership.isPremium) {
      return 'Premium haklarına geçerek limitleri kaldırabilirsin.';
    }
    if (until == null) {
      return 'Premium hakların açık.';
    }
    final formatted = DateFormat(
      'd MMMM y',
      'tr_TR',
    ).format(until.toLocal());
    // İptal edilse bile ödenen süre kaybolmaz — sunucudaki kural budur.
    return membership.isCancelled
        ? 'Erişimin $formatted tarihine kadar sürüyor, sonra Ücretsiz üyeliğe döneceksin.'
        : 'Geçerlilik: $formatted';
  }

  Future<void> _confirmCancel(
    BuildContext context,
    MembershipCubit cubit,
  ) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Aboneliği iptal et'),
        content: const Text(
          'Erişimin dönem sonuna kadar sürer, ödediğin süreyi kaybetmezsin. '
          'Devam edilsin mi?',
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Vazgeç'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('İptal et'),
          ),
        ],
      ),
    );

    if (confirmed ?? false) {
      await cubit.cancel();
    }
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 6),
      child: Row(
        children: <Widget>[
          Text(
            label,
            style: Theme.of(
              context,
            ).textTheme.bodySmall?.copyWith(color: AppColors.textMuted),
          ),
          const Spacer(),
          Text(
            value,
            style: Theme.of(context).textTheme.bodySmall?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

/// M17-3: Kullanıcının referans kodu + paylaşım.
class _ReferralPanel extends StatelessWidget {
  const _ReferralPanel({required this.state});

  final MembershipState state;

  @override
  Widget build(BuildContext context) {
    final referral = state.referral;

    if (referral == null) {
      return MembershipPanel(
        child: Row(
          children: <Widget>[
            const Expanded(
              child: Text(
                'Referans kodun henüz yüklenmedi.',
                style: TextStyle(color: AppColors.textSecondary, fontSize: 12),
              ),
            ),
            TextButton(
              onPressed: context.read<MembershipCubit>().loadReferralCode,
              child: const Text('Yükle'),
            ),
          ],
        ),
      );
    }

    return MembershipPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: SelectableText(
                  referral.code,
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 3,
                  ),
                ),
              ),
              IconButton(
                tooltip: 'Kodu kopyala',
                onPressed: () async {
                  await Clipboard.setData(ClipboardData(text: referral.code));
                  if (context.mounted) {
                    ScaffoldMessenger.of(context)
                      ..hideCurrentSnackBar()
                      ..showSnackBar(
                        const SnackBar(content: Text('Kod kopyalandı.')),
                      );
                  }
                },
                icon: const Icon(Icons.copy_rounded),
              ),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'Kodunu paylaş: kodu kullanana da sana da '
            '${referral.rewardMonths} ay Premium yazılır.',
            style: Theme.of(
              context,
            ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
          ),
          const SizedBox(height: 8),
          _InfoRow(
            label: 'Kodu kullanan kişi',
            value: '${referral.redemptionCount}',
          ),
        ],
      ),
    );
  }
}

/// M17-3: Başkasının kodunu kullanma. Ömür boyu **bir kez** çalışır (sunucu kuralı).
class _RedeemPanel extends StatelessWidget {
  const _RedeemPanel({required this.controller, required this.state});

  final TextEditingController controller;
  final MembershipState state;

  @override
  Widget build(BuildContext context) {
    return MembershipPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          TextField(
            controller: controller,
            textCapitalization: TextCapitalization.characters,
            decoration: const InputDecoration(
              labelText: 'Referans kodu',
              hintText: 'Örn. K7MNPQ',
            ),
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: state.isBusy
                  ? null
                  : () async {
                      final ok = await context
                          .read<MembershipCubit>()
                          .redeemReferral(controller.text);
                      if (ok) {
                        controller.clear();
                      }
                    },
              child: const Text('Kodu kullan'),
            ),
          ),
          const SizedBox(height: 6),
          Text(
            'Referans ödülü hesap başına bir kez kullanılabilir ve kendi kodunu kullanamazsın.',
            style: Theme.of(
              context,
            ).textTheme.labelSmall?.copyWith(color: AppColors.textMuted),
          ),
        ],
      ),
    );
  }
}
