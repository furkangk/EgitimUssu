import 'dart:async';

import 'package:egitim_ussu_mobile/core/config/app_config.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_cubit.dart';
import 'package:egitim_ussu_mobile/features/auth/presentation/cubit/auth_state.dart';
import 'package:egitim_ussu_mobile/shared/widgets/state_views.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({super.key, this.selectedRole = 'ogretmen'});

  final String selectedRole;

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  static const _primary = Color(0xFF082B4F);
  static const _primaryDark = Color(0xFF061F3A);
  static const _background = Color(0xFFF7F9FC);
  static const _surface = Color(0xFFFFFFFF);
  static const _textPrimary = Color(0xFF111827);
  static const _textSecondary = Color(0xFF6B7280);
  static const _border = Color(0xFFE5E7EB);

  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController(text: 'teacher1@example.com');
  final _passwordController = TextEditingController(text: 'Teacher123!');
  bool _obscurePassword = true;
  bool _rememberMe = false;
  bool _showWakeUpHint = false;
  Timer? _wakeUpTimer;

  void _startLogin() {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    context.read<AuthCubit>().login(
      email: _emailController.text.trim(),
      password: _passwordController.text.trim(),
    );
    final isProduction = AppConfig.fromEnvironment().isProductionLike;
    if (isProduction) {
      _wakeUpTimer = Timer(const Duration(seconds: 6), () {
        if (mounted) setState(() => _showWakeUpHint = true);
      });
    }
  }

  void _cancelWakeUpHint() {
    _wakeUpTimer?.cancel();
    if (mounted) setState(() => _showWakeUpHint = false);
  }

  @override
  void dispose() {
    _wakeUpTimer?.cancel();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      backgroundColor: _background,
      body: SafeArea(
        child: Center(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(20, 20, 20, 20),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 430),
              child: BlocConsumer<AuthCubit, AuthState>(
                listener: (context, state) {
                  if (state.status != AuthStatus.loading) {
                    _cancelWakeUpHint();
                  }
                },
                builder: (context, state) {
                  return Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      _TopBar(onBack: () => context.go('/')),
                      const SizedBox(height: 20),
                      _LogoHeader(selectedRole: widget.selectedRole),
                      const SizedBox(height: 20),
                      Container(
                        padding: const EdgeInsets.all(18),
                        decoration: BoxDecoration(
                          color: _surface,
                          borderRadius: BorderRadius.circular(24),
                          border: Border.all(color: _border),
                          boxShadow: const <BoxShadow>[
                            BoxShadow(
                              color: Color(0x12082B4F),
                              blurRadius: 24,
                              offset: Offset(0, 12),
                            ),
                          ],
                        ),
                        child: Form(
                          key: _formKey,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: <Widget>[
                              if (state.errorMessage != null) ...<Widget>[
                                ErrorStateView(message: state.errorMessage!),
                                const SizedBox(height: 14),
                              ],
                              Text(
                                'E-posta',
                                style: theme.textTheme.labelMedium?.copyWith(
                                  color: _textSecondary,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                              const SizedBox(height: 8),
                              TextFormField(
                                controller: _emailController,
                                keyboardType: TextInputType.emailAddress,
                                decoration: InputDecoration(
                                  hintText: 'ornek@mail.com',
                                  prefixIcon: const Icon(
                                    Icons.mail_outline_rounded,
                                  ),
                                  filled: true,
                                  fillColor: const Color(0xFFF9FBFD),
                                  contentPadding: const EdgeInsets.symmetric(
                                    horizontal: 16,
                                    vertical: 14,
                                  ),
                                  border: OutlineInputBorder(
                                    borderRadius: BorderRadius.circular(14),
                                    borderSide: const BorderSide(color: _border),
                                  ),
                                  enabledBorder: OutlineInputBorder(
                                    borderRadius: BorderRadius.circular(14),
                                    borderSide: const BorderSide(color: _border),
                                  ),
                                  focusedBorder: OutlineInputBorder(
                                    borderRadius: BorderRadius.circular(14),
                                    borderSide: const BorderSide(
                                      color: _primary,
                                      width: 1.4,
                                    ),
                                  ),
                                ),
                                validator: (value) {
                                  if (value == null || !value.contains('@')) {
                                    return 'Gecerli bir e-posta gir.';
                                  }
                                  return null;
                                },
                              ),
                              const SizedBox(height: 14),
                              Text(
                                'Sifre',
                                style: theme.textTheme.labelMedium?.copyWith(
                                  color: _textSecondary,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                              const SizedBox(height: 8),
                              TextFormField(
                                controller: _passwordController,
                                obscureText: _obscurePassword,
                                decoration: InputDecoration(
                                  hintText: '••••••••',
                                  prefixIcon: const Icon(Icons.lock_outline_rounded),
                                  filled: true,
                                  fillColor: const Color(0xFFF9FBFD),
                                  contentPadding: const EdgeInsets.symmetric(
                                    horizontal: 16,
                                    vertical: 14,
                                  ),
                                  suffixIcon: IconButton(
                                    onPressed: () {
                                      setState(() {
                                        _obscurePassword = !_obscurePassword;
                                      });
                                    },
                                    icon: Icon(
                                      _obscurePassword
                                          ? Icons.visibility_outlined
                                          : Icons.visibility_off_outlined,
                                    ),
                                  ),
                                  border: OutlineInputBorder(
                                    borderRadius: BorderRadius.circular(14),
                                    borderSide: const BorderSide(color: _border),
                                  ),
                                  enabledBorder: OutlineInputBorder(
                                    borderRadius: BorderRadius.circular(14),
                                    borderSide: const BorderSide(color: _border),
                                  ),
                                  focusedBorder: OutlineInputBorder(
                                    borderRadius: BorderRadius.circular(14),
                                    borderSide: const BorderSide(
                                      color: _primary,
                                      width: 1.4,
                                    ),
                                  ),
                                ),
                                validator: (value) {
                                  if (value == null || value.length < 8) {
                                    return 'Sifre en az 8 karakter olmali.';
                                  }
                                  return null;
                                },
                              ),
                              const SizedBox(height: 10),
                              Row(
                                children: <Widget>[
                                  InkWell(
                                    borderRadius: BorderRadius.circular(999),
                                    onTap: () {
                                      setState(() {
                                        _rememberMe = !_rememberMe;
                                      });
                                    },
                                    child: Padding(
                                      padding: const EdgeInsets.symmetric(
                                        vertical: 4,
                                      ),
                                      child: Row(
                                        children: <Widget>[
                                          SizedBox(
                                            width: 20,
                                            height: 20,
                                            child: Checkbox(
                                              value: _rememberMe,
                                              onChanged: (value) {
                                                setState(() {
                                                  _rememberMe = value ?? false;
                                                });
                                              },
                                              activeColor: _primary,
                                              visualDensity:
                                                  VisualDensity.compact,
                                            ),
                                          ),
                                          const SizedBox(width: 8),
                                          Text(
                                            'Beni hatirla',
                                            style: theme.textTheme.bodySmall
                                                ?.copyWith(
                                                  color: _textSecondary,
                                                ),
                                          ),
                                        ],
                                      ),
                                    ),
                                  ),
                                  const Spacer(),
                                  TextButton(
                                    onPressed: () {
                                      ScaffoldMessenger.of(context).showSnackBar(
                                        const SnackBar(
                                          content: Text(
                                            'Sifre sifirlama akisi henuz eklenmedi.',
                                          ),
                                        ),
                                      );
                                    },
                                    style: TextButton.styleFrom(
                                      foregroundColor: _primary,
                                      padding: EdgeInsets.zero,
                                      minimumSize: Size.zero,
                                      tapTargetSize:
                                          MaterialTapTargetSize.shrinkWrap,
                                    ),
                                    child: const Text('Sifremi unuttum'),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 14),
                              SizedBox(
                                width: double.infinity,
                                child: FilledButton(
                                  onPressed: state.status == AuthStatus.loading
                                      ? null
                                      : () {
                                          _cancelWakeUpHint();
                                          _startLogin();
                                        },
                                  style: FilledButton.styleFrom(
                                    backgroundColor: _primary,
                                    foregroundColor: Colors.white,
                                    minimumSize: const Size.fromHeight(54),
                                    shape: RoundedRectangleBorder(
                                      borderRadius: BorderRadius.circular(14),
                                    ),
                                    elevation: 0,
                                  ),
                                  child: state.status == AuthStatus.loading
                                      ? const SizedBox(
                                          width: 22,
                                          height: 22,
                                          child: CircularProgressIndicator(
                                            strokeWidth: 2.2,
                                            color: Colors.white,
                                          ),
                                        )
                                      : Text(
                                          'Giris Yap',
                                          style: theme.textTheme.titleMedium
                                              ?.copyWith(
                                                color: Colors.white,
                                                fontWeight: FontWeight.w700,
                                              ),
                                        ),
                                ),
                              ),
                              if (_showWakeUpHint &&
                                  state.status == AuthStatus.loading) ...[
                                const SizedBox(height: 8),
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    const Icon(
                                      Icons.cloud_outlined,
                                      size: 14,
                                      color: _textSecondary,
                                    ),
                                    const SizedBox(width: 6),
                                    Text(
                                      'Sunucu uyandırılıyor, lütfen bekleyin...',
                                      style: theme.textTheme.bodySmall?.copyWith(
                                        color: _textSecondary,
                                      ),
                                    ),
                                  ],
                                ),
                              ],
                              const SizedBox(height: 16),
                              const _AuthDivider(),
                              const SizedBox(height: 16),
                              SizedBox(
                                width: double.infinity,
                                child: OutlinedButton.icon(
                                  onPressed: () {
                                    ScaffoldMessenger.of(context).showSnackBar(
                                      const SnackBar(
                                        content: Text(
                                          'Google ile giris akisi henuz eklenmedi.',
                                        ),
                                      ),
                                    );
                                  },
                                  style: OutlinedButton.styleFrom(
                                    foregroundColor: _textPrimary,
                                    minimumSize: const Size.fromHeight(54),
                                    side: const BorderSide(color: _border),
                                    shape: RoundedRectangleBorder(
                                      borderRadius: BorderRadius.circular(14),
                                    ),
                                  ),
                                  icon: Container(
                                    width: 22,
                                    height: 22,
                                    decoration: const BoxDecoration(
                                      color: Colors.white,
                                      shape: BoxShape.circle,
                                    ),
                                    child: const Center(
                                      child: Text(
                                        'G',
                                        style: TextStyle(
                                          color: Color(0xFF4285F4),
                                          fontSize: 14,
                                          fontWeight: FontWeight.w700,
                                        ),
                                      ),
                                    ),
                                  ),
                                  label: Text(
                                    'Google',
                                    style: theme.textTheme.titleMedium?.copyWith(
                                      color: _textPrimary,
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: 14),
                      Center(
                        child: RichText(
                          text: TextSpan(
                            style: theme.textTheme.bodyMedium?.copyWith(
                              color: _textSecondary,
                              fontWeight: FontWeight.w400,
                            ),
                            children: <InlineSpan>[
                              const TextSpan(text: 'Hesabin yok mu? '),
                              WidgetSpan(
                                alignment: PlaceholderAlignment.middle,
                                child: GestureDetector(
                                  onTap: () => context.go('/role-selection'),
                                  child: Text(
                                    'Kayit Ol',
                                    style: theme.textTheme.bodyMedium?.copyWith(
                                      color: _primary,
                                      fontWeight: FontWeight.w700,
                                    ),
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({required this.onBack});

  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        _IconPill(icon: Icons.arrow_back_rounded, onTap: onBack),
        const Spacer(),
        const Spacer(),
        const Spacer(),
        const SizedBox(width: 44),
      ],
    );
  }
}

class _LogoHeader extends StatelessWidget {
  const _LogoHeader({required this.selectedRole});

  final String selectedRole;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final role = _roles[selectedRole] ?? _roles['ogretmen']!;

    return Column(
      children: <Widget>[
        Container(
          width: 72,
          height: 72,
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: <Color>[
                _LoginPageState._primary,
                _LoginPageState._primaryDark,
              ],
            ),
            borderRadius: BorderRadius.circular(24),
            boxShadow: const <BoxShadow>[
              BoxShadow(
                color: Color(0x19082B4F),
                blurRadius: 22,
                offset: Offset(0, 12),
              ),
            ],
          ),
          child: const Icon(Icons.school_rounded, color: Colors.white, size: 32),
        ),
        const SizedBox(height: 16),
        Text(
          'Giris Yapin',
          textAlign: TextAlign.center,
          style: theme.textTheme.headlineSmall?.copyWith(
            color: _LoginPageState._textPrimary,
            fontWeight: FontWeight.w800,
          ),
        ),
        const SizedBox(height: 8),
        Text(
          '${role.label} hesabinizla devam edin.',
          textAlign: TextAlign.center,
          style: theme.textTheme.bodyMedium?.copyWith(
            color: _LoginPageState._textSecondary,
            height: 1.5,
          ),
        ),
      ],
    );
  }
}

class _AuthDivider extends StatelessWidget {
  const _AuthDivider();

  @override
  Widget build(BuildContext context) {
    final textStyle = Theme.of(context).textTheme.bodySmall?.copyWith(
      color: _LoginPageState._textSecondary,
      fontWeight: FontWeight.w500,
    );

    return Row(
      children: <Widget>[
        const Expanded(
          child: Divider(
            color: _LoginPageState._border,
            thickness: 1,
            height: 1,
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12),
          child: Text('veya sununla devam et', style: textStyle),
        ),
        const Expanded(
          child: Divider(
            color: _LoginPageState._border,
            thickness: 1,
            height: 1,
          ),
        ),
      ],
    );
  }
}

class _IconPill extends StatelessWidget {
  const _IconPill({required this.icon, required this.onTap});

  final IconData icon;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: onTap,
      child: Container(
        width: 44,
        height: 44,
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: _LoginPageState._border),
        ),
        child: Icon(icon, color: _LoginPageState._primary),
      ),
    );
  }
}

const Map<String, _RoleLabel> _roles = <String, _RoleLabel>{
  'ogretmen': _RoleLabel('Ogretmen'),
  'ogrenci': _RoleLabel('Ogrenci'),
  'veli': _RoleLabel('Veli'),
};

class _RoleLabel {
  const _RoleLabel(this.label);

  final String label;
}
