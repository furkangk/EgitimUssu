import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class AccountInfoPage extends StatefulWidget {
  const AccountInfoPage({super.key});

  @override
  State<AccountInfoPage> createState() => _AccountInfoPageState();
}

class _AccountInfoPageState extends State<AccountInfoPage> {
  static const _navy = Color(0xFF082B4F);
  static const _blue = Color(0xFF3D8BFF);
  static const _emerald = Color(0xFF20B486);
  static const _red = Color(0xFFFF5A5F);
  static const _slate = Color(0xFF6B7A90);
  static const _text = Color(0xFF10233D);
  static const _background = Color(0xFFF4F8FC);
  static const _border = Color(0xFFE5EEF7);
  static const _surfaceLow = Color(0xFFEAF3FF);

  late _AccountData _account;

  @override
  void initState() {
    super.initState();
    _account = _AccountData.initial();
  }

  @override
  Widget build(BuildContext context) {
    final session = context.select((AuthCubit cubit) => cubit.state.session);
    final fullName = session?.fullName.trim().isNotEmpty == true
        ? session!.fullName
        : _account.fullName;
    final role = session?.roles.isNotEmpty == true
        ? session!.roles.join(', ')
        : _account.role;

    _account = _account.copyWith(fullName: fullName, role: role);

    return Scaffold(
      backgroundColor: _background,
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 28),
          children: <Widget>[
            _TopBar(onBack: () => context.pop()),
            const SizedBox(height: 22),
            _AccountHeader(account: _account),
            const SizedBox(height: 22),
            const _SectionTitle(title: 'Hesap durumu'),
            const SizedBox(height: 10),
            _SettingsPanel(
              children: <Widget>[
                _StaticTile(
                  icon: Icons.manage_accounts_outlined,
                  label: 'Hesap türü',
                  value: _account.role,
                ),
                const _DividerLine(),
                _StaticTile(
                  icon: Icons.event_available_outlined,
                  label: 'Üyelik tarihi',
                  value: _account.memberSince,
                ),
              ],
            ),
            const SizedBox(height: 22),
            const _SectionTitle(title: 'Güvenlik'),
            const SizedBox(height: 10),
            _SettingsPanel(
              children: <Widget>[
                _SettingTile(
                  icon: Icons.lock_outline_rounded,
                  label: 'Şifre',
                  value: 'Son değişiklik 18 gün önce',
                  onTap: () =>
                      _showSavedMessage('Şifre değiştirme akışı açılacak.'),
                ),
                const _DividerLine(),
                _StatusTile(
                  icon: Icons.enhanced_encryption_outlined,
                  title: 'İki aşamalı doğrulama',
                  subtitle: 'Kapalı',
                  color: _blue,
                  onTap: () => _showSavedMessage('Doğrulama ayarı hazır.'),
                ),
                const _DividerLine(),
                _StatusTile(
                  icon: Icons.devices_rounded,
                  title: 'Aktif oturum',
                  subtitle: 'Bu cihaz, bugün 14:20',
                  color: _emerald,
                  onTap: () => _showSavedMessage('Oturum detayları açılacak.'),
                ),
              ],
            ),
            const SizedBox(height: 14),
            _SecurityNote(),
            const SizedBox(height: 22),
            const _SectionTitle(title: 'Hesap işlemleri'),
            const SizedBox(height: 10),
            _DangerPanel(onTap: _confirmCloseAccount),
          ],
        ),
      ),
    );
  }

  void _confirmCloseAccount() {
    showModalBottomSheet<void>(
      context: context,
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
              const _SheetTitle(title: 'Hesabı kapat'),
              const SizedBox(height: 10),
              Text(
                'Bu prototipte işlem yapılmaz; gerçek akışta hesap ve bağlı veriler için onay süreci başlatılır.',
                style: Theme.of(
                  context,
                ).textTheme.bodyMedium?.copyWith(color: _slate),
              ),
              const SizedBox(height: 18),
              Row(
                children: <Widget>[
                  Expanded(
                    child: OutlinedButton(
                      onPressed: () => Navigator.of(context).pop(),
                      child: const Text('Vazgeç'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: FilledButton(
                      style: FilledButton.styleFrom(backgroundColor: _red),
                      onPressed: () {
                        Navigator.of(context).pop();
                        _showSavedMessage('Hesap kapatma talebi taslakta.');
                      },
                      child: const Text('Devam et'),
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

  void _showSavedMessage(String message) {
    ScaffoldMessenger.of(
      context,
    ).showSnackBar(SnackBar(content: Text(message)));
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({required this.onBack});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        _IconButtonBox(icon: Icons.arrow_back_rounded, onTap: onBack),
        const SizedBox(width: 14),
        Expanded(
          child: Text(
            'Hesap Bilgileri',
            style: Theme.of(context).textTheme.headlineSmall?.copyWith(
              color: _AccountInfoPageState._text,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
      ],
    );
  }
}

class _AccountHeader extends StatelessWidget {
  const _AccountHeader({required this.account});

  final _AccountData account;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(20, 22, 20, 20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(28),
        border: Border.all(color: _AccountInfoPageState._border),
        boxShadow: const <BoxShadow>[
          BoxShadow(
            color: Color(0x0F082B4F),
            blurRadius: 28,
            offset: Offset(0, 12),
          ),
        ],
      ),
      child: Row(
        children: <Widget>[
          _AccountAvatar(name: account.fullName),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  account.fullName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.titleLarge?.copyWith(
                    color: _AccountInfoPageState._text,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: <Widget>[
                    _Pill(
                      label: account.role,
                      color: _AccountInfoPageState._emerald,
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _AccountAvatar extends StatelessWidget {
  const _AccountAvatar({required this.name});

  final String name;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 76,
      height: 76,
      decoration: const BoxDecoration(
        shape: BoxShape.circle,
        color: _AccountInfoPageState._navy,
      ),
      child: Center(
        child: Text(
          _initials(name),
          style: Theme.of(context).textTheme.titleLarge?.copyWith(
            color: Colors.white,
            fontWeight: FontWeight.w900,
          ),
        ),
      ),
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({required this.label, required this.color});

  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
          color: color,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}

class _SettingsPanel extends StatelessWidget {
  const _SettingsPanel({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _AccountInfoPageState._border),
      ),
      child: Column(children: children),
    );
  }
}

class _SettingTile extends StatelessWidget {
  const _SettingTile({
    required this.icon,
    required this.label,
    required this.value,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final String value;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(14, 13, 14, 13),
        child: Row(
          children: <Widget>[
            _TintedIcon(icon: icon, color: _AccountInfoPageState._blue),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    label,
                    style: Theme.of(context).textTheme.labelMedium?.copyWith(
                      color: _AccountInfoPageState._slate,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 3),
                  Text(
                    value,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      color: _AccountInfoPageState._text,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ],
              ),
            ),
            const Icon(
              Icons.chevron_right_rounded,
              color: _AccountInfoPageState._slate,
            ),
          ],
        ),
      ),
    );
  }
}

class _StaticTile extends StatelessWidget {
  const _StaticTile({
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
      padding: const EdgeInsets.fromLTRB(14, 13, 14, 13),
      child: Row(
        children: <Widget>[
          _TintedIcon(icon: icon, color: _AccountInfoPageState._blue),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  label,
                  style: Theme.of(context).textTheme.labelMedium?.copyWith(
                    color: _AccountInfoPageState._slate,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 3),
                Text(
                  value,
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                    color: _AccountInfoPageState._text,
                    fontWeight: FontWeight.w800,
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

class _StatusTile extends StatelessWidget {
  const _StatusTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.color,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final Color color;
  final VoidCallback onTap;

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
                      color: _AccountInfoPageState._text,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                  const SizedBox(height: 3),
                  Text(
                    subtitle,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _AccountInfoPageState._slate,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),
            const Icon(
              Icons.chevron_right_rounded,
              color: _AccountInfoPageState._slate,
            ),
          ],
        ),
      ),
    );
  }
}

class _SecurityNote extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: _AccountInfoPageState._surfaceLow,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: _AccountInfoPageState._border),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          const _TintedIcon(
            icon: Icons.verified_user_outlined,
            color: _AccountInfoPageState._emerald,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              'Hesap bilgileri bu prototipte statik tutulur. API bağlanınca aynı alanlar oturum verisiyle beslenecek.',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: _AccountInfoPageState._slate,
                fontWeight: FontWeight.w700,
                height: 1.45,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _DangerPanel extends StatelessWidget {
  const _DangerPanel({required this.onTap});

  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(22),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(22),
          border: Border.all(
            color: _AccountInfoPageState._red.withValues(alpha: 0.24),
          ),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            const _TintedIcon(
              icon: Icons.delete_outline_rounded,
              color: _AccountInfoPageState._red,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    'Hesabı kapat',
                    style: Theme.of(context).textTheme.titleSmall?.copyWith(
                      color: _AccountInfoPageState._red,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Hesabınız ve bağlı veriler için kalıcı işlem başlatılır.',
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: _AccountInfoPageState._slate,
                      fontWeight: FontWeight.w600,
                      height: 1.45,
                    ),
                  ),
                ],
              ),
            ),
            const Icon(
              Icons.chevron_right_rounded,
              color: _AccountInfoPageState._red,
            ),
          ],
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Text(
      title,
      style: Theme.of(context).textTheme.titleLarge?.copyWith(
        color: _AccountInfoPageState._text,
        fontWeight: FontWeight.w900,
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
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Icon(icon, color: color, size: 22),
    );
  }
}

class _IconButtonBox extends StatelessWidget {
  const _IconButtonBox({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Container(
        width: 46,
        height: 46,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: _AccountInfoPageState._border),
        ),
        child: Icon(icon, color: _AccountInfoPageState._text),
      ),
    );
  }
}

class _DividerLine extends StatelessWidget {
  const _DividerLine();

  @override
  Widget build(BuildContext context) {
    return const Divider(
      height: 1,
      indent: 68,
      color: _AccountInfoPageState._border,
    );
  }
}

class _SheetTitle extends StatelessWidget {
  const _SheetTitle({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerLeft,
      child: Text(
        title,
        style: Theme.of(context).textTheme.titleLarge?.copyWith(
          color: _AccountInfoPageState._text,
          fontWeight: FontWeight.w900,
        ),
      ),
    );
  }
}

class _AccountData {
  const _AccountData({
    required this.fullName,
    required this.role,
    required this.memberSince,
  });

  factory _AccountData.initial() {
    return const _AccountData(
      fullName: 'Ahmet Yılmaz',
      role: 'Öğretmen',
      memberSince: '12 Mayıs 2026',
    );
  }

  final String fullName;
  final String role;
  final String memberSince;

  _AccountData copyWith({String? fullName, String? role, String? memberSince}) {
    return _AccountData(
      fullName: fullName ?? this.fullName,
      role: role ?? this.role,
      memberSince: memberSince ?? this.memberSince,
    );
  }
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
