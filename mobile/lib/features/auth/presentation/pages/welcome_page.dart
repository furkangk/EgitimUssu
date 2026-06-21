import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

class WelcomePage extends StatelessWidget {
  const WelcomePage({super.key});

  static const _primary = Color(0xFF082B4F);
  static const _primaryDark = Color(0xFF061F3A);
  static const _primaryLight = Color(0xFFEAF2FB);
  static const _background = Color(0xFFF7F9FC);
  static const _surface = Color(0xFFFFFFFF);
  static const _textPrimary = Color(0xFF111827);
  static const _textSecondary = Color(0xFF6B7280);
  static const _border = Color(0xFFE5E7EB);
  static const _accentOrange = Color(0xFFFFA726);
  static const _accentGreen = Color(0xFF20B486);
  static const _accentBlue = Color(0xFF3D8BFF);

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      backgroundColor: _background,
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 430),
            child: Padding(
              padding: const EdgeInsets.fromLTRB(20, 20, 20, 28),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  const _BrandBlock(),
                  const SizedBox(height: 28),
                  const Expanded(child: _WelcomePhoto()),
                  const SizedBox(height: 28),
                  FilledButton(
                    onPressed: () => context.go('/login?role=ogretmen'),
                    style: FilledButton.styleFrom(
                      backgroundColor: _primary,
                      foregroundColor: Colors.white,
                      minimumSize: const Size.fromHeight(56),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(14),
                      ),
                      elevation: 0,
                    ),
                    child: Text(
                      'Giris Yap',
                      style: theme.textTheme.titleMedium?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  OutlinedButton(
                    onPressed: () => context.go('/role-selection'),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: _primary,
                      minimumSize: const Size.fromHeight(56),
                      side: const BorderSide(color: _primary, width: 1.4),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(14),
                      ),
                    ),
                    child: Text(
                      'Kayit Ol',
                      style: theme.textTheme.titleMedium?.copyWith(
                        color: _primary,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _BrandBlock extends StatelessWidget {
  const _BrandBlock();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      children: <Widget>[
        Container(
          width: 78,
          height: 78,
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: <Color>[WelcomePage._primary, WelcomePage._primaryDark],
            ),
            borderRadius: BorderRadius.circular(24),
            boxShadow: const <BoxShadow>[
              BoxShadow(
                color: Color(0x1A082B4F),
                blurRadius: 22,
                offset: Offset(0, 12),
              ),
            ],
          ),
          child: Stack(
            alignment: Alignment.center,
            children: <Widget>[
              Positioned(
                top: 16,
                left: 18,
                child: Container(
                  width: 14,
                  height: 14,
                  decoration: const BoxDecoration(
                    color: WelcomePage._accentOrange,
                    shape: BoxShape.circle,
                  ),
                ),
              ),
              Positioned(
                bottom: 18,
                right: 18,
                child: Container(
                  width: 12,
                  height: 12,
                  decoration: const BoxDecoration(
                    color: WelcomePage._accentGreen,
                    shape: BoxShape.circle,
                  ),
                ),
              ),
              const Icon(Icons.school_rounded, color: Colors.white, size: 34),
            ],
          ),
        ),
        const SizedBox(height: 18),
        Text(
          'EgitimUssu',
          textAlign: TextAlign.center,
          style: theme.textTheme.headlineSmall?.copyWith(
            color: WelcomePage._textPrimary,
            fontWeight: FontWeight.w800,
          ),
        ),
        const SizedBox(height: 8),
        Text(
          'Ozel ders sureclerinizi tek bir yerde yonetin.',
          textAlign: TextAlign.center,
          style: theme.textTheme.bodyMedium?.copyWith(
            color: WelcomePage._textSecondary,
            height: 1.5,
          ),
        ),
      ],
    );
  }
}

class _WelcomePhoto extends StatelessWidget {
  const _WelcomePhoto();

  static const _photoUrl =
      'https://images.unsplash.com/photo-1509062522246-3755977927d7?auto=format&fit=crop&w=1200&q=80';

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: WelcomePage._surface,
        borderRadius: BorderRadius.circular(28),
        border: Border.all(color: WelcomePage._border),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x10082B4F),
            blurRadius: 28,
            offset: Offset(0, 16),
          ),
        ],
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(28),
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            Image.network(
              _photoUrl,
              fit: BoxFit.cover,
              errorBuilder: (context, error, stackTrace) {
                return const _PhotoFallback();
              },
              loadingBuilder: (context, child, loadingProgress) {
                if (loadingProgress == null) {
                  return child;
                }
                return const _PhotoFallback(isLoading: true);
              },
            ),
            const DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  colors: <Color>[
                    Color(0x05082B4F),
                    Color(0x15082B4F),
                    Color(0xAA061F3A),
                  ],
                ),
              ),
            ),
            Positioned(
              left: 20,
              right: 20,
              bottom: 20,
              child: Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.16),
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(
                    color: Colors.white.withValues(alpha: 0.18),
                  ),
                  boxShadow: const <BoxShadow>[
                    BoxShadow(
                      color: Color(0x15000000),
                      blurRadius: 18,
                      offset: Offset(0, 10),
                    ),
                  ],
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      'Ogretmen, ogrenci ve veli deneyimi tek mobil akista.',
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w700,
                        height: 1.35,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Planlama, takip, odev ve gelisim ekranlarini tek merkezden yonetin.',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: Colors.white.withValues(alpha: 0.86),
                        height: 1.5,
                      ),
                    ),
                  ],
                ),
              ),
            ),
            const Positioned(top: 18, right: 18, child: _PhotoBadge()),
          ],
        ),
      ),
    );
  }
}

class _PhotoBadge extends StatelessWidget {
  const _PhotoBadge();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.9),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Container(
            width: 8,
            height: 8,
            decoration: const BoxDecoration(
              color: WelcomePage._accentGreen,
              shape: BoxShape.circle,
            ),
          ),
          const SizedBox(width: 8),
          Text(
            'Canli takip',
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: WelcomePage._primary,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

class _PhotoFallback extends StatelessWidget {
  const _PhotoFallback({this.isLoading = false});

  final bool isLoading;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final visualSize = constraints.maxHeight < 260 ? 112.0 : 168.0;
        final titleStyle = Theme.of(context).textTheme.titleMedium?.copyWith(
          color: WelcomePage._primary,
          fontWeight: FontWeight.w700,
        );

        return DecoratedBox(
          decoration: const BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: <Color>[
                WelcomePage._primaryLight,
                Color(0xFFDCEBFF),
                Color(0xFFF1F5FF),
              ],
            ),
          ),
          child: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              Positioned(
                top: -12,
                right: -8,
                child: Container(
                  width: 124,
                  height: 124,
                  decoration: BoxDecoration(
                    color: WelcomePage._accentBlue.withValues(alpha: 0.14),
                    shape: BoxShape.circle,
                  ),
                ),
              ),
              Positioned(
                left: -18,
                bottom: 36,
                child: Container(
                  width: 116,
                  height: 116,
                  decoration: BoxDecoration(
                    color: WelcomePage._accentOrange.withValues(alpha: 0.12),
                    shape: BoxShape.circle,
                  ),
                ),
              ),
              Center(
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 22),
                  child: FittedBox(
                    fit: BoxFit.scaleDown,
                    child: SizedBox(
                      width: 220,
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: <Widget>[
                          Container(
                            width: visualSize,
                            height: visualSize,
                            decoration: BoxDecoration(
                              color: Colors.white.withValues(alpha: 0.72),
                              borderRadius: BorderRadius.circular(28),
                            ),
                            child: Stack(
                              children: <Widget>[
                                Positioned(
                                  left: 18,
                                  right: 18,
                                  bottom: 18,
                                  child: Container(
                                    height: 14,
                                    decoration: BoxDecoration(
                                      color: WelcomePage._primaryDark
                                          .withValues(alpha: 0.08),
                                      borderRadius: BorderRadius.circular(999),
                                    ),
                                  ),
                                ),
                                const Center(
                                  child: Icon(
                                    Icons.auto_stories_rounded,
                                    size: 56,
                                    color: WelcomePage._primary,
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: 14),
                          if (isLoading)
                            const SizedBox(
                              width: 22,
                              height: 22,
                              child: CircularProgressIndicator(
                                strokeWidth: 2.2,
                              ),
                            )
                          else
                            Text(
                              'Egitim odakli guclu bir baslangic alani',
                              textAlign: TextAlign.center,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: titleStyle,
                            ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}
