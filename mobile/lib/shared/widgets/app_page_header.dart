import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/notifications/presentation/cubit/notifications_cubit.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// Tum ekranlarda ortak kullanilan sayfa basligi.
///
/// Sol tarafta baslik (+ istege bagli alt baslik), sag tarafta bildirim
/// zili bulunur. Zil her zaman `/notifications` ekranina gider ve uzerindeki
/// rozet [NotificationsCubit] uzerindeki okunmamis bildirim sayisini gosterir.
/// Header tek yerde tanimlandigi icin buradaki bir degisiklik tum sayfalara
/// yansir.
class AppPageHeader extends StatelessWidget {
  const AppPageHeader({
    super.key,
    required this.title,
    this.subtitle,
    this.trailing,
  });

  /// Ana baslik (ogretmen adi veya sayfa adi).
  final String title;

  /// Istege bagli alt baslik (or. "Takvim").
  final String? subtitle;

  /// Bildirim zilinden once gosterilecek istege bagli ek aksiyon.
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final unread = context.select<NotificationsCubit, int>(
      (cubit) => cubit.state.unreadCount,
    );

    return Row(
      children: <Widget>[
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                title,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                  color: AppColors.textPrimary,
                  fontWeight: FontWeight.w800,
                ),
              ),
              if (subtitle != null) ...<Widget>[
                const SizedBox(height: 2),
                Text(
                  subtitle!,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ],
          ),
        ),
        if (trailing != null) ...<Widget>[trailing!, const SizedBox(width: 10)],
        _NotificationBell(
          count: unread,
          onTap: () => context.push('/notifications'),
        ),
      ],
    );
  }
}

class _NotificationBell extends StatelessWidget {
  const _NotificationBell({required this.count, required this.onTap});

  final int count;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Stack(
        clipBehavior: Clip.none,
        children: <Widget>[
          Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: AppColors.border),
            ),
            child: const Icon(
              Icons.notifications_none_rounded,
              color: AppColors.textPrimary,
            ),
          ),
          if (count > 0)
            Positioned(
              top: -3,
              right: -3,
              child: Container(
                constraints: const BoxConstraints(minWidth: 18),
                padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 2),
                decoration: BoxDecoration(
                  color: AppColors.accentRed,
                  borderRadius: BorderRadius.circular(999),
                  border: Border.all(color: AppColors.surface, width: 2),
                ),
                child: Text(
                  count > 99 ? '99+' : '$count',
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.labelSmall?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                    fontSize: 10,
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}
