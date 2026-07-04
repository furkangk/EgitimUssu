import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// Veli paneline özgü alt navigasyon. Öğretmen [AppBottomNav]'inden ayrıdır çünkü
/// veli sekmeleri farklıdır (Ana Sayfa · Çocuklar · Bildirimler · Profil).
enum ParentNavTab {
  home(Icons.home_rounded, 'Ana Sayfa', '/parent'),
  children(Icons.family_restroom_rounded, 'Çocuklar', '/parent/children'),
  notifications(Icons.notifications_rounded, 'Bildirim', '/parent/notifications'),
  profile(Icons.person_rounded, 'Profil', '/parent/profile'),
  none(Icons.circle, '', '');

  const ParentNavTab(this.icon, this.label, this.route);

  final IconData icon;
  final String label;
  final String route;
}

class ParentBottomNav extends StatelessWidget {
  const ParentBottomNav({super.key, required this.current});

  final ParentNavTab current;

  static const List<ParentNavTab> _tabs = <ParentNavTab>[
    ParentNavTab.home,
    ParentNavTab.children,
    ParentNavTab.notifications,
    ParentNavTab.profile,
  ];

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: AppColors.surface,
        border: Border(top: BorderSide(color: AppColors.divider)),
      ),
      padding: EdgeInsets.fromLTRB(
        10,
        8,
        10,
        MediaQuery.of(context).padding.bottom + 8,
      ),
      child: Row(
        children: _tabs.map((tab) {
          final selected = tab == current;
          return Expanded(
            child: InkWell(
              borderRadius: BorderRadius.circular(18),
              onTap: selected ? null : () => context.go(tab.route),
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 8),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Icon(
                      tab.icon,
                      color: selected
                          ? AppColors.primary
                          : AppColors.textSecondary,
                    ),
                    const SizedBox(height: 4),
                    FittedBox(
                      fit: BoxFit.scaleDown,
                      child: Text(
                        tab.label,
                        maxLines: 1,
                        style: Theme.of(context).textTheme.labelMedium?.copyWith(
                          color: selected
                              ? AppColors.primary
                              : AppColors.textSecondary,
                          fontWeight: selected
                              ? FontWeight.w800
                              : FontWeight.w600,
                          fontSize: 11,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        }).toList(),
      ),
    );
  }
}

/// Sayfa üst başlığı (veli paneli). Basit; öğretmen [AppPageHeader]'ından bağımsız.
class ParentHeader extends StatelessWidget {
  const ParentHeader({super.key, required this.title, this.subtitle});

  final String title;
  final String? subtitle;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 14, 20, 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            title,
            style: theme.textTheme.headlineSmall?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          if (subtitle != null) ...<Widget>[
            const SizedBox(height: 4),
            Text(
              subtitle!,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// Kart kabuğu — başlık + isteğe bağlı ikon + içerik.
class ParentCard extends StatelessWidget {
  const ParentCard({
    super.key,
    required this.child,
    this.title,
    this.icon,
    this.iconColor,
    this.padding = const EdgeInsets.all(16),
  });

  final Widget child;
  final String? title;
  final IconData? icon;
  final Color? iconColor;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: padding,
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.soft,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          if (title != null) ...<Widget>[
            Row(
              children: <Widget>[
                if (icon != null) ...<Widget>[
                  Icon(icon, size: 18, color: iconColor ?? AppColors.primary),
                  const SizedBox(width: 8),
                ],
                Text(
                  title!,
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: AppColors.textPrimary,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
          ],
          child,
        ],
      ),
    );
  }
}

/// Küçük KPI kutucuğu — ikon + değer + etiket.
class ParentStatTile extends StatelessWidget {
  const ParentStatTile({
    super.key,
    required this.icon,
    required this.value,
    required this.label,
    required this.accent,
  });

  final IconData icon;
  final String value;
  final String label;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
        boxShadow: AppShadows.soft,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: accent.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(icon, color: accent, size: 20),
          ),
          const SizedBox(height: 10),
          Text(
            value,
            style: theme.textTheme.titleLarge?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            style: theme.textTheme.bodySmall?.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }
}

/// Durum rozeti (bağ durumu için renk kodlu).
class ParentStatusBadge extends StatelessWidget {
  const ParentStatusBadge({super.key, required this.status});

  final String status;

  @override
  Widget build(BuildContext context) {
    late final Color color;
    late final String label;
    switch (status) {
      case 'Approved':
        color = AppColors.accentGreen;
        label = 'Onaylı';
        break;
      case 'Pending':
        color = AppColors.accentOrange;
        label = 'Onay bekliyor';
        break;
      case 'Rejected':
        color = AppColors.accentRed;
        label = 'Reddedildi';
        break;
      default:
        color = AppColors.textSecondary;
        label = 'İptal edildi';
    }
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(
          color: color,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

/// Basit haftalık çubuk grafik (fl_chart bağımlılığı olmadan; sağlam & hafif).
class ParentWeeklyBars extends StatelessWidget {
  const ParentWeeklyBars({super.key, required this.minutesPerDay});

  final List<int> minutesPerDay;

  static const List<String> _labels = <String>[
    'Pzt',
    'Sal',
    'Çar',
    'Per',
    'Cum',
    'Cmt',
    'Paz',
  ];

  @override
  Widget build(BuildContext context) {
    final maxMinutes = minutesPerDay.isEmpty
        ? 1
        : minutesPerDay.reduce((a, b) => a > b ? a : b);
    final safeMax = maxMinutes == 0 ? 1 : maxMinutes;
    return SizedBox(
      height: 130,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.end,
        children: List<Widget>.generate(7, (index) {
          final minutes = index < minutesPerDay.length
              ? minutesPerDay[index]
              : 0;
          final ratio = minutes / safeMax;
          return Expanded(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.end,
                children: <Widget>[
                  Text(
                    '$minutes',
                    style: Theme.of(context).textTheme.labelSmall?.copyWith(
                      color: AppColors.textMuted,
                      fontSize: 9,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Container(
                    height: (90 * ratio).clamp(4, 90).toDouble(),
                    decoration: BoxDecoration(
                      gradient: const LinearGradient(
                        begin: Alignment.bottomCenter,
                        end: Alignment.topCenter,
                        colors: <Color>[
                          AppColors.primary,
                          AppColors.secondary,
                        ],
                      ),
                      borderRadius: BorderRadius.circular(6),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    _labels[index],
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
    );
  }
}

String formatMinutes(int minutes) {
  if (minutes <= 0) return '0 dk';
  final h = minutes ~/ 60;
  final m = minutes % 60;
  if (h == 0) return '$m dk';
  if (m == 0) return '$h sa';
  return '$h sa $m dk';
}
