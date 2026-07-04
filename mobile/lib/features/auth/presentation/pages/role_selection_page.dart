import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class RoleSelectionPage extends StatelessWidget {
  const RoleSelectionPage({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    const roles = <_RoleCardData>[
      _RoleCardData(
        title: 'Ogretmen',
        description: 'Derslerinizi yonetin, ogrencilerinizi takip edin.',
        icon: Icons.history_edu_rounded,
        accent: AppColors.accentGreen,
        keyName: 'ogretmen',
      ),
      _RoleCardData(
        title: 'Ogrenci',
        description: 'Calisma surecinizi planlayin, gelisiminizi izleyin.',
        icon: Icons.auto_stories_rounded,
        accent: AppColors.accentBlue,
        keyName: 'ogrenci',
      ),
      _RoleCardData(
        title: 'Veli',
        description: 'Cocugunuzun ilerlemesini sade ozetlerle takip edin.',
        icon: Icons.family_restroom_rounded,
        accent: AppColors.accentOrange,
        keyName: 'veli',
      ),
    ];

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 430),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(20, 24, 20, 28),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  const Spacer(flex: 3),
                  Text(
                    'Hesap turunu secin',
                    textAlign: TextAlign.center,
                    style: theme.textTheme.headlineSmall?.copyWith(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Size en uygun deneyimi yasatalim.',
                    textAlign: TextAlign.center,
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: AppColors.textSecondary,
                      height: 1.5,
                    ),
                  ),
                  const SizedBox(height: 32),
                  for (final role in roles) ...<Widget>[
                    _RoleCard(
                      data: role,
                      onTap: () {
                        if (role.keyName == 'ogretmen' ||
                            role.keyName == 'veli') {
                          context.go('/register?role=${role.keyName}');
                          return;
                        }
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(
                            content: Text(
                              '${role.title} deneyimi yakinda aktif olacak.',
                            ),
                          ),
                        );
                      },
                    ),
                    if (role != roles.last) const SizedBox(height: 14),
                  ],
                  const SizedBox(height: 18),
                  TextButton(
                    onPressed: () => context.go('/login?role=ogretmen'),
                    child: RichText(
                      text: TextSpan(
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: AppColors.textSecondary,
                          fontWeight: FontWeight.w400,
                        ),
                        children: <InlineSpan>[
                          const TextSpan(text: 'Zaten hesabim var '),
                          TextSpan(
                            text: 'Giris Yap',
                            style: theme.textTheme.bodyMedium?.copyWith(
                              color: AppColors.primary,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                  const Spacer(flex: 2),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _RoleCard extends StatelessWidget {
  const _RoleCard({required this.data, required this.onTap});

  final _RoleCardData data;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Material(
      color: Colors.transparent,
      child: InkWell(
        borderRadius: BorderRadius.circular(20),
        onTap: onTap,
        child: Ink(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 18),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: AppColors.border),
            boxShadow: AppShadows.soft,
          ),
          child: Row(
            children: <Widget>[
              Container(
                width: 58,
                height: 58,
                decoration: BoxDecoration(
                  color: data.accent.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Icon(data.icon, color: data.accent, size: 30),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      data.title,
                      style: theme.textTheme.titleLarge?.copyWith(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      data.description,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: AppColors.textSecondary,
                        height: 1.5,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 10),
              const Icon(
                Icons.arrow_forward_ios_rounded,
                size: 18,
                color: AppColors.textSecondary,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _RoleCardData {
  const _RoleCardData({
    required this.title,
    required this.description,
    required this.icon,
    required this.accent,
    required this.keyName,
  });

  final String title;
  final String description;
  final IconData icon;
  final Color accent;
  final String keyName;
}
