import 'package:egitim_ussu_mobile/core/network/api_exception.dart';
import 'package:egitim_ussu_mobile/core/theme/app_colors.dart';
import 'package:egitim_ussu_mobile/core/theme/app_shadows.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/student_scope.dart';
import 'package:egitim_ussu_mobile/features/study/presentation/widgets/student_bottom_nav.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// "Diğer" sekmesi — öğrenci hub'ı (ogrenci_ux §4). Profil/İstatistik, Rozetler,
/// Öğretmenlerim, paylaşım ayarları ve hesap/çıkış tek yerde toplanır.
///
/// Görsel dil öğretmen "Diğer" (`more_page.dart`) sayfasını referans alır:
/// ortalanmış hero profil kartı + gruplu menü panelleri + tonlu ikonlar.
class StudentMorePage extends StatefulWidget {
  const StudentMorePage({super.key});

  @override
  State<StudentMorePage> createState() => _StudentMorePageState();
}

class _StudentMorePageState extends State<StudentMorePage> {
  String? _studentId;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final session = context.read<AuthCubit>().state.session;
      final studentId = _studentId ??
          await StudentScope.resolve(
            userId: session?.userId ?? '',
            fullName: session?.fullName ?? '',
          );
      if (!mounted) return;
      setState(() {
        _studentId = studentId;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _error = e.message;
          _loading = false;
        });
      }
    }
  }

  void _confirmLogout() {
    showModalBottomSheet<void>(
      context: context,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
      ),
      builder: (BuildContext sheetContext) {
        return Padding(
          padding: const EdgeInsets.fromLTRB(20, 6, 20, 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                'Çıkış yap',
                style: Theme.of(sheetContext).textTheme.titleLarge?.copyWith(
                      color: AppColors.primary,
                      fontWeight: FontWeight.w800,
                    ),
              ),
              const SizedBox(height: 10),
              Text(
                'Oturumunu kapatmak istediğine emin misin?',
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
                      child: const Text('Vazgeç'),
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
                      child: const Text('Çıkış yap'),
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

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        bottom: false,
        child: _loading
            ? const LoadingStateView(message: 'Yükleniyor...')
            : _error != null
                ? ErrorStateView(message: _error!, onRetry: _load)
                : _content(),
      ),
      bottomNavigationBar: const StudentBottomNav(current: StudentNavTab.none),
    );
  }

  Widget _content() {
    final String studentId = _studentId!;
    final session = context.read<AuthCubit>().state.session;
    final String name = (session?.fullName.trim().isNotEmpty ?? false)
        ? session!.fullName.trim()
        : 'Öğrenci';

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 24),
      children: <Widget>[
        const _TopBar(),
        const SizedBox(height: 18),
        _ProfileSummary(
          name: name,
          onTap: () => context.push('/student/profile'),
        ),
        const SizedBox(height: 22),
        _MenuPanel(
          children: <Widget>[
            _MenuTile(
              icon: Icons.insights_rounded,
              color: AppColors.accentBlue,
              title: 'Profil / İstatistik',
              subtitle: 'Toplam çalışma, net, rozetler',
              onTap: () => context.push('/student/profile'),
            ),
            _DividerLine(),
            _MenuTile(
              icon: Icons.workspace_premium_rounded,
              color: AppColors.accentOrange,
              title: 'Rozetler',
              subtitle: 'Kazandığın başarımlar',
              onTap: () =>
                  context.push('/study/achievements?studentId=$studentId'),
            ),
            _DividerLine(),
            _MenuTile(
              icon: Icons.school_rounded,
              color: AppColors.primary,
              title: 'Öğretmenlerim',
              subtitle: 'Bağlı öğretmenlerin ve bilgileri',
              onTap: () => context.push('/student/teacher'),
            ),
            _DividerLine(),
            _MenuTile(
              icon: Icons.menu_book_rounded,
              color: AppColors.accentTeal,
              title: 'Dersler & Konular',
              subtitle: 'Ders ve konu kataloğunu düzenle',
              onTap: () => context.push('/study/catalog?studentId=$studentId'),
            ),
            _DividerLine(),
            _MenuTile(
              icon: Icons.assignment_turned_in_rounded,
              color: AppColors.accentBlue,
              title: 'Ödevlerim',
              subtitle: 'Öğretmen ödevlerini yükle ve tamamla',
              onTap: () => context.push('/student/assignments'),
            ),
            _DividerLine(),
            _MenuTile(
              icon: Icons.sticky_note_2_rounded,
              color: AppColors.accentOrange,
              title: 'Notlarım',
              subtitle: 'Kendi ders notlarını ekle ve düzenle',
              onTap: () => context.push('/study/notes?studentId=$studentId'),
            ),
            _DividerLine(),
            _MenuTile(
              icon: Icons.flag_rounded,
              color: AppColors.accentGreen,
              title: 'Hedefler',
              subtitle: 'Günlük, haftalık ve net hedeflerin',
              onTap: () => context.push('/student/goals-overview'),
            ),
            _DividerLine(),
            _MenuTile(
              icon: Icons.insights_rounded,
              color: AppColors.accentBlue,
              title: 'Gelişimim',
              subtitle: 'Konu bazlı hâkimiyet, eksik/güçlü konular',
              onTap: () => context.push('/student/progress?studentId=$studentId'),
            ),
          ],
        ),
        const SizedBox(height: 14),
        _MenuPanel(
          children: <Widget>[
            _MenuTile(
              icon: Icons.tune_rounded,
              color: AppColors.accentGreen,
              title: 'Hedef & paylaşım ayarları',
              subtitle: 'Hedefler ve veli/öğretmen paylaşımı',
              onTap: () => context.push('/study/goals?studentId=$studentId'),
            ),
            _DividerLine(),
            _MenuTile(
              icon: Icons.manage_accounts_outlined,
              color: AppColors.accentTeal,
              title: 'Hesap bilgileri',
              subtitle: 'E-posta, rol ve oturum',
              onTap: () => context.push('/account-info'),
            ),
          ],
        ),
        const SizedBox(height: 14),
        _MenuPanel(
          children: <Widget>[
            _MenuTile(
              icon: Icons.logout_rounded,
              color: AppColors.accentRed,
              title: 'Çıkış yap',
              subtitle: 'Oturumu kapat ve giriş ekranına dön',
              titleColor: AppColors.accentRed,
              onTap: _confirmLogout,
            ),
          ],
        ),
      ],
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar();

  @override
  Widget build(BuildContext context) {
    return Text(
      'Diğer',
      style: Theme.of(context).textTheme.headlineSmall?.copyWith(
            color: AppColors.textPrimary,
            fontWeight: FontWeight.w800,
          ),
    );
  }
}

class _ProfileSummary extends StatelessWidget {
  const _ProfileSummary({required this.name, required this.onTap});

  final String name;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
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
            _InitialsAvatar(name: name),
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
            const SizedBox(height: 10),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              decoration: BoxDecoration(
                color: AppColors.primaryLight,
                borderRadius: BorderRadius.circular(999),
              ),
              child: Text(
                'Öğrenci',
                style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: AppColors.primary,
                      fontWeight: FontWeight.w800,
                    ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _InitialsAvatar extends StatelessWidget {
  const _InitialsAvatar({required this.name});

  final String name;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 104,
      height: 104,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: AppColors.primary,
        border: Border.all(color: Colors.white, width: 4),
        boxShadow: AppShadows.soft,
      ),
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
    final List<String> parts = value
        .split(RegExp(r'\s+'))
        .where((String part) => part.isNotEmpty)
        .take(2)
        .toList();
    if (parts.isEmpty) {
      return '?';
    }
    return parts.map((String part) => part.characters.first).join().toUpperCase();
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
    required this.color,
    required this.title,
    required this.subtitle,
    required this.onTap,
    this.titleColor,
  });

  final IconData icon;
  final Color color;
  final String title;
  final String subtitle;
  final VoidCallback onTap;
  final Color? titleColor;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(14, 13, 14, 13),
        child: Row(
          children: <Widget>[
            _TintedIcon(icon: icon, color: color),
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

class _TintedIcon extends StatelessWidget {
  const _TintedIcon({required this.icon, required this.color});

  final IconData icon;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 42,
      height: 42,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Icon(icon, color: color, size: 22),
    );
  }
}

class _DividerLine extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return const Divider(height: 1, indent: 66, color: AppColors.border);
  }
}
