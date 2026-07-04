import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_cubit.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/cubit/parent_state.dart';
import 'package:egitim_ussu_mobile/features/parent/presentation/widgets/parent_widgets.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class ParentProfilePage extends StatelessWidget {
  const ParentProfilePage({super.key});

  @override
  Widget build(BuildContext context) {
    final userId = context.select(
      (AuthCubit c) => c.state.session?.userId ?? 'mock-parent-user',
    );
    final fullName = context.select(
      (AuthCubit c) => c.state.session?.fullName ?? 'Veli',
    );
    final email = context.select((AuthCubit c) => c.state.session?.email ?? '');

    return BlocProvider<ParentCubit>(
      create: (_) => ParentCubit.create()..load(userId, fullName: fullName),
      child: Scaffold(
        backgroundColor: AppColors.background,
        bottomNavigationBar: const ParentBottomNav(
          current: ParentNavTab.profile,
        ),
        body: SafeArea(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
            children: <Widget>[
              const ParentHeader(title: 'Profil'),
              const SizedBox(height: 4),
              _ProfileHeaderCard(fullName: fullName, email: email),
              const SizedBox(height: 14),
              BlocBuilder<ParentCubit, ParentState>(
                builder: (context, state) {
                  final profile = state.profile;
                  return ParentCard(
                    title: 'İletişim',
                    icon: Icons.contact_phone_rounded,
                    child: Column(
                      children: <Widget>[
                        _InfoRow(
                          icon: Icons.phone_rounded,
                          label: 'Telefon',
                          value: profile?.contactPhone ?? '—',
                        ),
                        _InfoRow(
                          icon: Icons.mail_rounded,
                          label: 'E-posta',
                          value: profile?.contactEmail ?? email,
                        ),
                        _InfoRow(
                          icon: Icons.family_restroom_rounded,
                          label: 'Bağlı çocuk',
                          value: '${state.children.length}',
                        ),
                      ],
                    ),
                  );
                },
              ),
              const SizedBox(height: 14),
              _MenuTile(
                icon: Icons.notifications_rounded,
                label: 'Bildirim tercihleri',
                onTap: () => context.go('/parent/notifications'),
              ),
              _MenuTile(
                icon: Icons.family_restroom_rounded,
                label: 'Çocuklarım',
                onTap: () => context.go('/parent/children'),
              ),
              const SizedBox(height: 8),
              _MenuTile(
                icon: Icons.logout_rounded,
                label: 'Çıkış yap',
                color: AppColors.accentRed,
                onTap: () => context.read<AuthCubit>().logout(),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ProfileHeaderCard extends StatelessWidget {
  const _ProfileHeaderCard({required this.fullName, required this.email});

  final String fullName;
  final String email;

  @override
  Widget build(BuildContext context) {
    return ParentCard(
      child: Row(
        children: <Widget>[
          Container(
            width: 58,
            height: 58,
            decoration: BoxDecoration(
              gradient: const LinearGradient(
                colors: <Color>[AppColors.primary, AppColors.primaryDark],
              ),
              borderRadius: BorderRadius.circular(18),
            ),
            child: const Icon(
              Icons.person_rounded,
              color: Colors.white,
              size: 30,
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  fullName,
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  email.isEmpty ? 'Veli hesabı' : email,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
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

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 7),
      child: Row(
        children: <Widget>[
          Icon(icon, size: 18, color: AppColors.textSecondary),
          const SizedBox(width: 10),
          Text(
            label,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: AppColors.textSecondary,
            ),
          ),
          const Spacer(),
          Text(
            value,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

class _MenuTile extends StatelessWidget {
  const _MenuTile({
    required this.icon,
    required this.label,
    required this.onTap,
    this.color,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    final tint = color ?? AppColors.primary;
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Material(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        child: InkWell(
          borderRadius: BorderRadius.circular(16),
          onTap: onTap,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: AppColors.border),
            ),
            child: Row(
              children: <Widget>[
                Icon(icon, color: tint, size: 20),
                const SizedBox(width: 12),
                Text(
                  label,
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w700,
                    color: color ?? AppColors.textPrimary,
                  ),
                ),
                const Spacer(),
                const Icon(
                  Icons.arrow_forward_ios_rounded,
                  size: 15,
                  color: AppColors.textMuted,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
