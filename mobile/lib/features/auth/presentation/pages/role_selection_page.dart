import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class RoleSelectionPage extends StatelessWidget {
  const RoleSelectionPage({super.key});

  static const _primary = Color(0xFF082B4F);
  static const _background = Color(0xFFF7F9FC);
  static const _surface = Color(0xFFFFFFFF);
  static const _textPrimary = Color(0xFF111827);
  static const _textSecondary = Color(0xFF6B7280);
  static const _border = Color(0xFFE5E7EB);

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    const roles = <_RoleCardData>[
      _RoleCardData(
        title: 'Ogretmen',
        description: 'Derslerinizi yonetin, ogrencilerinizi takip edin.',
        icon: Icons.history_edu_rounded,
        accent: Color(0xFF20B486),
        keyName: 'ogretmen',
      ),
      _RoleCardData(
        title: 'Ogrenci',
        description: 'Calisma surecinizi planlayin, gelisiminizi izleyin.',
        icon: Icons.auto_stories_rounded,
        accent: Color(0xFF3D8BFF),
        keyName: 'ogrenci',
      ),
      _RoleCardData(
        title: 'Veli',
        description: 'Cocugunuzun ilerlemesini sade ozetlerle takip edin.',
        icon: Icons.family_restroom_rounded,
        accent: Color(0xFFFFA726),
        keyName: 'veli',
      ),
    ];

    return Scaffold(
      backgroundColor: _background,
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
                      color: _textPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Size en uygun deneyimi yasatalim.',
                    textAlign: TextAlign.center,
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: _textSecondary,
                      height: 1.5,
                    ),
                  ),
                  const SizedBox(height: 32),
                  for (final role in roles) ...<Widget>[
                    _RoleCard(
                      data: role,
                      onTap: () {
                        if (role.keyName == 'ogretmen') {
                          context.go('/register');
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
                          color: _textSecondary,
                          fontWeight: FontWeight.w400,
                        ),
                        children: <InlineSpan>[
                          const TextSpan(text: 'Zaten hesabim var '),
                          TextSpan(
                            text: 'Giris Yap',
                            style: theme.textTheme.bodyMedium?.copyWith(
                              color: _primary,
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
            color: RoleSelectionPage._surface,
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: RoleSelectionPage._border),
            boxShadow: const <BoxShadow>[
              BoxShadow(
                color: Color(0x0D082B4F),
                blurRadius: 18,
                offset: Offset(0, 10),
              ),
            ],
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
                        color: RoleSelectionPage._textPrimary,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      data.description,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: RoleSelectionPage._textSecondary,
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
                color: RoleSelectionPage._textSecondary,
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
