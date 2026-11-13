import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// D-17: Oturum yokken sahte bir kullanici kimligi ("mock-parent-user") uydurmak
/// yerine gosterilen bos durum. Kullanici giris ekranina yonlendirilir; boylece
/// ekran, olmayan bir hesabin verisini gercekmis gibi gostermez.
class SessionRequiredView extends StatelessWidget {
  const SessionRequiredView({
    super.key,
    this.title = 'Oturum bulunamadi',
    this.subtitle = 'Bu ekrani gormek icin giris yapmalisin.',
  });

  final String title;
  final String subtitle;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                const Icon(
                  Icons.lock_outline_rounded,
                  size: 40,
                  color: AppColors.primary,
                ),
                const SizedBox(height: 12),
                Text(
                  title,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 4),
                Text(
                  subtitle,
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.bodyMedium,
                ),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: () => context.go('/login'),
                  child: const Text('Giris yap'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
