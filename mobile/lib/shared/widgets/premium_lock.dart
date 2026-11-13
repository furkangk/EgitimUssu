import 'dart:ui';

import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// Paywall rotası — kilit kartlarının tek çıkış noktası.
const String kPaywallRoute = '/membership/paywall';

/// Kilitli içeriğin bulanık önizlemesi — testin "tıklanamaz" iddiasını
/// Material'ın kendi `IgnorePointer`'larından ayırt edebilmesi için.
const Key premiumLockPreviewKey = ValueKey<String>('premium_lock_preview');

/// D-10/K-09: Premium'a özel bir içeriği sarar.
///
/// Premium kullanıcıda [child] **doğrudan** çizilir. Free kullanıcıda içerik bulanık ve
/// tıklanamaz hâle gelir, üstüne [PremiumLockCard] konur.
///
/// Kilit kararı `MembershipCubit`'ten (`GET /api/membership/me`) okunur — ekranlar kendi
/// başına "bu kullanıcı premium mu" hesabı yapmaz. Bu, K-09'un ("entitlement tek noktadan")
/// mobil karşılığıdır.
///
/// ⚠️ Bu widget yalnız **sunucunun gerçekten uyguladığı** kapıların önünde kullanılır.
/// Sunucu bir özelliği kısıtlamıyorsa istemcide kilitlemek, olmayan bir kuralı uydurmak olur.
class PremiumLock extends StatelessWidget {
  const PremiumLock({
    super.key,
    required this.child,
    this.message = 'Bu özellik Premium üyeliğe özeldir.',
  });

  final Widget child;

  /// Kilit kartında gösterilecek açıklama.
  final String message;

  @override
  Widget build(BuildContext context) {
    final isPremium = context.select(
      (MembershipCubit cubit) => cubit.state.isPremium,
    );

    if (isPremium) {
      return child;
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        // Önizleme: kullanıcı ne kazanacağını görür ama kullanamaz.
        IgnorePointer(
          key: premiumLockPreviewKey,
          child: ImageFiltered(
            imageFilter: ImageFilter.blur(sigmaX: 2.6, sigmaY: 2.6),
            child: Opacity(opacity: 0.55, child: child),
          ),
        ),
        const SizedBox(height: 12),
        PremiumLockCard(message: message),
      ],
    );
  }
}

/// Kilit kartı: sebep + "Premium'a geç" düğmesi.
///
/// `MembershipCubit`'e **bağlı değildir**; sunucudan `402 *_premium_required` alan ekranlar
/// (PDF rapor, hedef net takibi) kendi hata mesajlarıyla doğrudan bu kartı gösterir.
class PremiumLockCard extends StatelessWidget {
  const PremiumLockCard({
    super.key,
    required this.message,
    this.title = 'Premium üyelik gerekiyor',
  });

  final String message;
  final String title;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.amber.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.amber.withValues(alpha: 0.35)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Row(
            children: <Widget>[
              const Icon(
                Icons.workspace_premium_rounded,
                color: AppColors.amber,
                size: 20,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  title,
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            message,
            style: Theme.of(
              context,
            ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: FilledButton.icon(
              onPressed: () => context.push(kPaywallRoute),
              icon: const Icon(Icons.lock_open_rounded, size: 18),
              label: const Text("Premium'a geç"),
            ),
          ),
        ],
      ),
    );
  }
}
