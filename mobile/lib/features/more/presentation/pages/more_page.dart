import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/membership/presentation/cubit/membership_cubit.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/domain/teacher_profile_contracts.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/presentation/cubit/teacher_profile_cubit.dart';
import 'package:egitim_ussu_mobile/features/teacher_profile/presentation/cubit/teacher_profile_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/app_bottom_nav.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class MorePage extends StatefulWidget {
  const MorePage({super.key});

  @override
  State<MorePage> createState() => _MorePageState();
}

class _MorePageState extends State<MorePage> {
  @override
  Widget build(BuildContext context) {
    final session = context.select((AuthCubit cubit) => cubit.state.session);
    final fallbackName = session?.fullName.trim().isNotEmpty == true
        ? session!.fullName
        : 'Ogretmen';
    final userId = session?.userId ?? '';
    // D-15: Rozet artık gerçek üyelikten gelir (`GET /api/membership/me`),
    // P05'teki sabit `true` sahtesi kaldırıldı.
    final isPremium = context.select(
      (MembershipCubit cubit) => cubit.state.isPremium,
    );

    return BlocProvider<TeacherProfileCubit>(
      create: (_) => TeacherProfileCubit.create()..load(userId),
      child: BlocBuilder<TeacherProfileCubit, TeacherProfileState>(
        builder: (context, state) {
          final profile = state.profile;
          final displayName = profile?.fullName.trim().isNotEmpty == true
              ? profile!.fullName
              : fallbackName;

          return Scaffold(
            backgroundColor: AppColors.background,
            bottomNavigationBar: const AppBottomNav(current: AppNavTab.more),
            body: SafeArea(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 10, 16, 110),
                children: <Widget>[
                  const _TopBar(),
                  const SizedBox(height: 18),
                  _ProfileSummary(
                    name: displayName,
                    profile: profile,
                    rating: 4.8,
                    reviewCount: 126,
                    isPlusMember: isPremium,
                    onTap: () => context.push('/teacher-profile'),
                  ),
                  const SizedBox(height: 22),
                  _MenuPanel(
                    children: <Widget>[
                      _MenuTile(
                        icon: Icons.person_outline_rounded,
                        title: 'Profil Bilgileri',
                        subtitle: 'Vitrin, brans, biyografi ve musaitlik',
                        color: AppColors.accentBlue,
                        onTap: () => context.push('/teacher-profile'),
                      ),
                      _DividerLine(),
                      _MenuTile(
                        icon: Icons.manage_accounts_outlined,
                        title: 'Hesap bilgileri',
                        subtitle: 'E-posta, rol ve oturum bilgileri',
                        color: AppColors.accentGreen,
                        onTap: () => context.push('/account-info'),
                      ),
                      _DividerLine(),
                      // D-10/D-15: P09'da gercek uyelik uclari geldi (M17-1/M17-3);
                      // satir artik aktif ve `GET /api/membership/me`den beslenen
                      // uyelik ekranina gidiyor.
                      _MenuTile(
                        icon: Icons.workspace_premium_outlined,
                        title: 'Uyelik',
                        subtitle: isPremium
                            ? 'Premium plan, donem sonu ve referans kodu'
                            : 'Plani gor, ucretsiz dene ve referans kodunu kullan',
                        color: AppColors.amber,
                        onTap: () => context.push('/membership'),
                      ),
                      _DividerLine(),
                      // D-16: P08'de gercek rapor uclari geldi (M14-1/M14-5);
                      // satir artik aktif ve projeksiyondan beslenen ekrana gidiyor.
                      _MenuTile(
                        icon: Icons.bar_chart_rounded,
                        title: 'Raporlar',
                        subtitle: 'Aylik ders/tahsilat ozeti ve bos zamanlar',
                        color: AppColors.amber,
                        onTap: () => context.push('/teacher/reports'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 14),
                  _MenuPanel(
                    children: <Widget>[
                      // D-14: Sahte "sessiz saatler" sheet'i kaldirildi; satir gercek
                      // gizlilik/guvenlik ekranina gidiyor (M15-3/M15-4).
                      _MenuTile(
                        icon: Icons.shield_outlined,
                        title: 'Gizlilik ve guvenlik',
                        subtitle:
                            'Calisma verisi paylasimi ve oturum politikasi',
                        color: AppColors.accentBlue,
                        onTap: () => context.push('/settings/privacy'),
                      ),
                      _DividerLine(),
                      _MenuTile(
                        icon: Icons.notifications_active_outlined,
                        title: 'Bildirim ayarlari',
                        subtitle: 'Ders, odev, odeme ve haftalik ozet',
                        color: AppColors.accentRed,
                        onTap: () => context.push('/settings/notifications'),
                      ),
                      _DividerLine(),
                      // Gercek karsiligi ogretmen profilidir (ders bicimi + musaitlik).
                      _MenuTile(
                        icon: Icons.work_outline_rounded,
                        title: 'Calisma ayarlari',
                        subtitle: 'Ders bicimi ve musaitlik (profil)',
                        color: AppColors.accentGreen,
                        onTap: () => context.push('/teacher-profile'),
                      ),
                      _DividerLine(),
                      // Gercek karsiligi M04 tatil uclaridir; takvimdeki tatil yonetimi.
                      _MenuTile(
                        icon: Icons.event_busy_outlined,
                        title: 'Tatil ayarlari',
                        subtitle: 'Kapali gunler ve tatil takvimi',
                        color: AppColors.amber,
                        onTap: () => context.push('/scheduling'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 14),
                  _MenuPanel(
                    children: <Widget>[
                      _MenuTile(
                        icon: Icons.help_outline_rounded,
                        title: 'Yardim (SSS)',
                        subtitle: 'Sik sorulan sorular',
                        color: AppColors.accentBlue,
                        onTap: _showHelpSheet,
                      ),
                      _DividerLine(),
                      _MenuTile(
                        icon: Icons.support_agent_rounded,
                        title: 'Bize ulasin',
                        subtitle: 'Hata, talep veya gelistirme fikri',
                        color: AppColors.accentGreen,
                        onTap: _showContactSheet,
                      ),
                      _DividerLine(),
                      _MenuTile(
                        icon: Icons.info_outline_rounded,
                        title: 'Hakkimizda',
                        subtitle: 'EgitimUssu ve uygulama bilgileri',
                        color: AppColors.textSecondary,
                        onTap: _showAboutSheet,
                      ),
                      _DividerLine(),
                      _MenuTile(
                        icon: Icons.logout_rounded,
                        title: 'Cikis yap',
                        subtitle: 'Oturumu kapat ve giris ekranina don',
                        color: AppColors.accentRed,
                        titleColor: AppColors.accentRed,
                        onTap: _confirmLogout,
                      ),
                    ],
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }

  void _showHelpSheet() {
    _showInfoSheet(
      title: 'Yardim',
      children: <Widget>[
        _FaqTile(
          title: 'Bildirimler ne zaman gider?',
          body:
              'Ders ve odeme hatirlatmalari aktifse ilgili saatlerden once hazir bildirim olusturulur.',
        ),
        _DividerLine(),
        _FaqTile(
          title: 'Tatil gunleri takvimi etkiler mi?',
          body:
              'Tatil gunlerini kapat secenegi acikken yeni ders slotlari varsayilan olarak kapali gelir.',
        ),
        _DividerLine(),
        _FaqTile(
          title: 'Profil bilgilerim nerede gorunur?',
          body:
              'Profil bilgileri ogrenci ve veli tarafinda ogretmen vitrinini olusturur.',
        ),
      ],
    );
  }

  void _showContactSheet() {
    final controller = TextEditingController();
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (context) {
        return Padding(
          padding: EdgeInsets.fromLTRB(
            20,
            6,
            20,
            MediaQuery.of(context).viewInsets.bottom + 24,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              _SheetTitle(title: 'Bize ulasin'),
              const SizedBox(height: 12),
              TextField(
                controller: controller,
                minLines: 4,
                maxLines: 6,
                decoration: const InputDecoration(
                  labelText: 'Mesajiniz',
                  hintText: 'Bir hata, istek veya gelistirme fikri yazin.',
                ),
              ),
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: () {
                    Navigator.of(context).pop();
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Mesajiniz alindi.')),
                    );
                  },
                  icon: const Icon(Icons.send_rounded),
                  label: const Text('Gonder'),
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  void _showAboutSheet() {
    _showInfoSheet(
      title: 'Hakkimizda',
      children: const <Widget>[
        Text(
          'EgitimUssu; ogretmenlerin ders planlama, ogrenci takibi, odev, tahsilat ve veli bilgilendirme sureclerini tek mobil deneyimde yonetmesi icin tasarlanir.',
        ),
      ],
    );
  }

  void _confirmLogout() {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (sheetContext) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 6, 20, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              const _SheetTitle(title: 'Cikis yap'),
              const SizedBox(height: 10),
              Text(
                'Hesabinizdan cikis yapmak istiyor musunuz?',
                style: Theme.of(sheetContext).textTheme.bodyMedium?.copyWith(
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 18),
              Row(
                children: <Widget>[
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () => Navigator.of(sheetContext).pop(),
                      child: const Text('Vazgec'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: FilledButton(
                      style: FilledButton.styleFrom(
                        backgroundColor: AppColors.accentRed,
                      ),
                      onPressed: () {
                        Navigator.of(sheetContext).pop();
                        context.read<AuthCubit>().logout();
                      },
                      child: const Text('Cikis yap'),
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );
  }

  void _showInfoSheet({required String title, required List<Widget> children}) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (context) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 6, 20, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              _SheetTitle(title: title),
              const SizedBox(height: 14),
              _MenuPanel(children: children),
            ],
          ),
        );
      },
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar();

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Text(
          'Diger',
          style: Theme.of(context).textTheme.headlineSmall?.copyWith(
            color: AppColors.textPrimary,
            fontWeight: FontWeight.w800,
          ),
        ),
      ],
    );
  }
}

class _ProfileSummary extends StatelessWidget {
  const _ProfileSummary({
    required this.name,
    required this.profile,
    required this.rating,
    required this.reviewCount,
    required this.isPlusMember,
    required this.onTap,
  });

  final String name;
  final TeacherProfile? profile;
  final double rating;
  final int reviewCount;
  final bool isPlusMember;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final photoUrl = profile?.profilePhotoUrl;

    return InkWell(
      borderRadius: BorderRadius.circular(28),
      onTap: onTap,
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.fromLTRB(20, 24, 20, 22),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(28),
          border: Border.all(color: AppColors.border),
          boxShadow: AppShadows.soft,
        ),
        child: Column(
          children: <Widget>[
            _ProfileAvatar(name: name, photoUrl: photoUrl),
            const SizedBox(height: 14),
            Text(
              name,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 8),
            _RatingLine(rating: rating, reviewCount: reviewCount),
            if (isPlusMember) ...<Widget>[
              const SizedBox(height: 12),
              const _PlusBadge(),
            ],
          ],
        ),
      ),
    );
  }
}

class _PlusBadge extends StatelessWidget {
  const _PlusBadge();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: BoxDecoration(
        color: AppColors.amber.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: AppColors.amber.withValues(alpha: 0.35)),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          const Icon(
            Icons.workspace_premium_rounded,
            size: 18,
            color: AppColors.amber,
          ),
          const SizedBox(width: 7),
          Text(
            'Plus uye',
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
              color: AppColors.textPrimary,
              fontWeight: FontWeight.w800,
            ),
          ),
        ],
      ),
    );
  }
}

class _ProfileAvatar extends StatelessWidget {
  const _ProfileAvatar({required this.name, required this.photoUrl});

  final String name;
  final String? photoUrl;

  @override
  Widget build(BuildContext context) {
    final hasPhoto = photoUrl != null && photoUrl!.trim().isNotEmpty;

    return Container(
      width: 104,
      height: 104,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: AppColors.primary,
        border: Border.all(color: Colors.white, width: 4),
        boxShadow: AppShadows.soft,
      ),
      clipBehavior: Clip.antiAlias,
      child: hasPhoto
          ? Image.network(
              photoUrl!,
              fit: BoxFit.cover,
              errorBuilder: (_, __, ___) => _InitialsAvatar(name: name),
            )
          : _InitialsAvatar(name: name),
    );
  }
}

class _InitialsAvatar extends StatelessWidget {
  const _InitialsAvatar({required this.name});

  final String name;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Text(
        _initials(name),
        style: Theme.of(context).textTheme.headlineMedium?.copyWith(
          color: Colors.white,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }

  String _initials(String value) {
    final parts = value
        .split(RegExp(r'\s+'))
        .where((part) => part.isNotEmpty)
        .take(2)
        .toList();
    if (parts.isEmpty) {
      return 'OG';
    }
    return parts.map((part) => part.characters.first).join().toUpperCase();
  }
}

class _RatingLine extends StatelessWidget {
  const _RatingLine({required this.rating, required this.reviewCount});

  final double rating;
  final int reviewCount;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        const Icon(Icons.star_rounded, size: 22, color: AppColors.amber),
        const SizedBox(width: 5),
        Text(
          rating.toStringAsFixed(1),
          style: Theme.of(context).textTheme.titleSmall?.copyWith(
            color: AppColors.textPrimary,
            fontWeight: FontWeight.w800,
          ),
        ),
        const SizedBox(width: 6),
        Text(
          '($reviewCount degerlendirme)',
          style: Theme.of(
            context,
          ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
        ),
      ],
    );
  }
}

class _MenuPanel extends StatelessWidget {
  const _MenuPanel({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(children: children),
    );
  }
}

class _MenuTile extends StatelessWidget {
  const _MenuTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.color,
    this.onTap,
    this.titleColor,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final Color color;
  final VoidCallback? onTap;
  final Color? titleColor;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(14, 13, 14, 13),
        child: Row(
          children: <Widget>[
            _TintedIcon(icon: icon, color: color, size: 42),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    title,
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      color: titleColor ?? AppColors.textPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 3),
                  Text(
                    subtitle,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: AppColors.textSecondary,
                    ),
                  ),
                ],
              ),
            ),
            const Icon(
              Icons.chevron_right_rounded,
              color: AppColors.textSecondary,
            ),
          ],
        ),
      ),
    );
  }
}

class _FaqTile extends StatelessWidget {
  const _FaqTile({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    return Theme(
      data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
      child: ExpansionTile(
        tilePadding: const EdgeInsets.symmetric(horizontal: 14),
        childrenPadding: const EdgeInsets.fromLTRB(14, 0, 14, 14),
        iconColor: AppColors.primary,
        collapsedIconColor: AppColors.textSecondary,
        title: Text(
          title,
          style: Theme.of(context).textTheme.titleSmall?.copyWith(
            color: AppColors.textPrimary,
            fontWeight: FontWeight.w800,
          ),
        ),
        children: <Widget>[
          Align(
            alignment: Alignment.centerLeft,
            child: Text(
              body,
              style: Theme.of(
                context,
              ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
            ),
          ),
        ],
      ),
    );
  }
}

class _SheetTitle extends StatelessWidget {
  const _SheetTitle({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Text(
      title,
      style: Theme.of(context).textTheme.titleLarge?.copyWith(
        color: AppColors.primary,
        fontWeight: FontWeight.w800,
      ),
    );
  }
}

class _TintedIcon extends StatelessWidget {
  const _TintedIcon({required this.icon, required this.color, this.size = 42});

  final IconData icon;
  final Color color;
  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(size >= 40 ? 14 : 12),
      ),
      child: Icon(icon, color: color, size: size >= 40 ? 22 : 18),
    );
  }
}

class _DividerLine extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return const Divider(height: 1, indent: 66, color: AppColors.border);
  }
}
